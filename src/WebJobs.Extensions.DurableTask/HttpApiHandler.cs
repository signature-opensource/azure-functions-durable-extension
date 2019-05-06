// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ActionResults;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class HttpApiHandler
    {
        private const string InstancesControllerSegment = "/instances/";
        private const string OrchestratorsControllerSegment = "/orchestrators/";
        private const string EntitiesControllerSegment = "/entities/";
        private const string TaskHubParameter = "taskHub";
        private const string ConnectionParameter = "connection";
        private const string RaiseEventOperation = "raiseEvent";
        private const string TerminateOperation = "terminate";
        private const string RewindOperation = "rewind";
        private const string ShowHistoryParameter = "showHistory";
        private const string ShowHistoryOutputParameter = "showHistoryOutput";
        private const string ShowInputParameter = "showInput";
        private const string CreatedTimeFromParameter = "createdTimeFrom";
        private const string CreatedTimeToParameter = "createdTimeTo";
        private const string RuntimeStatusParameter = "runtimeStatus";
        private const string PageSizeParameter = "top";

        private readonly DurableTaskExtension config;
        private readonly ILogger logger;

        public HttpApiHandler(DurableTaskExtension config, ILogger logger)
        {
            this.config = config;
            this.logger = logger;
        }

        internal ActionResult<CheckStatusResponse> CreateCheckStatusResponse(
            HttpRequest request,
            string instanceId,
            OrchestrationClientAttribute attribute)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName);
            return this.CreateCheckStatusResponseMessage(
                request,
                httpManagementPayload.Id,
                httpManagementPayload.StatusQueryGetUri,
                httpManagementPayload.SendEventPostUri,
                httpManagementPayload.TerminatePostUri,
                httpManagementPayload.RewindPostUri,
                httpManagementPayload.PurgeHistoryDeleteUri);
        }

        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHub,
            string connectionName)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(null, instanceId, taskHub, connectionName);
            return httpManagementPayload;
        }

        internal async Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequest request,
            string instanceId,
            OrchestrationClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            if (retryInterval > timeout)
            {
                throw new ArgumentException($"Total timeout {timeout.TotalSeconds} should be bigger than retry timeout {retryInterval.TotalSeconds}");
            }

            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName);

            IDurableOrchestrationClient client = this.GetClient(request);
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
                if (status != null)
                {
                    if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
                    {
                        return new OkObjectResult(status);
                    }

                    if (status.RuntimeStatus == OrchestrationRuntimeStatus.Canceled ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                    {
                        return await this.HandleGetStatusRequestAsync(request, instanceId);
                    }
                }

                TimeSpan elapsed = stopwatch.Elapsed;
                if (elapsed < timeout)
                {
                    TimeSpan remainingTime = timeout.Subtract(elapsed);
                    await Task.Delay(remainingTime > retryInterval ? retryInterval : remainingTime);
                }
                else
                {
                    return this.CreateCheckStatusResponseMessage(
                        request,
                        instanceId,
                        httpManagementPayload.StatusQueryGetUri,
                        httpManagementPayload.SendEventPostUri,
                        httpManagementPayload.TerminatePostUri,
                        httpManagementPayload.RewindPostUri,
                        httpManagementPayload.PurgeHistoryDeleteUri).Result;
                }
            }
        }

        public async Task<IActionResult> HandleRequestAsync(HttpRequest request)
        {
            try
            {
                string path = request.Path.ToString().TrimEnd('/');
                int i = path.IndexOf(OrchestratorsControllerSegment, StringComparison.OrdinalIgnoreCase);
                int nextSlash = -1;
                if (i >= 0 && request.Method == HttpMethod.Post.ToString())
                {
                    string functionName;
                    string instanceId = string.Empty;

                    i += OrchestratorsControllerSegment.Length;
                    nextSlash = path.IndexOf('/', i);

                    if (nextSlash < 0)
                    {
                        functionName = path.Substring(i);
                    }
                    else
                    {
                        functionName = path.Substring(i, nextSlash - i);
                        i = nextSlash + 1;
                        instanceId = path.Substring(i);
                    }

                    return await this.HandleStartOrchestratorRequestAsync(request, functionName, instanceId);
                }

                i = path.IndexOf(EntitiesControllerSegment, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && (request.Method == HttpMethod.Get.ToString() || request.Method == HttpMethod.Post.ToString()))
                {
                    EntityId entityId;

                    i += EntitiesControllerSegment.Length;
                    nextSlash = path.IndexOf('/', i);

                    try
                    {
                        if (nextSlash < 0)
                        {
                            entityId = new EntityId(path.Substring(i), string.Empty);
                        }
                        else
                        {
                            entityId = new EntityId(path.Substring(i, nextSlash - i), path.Substring(nextSlash + 1));
                        }
                    }
                    catch (ArgumentException e)
                    {
                        return new BadRequestObjectResult(new { Message = e.Message });
                    }

                    if (request.Method == HttpMethod.Get.ToString())
                    {
                        return await this.HandleGetEntityRequestAsync(request, entityId);
                    }
                    else
                    {
                        return await this.HandlePostEntityOperationRequestAsync(request, entityId);
                    }
                }

                i = path.IndexOf(InstancesControllerSegment, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                {
                    // Retrieve All Status or conditional query in case of the request URL ends e.g. /instances/
                    if (request.Method == HttpMethod.Get.ToString()
                        && path.EndsWith(InstancesControllerSegment.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    {
                        return (await this.HandleGetStatusRequestAsync(request)).Result;
                    }

                    if (request.Method == HttpMethod.Delete.ToString()
                        && path.EndsWith(InstancesControllerSegment.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    {
                        return (await this.HandleDeleteHistoryWithFiltersRequestAsync(request)).Result;
                    }

                    return new NotFoundResult();
                }

                i += InstancesControllerSegment.Length;
                nextSlash = path.IndexOf('/', i);

                if (nextSlash < 0)
                {
                    string instanceId = path.Substring(i);
                    if (request.Method == HttpMethod.Get.ToString())
                    {
                        return await this.HandleGetStatusRequestAsync(request, instanceId);
                    }

                    if (request.Method == HttpMethod.Delete.ToString())
                    {
                        return (await this.HandleDeleteHistoryByIdRequestAsync(request, instanceId)).Result;
                    }
                }
                else if (request.Method == HttpMethod.Post.ToString())
                {
                    string instanceId = path.Substring(i, nextSlash - i);
                    i = nextSlash + 1;
                    nextSlash = path.IndexOf('/', i);
                    if (nextSlash < 0)
                    {
                        string operation = path.Substring(i);
                        if (string.Equals(operation, TerminateOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleTerminateInstanceRequestAsync(request, instanceId);
                        }
                        else if (string.Equals(operation, RewindOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleRewindInstanceRequestAsync(request, instanceId);
                        }
                    }
                    else
                    {
                        string operation = path.Substring(i, nextSlash - i);
                        if (string.Equals(operation, RaiseEventOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            i = nextSlash + 1;
                            nextSlash = path.IndexOf('/', i);
                            if (nextSlash < 0)
                            {
                                string eventName = path.Substring(i);
                                return await this.HandleRaiseEventRequestAsync(request, instanceId, eventName);
                            }
                        }
                    }
                }

                return new BadRequestObjectResult(new { Message = "No such API" });
            }

            /* Some handler methods throw ArgumentExceptions in specialized cases which should be returned to the client, such as when:
             *     - the function name is not found (starting a new function)
             *     - the orchestration instance is not in a Failed state (rewinding an orchestration instance)
            */
            catch (ArgumentException e)
            {
                return new BadRequestObjectResult(new
                {
                    Message = "One or more of the arguments submitted is incorrect",
                    ExceptionMessage = e.Message,
                    ExceptionType = e.GetType().FullName,
                    e.StackTrace,
                });
            }
            catch (Exception e)
            {
                return new InternalServerErrorResponse(
                    new
                    {
                        Message = "Something went wrong while processing your request",
                        ExceptionMessage = e.Message,
                        ExceptionType = e.GetType().FullName,
                        e.StackTrace,
                    });
            }
        }

        private async Task<ActionResult<List<StatusResponsePayload>>> HandleGetStatusRequestAsync(
            HttpRequest request)
        {
            IDurableOrchestrationClient client = this.GetClient(request);
            IQueryCollection queryNameValuePairs = request.Query;
            var createdTimeFrom = GetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeFromParameter, default(DateTime));
            var createdTimeTo = GetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeToParameter, default(DateTime));
            var runtimeStatus = GetIEnumerableQueryParameterValue<OrchestrationRuntimeStatus>(queryNameValuePairs, RuntimeStatusParameter);
            var pageSize = GetIntQueryParameterValue(queryNameValuePairs, PageSizeParameter);

            var continuationToken = "";
            if (!StringValues.IsNullOrEmpty(request.Headers["x-ms-continuation-token"]))
            {
                continuationToken = request.Headers["x-ms-continuation-token"].FirstOrDefault();
            }

            IList<DurableOrchestrationStatus> statusForAllInstances;
            var nextContinuationToken = "";

            if (pageSize > 0)
            {
                var condition = new OrchestrationStatusQueryCondition()
                {
                    CreatedTimeFrom = createdTimeFrom,
                    CreatedTimeTo = createdTimeTo,
                    RuntimeStatus = runtimeStatus,
                    PageSize = pageSize,
                    ContinuationToken = continuationToken,
                };
                var context = await client.GetStatusAsync(condition, CancellationToken.None);
                statusForAllInstances = context.DurableOrchestrationState.ToList();
                nextContinuationToken = context.ContinuationToken;
            }
            else
            {
                statusForAllInstances = await client.GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus);
            }

            var results = new List<StatusResponsePayload>(statusForAllInstances.Count);
            foreach (var state in statusForAllInstances)
            {
                results.Add(this.ConvertFrom(state));
            }

            return new ContinuationResult(nextContinuationToken, results);
        }

        private async Task<ActionResult<PurgeHistoryResult>> HandleDeleteHistoryByIdRequestAsync(
            HttpRequest request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);
            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId, showHistory: false);
            if (status == null)
            {
                return new NotFoundResult();
            }

            return await client.PurgeInstanceHistoryAsync(instanceId);
        }

        private async Task<ActionResult<PurgeHistoryResult>> HandleDeleteHistoryWithFiltersRequestAsync(HttpRequest request)
        {
            IDurableOrchestrationClient client = this.GetClient(request);
            var queryNameValuePairs = request.Query;
            var createdTimeFrom =
                GetDateTimeQueryParameterValue(queryNameValuePairs, "createdTimeFrom", DateTime.MinValue);

            if (createdTimeFrom == DateTime.MinValue)
            {
                return new BadRequestObjectResult(new { Message = "Please provide value for 'createdTimeFrom' parameter." });
            }

            var createdTimeTo =
                GetDateTimeQueryParameterValue(queryNameValuePairs, "createdTimeTo", DateTime.UtcNow);
            var runtimeStatusCollection =
                GetIEnumerableQueryParameterValue<OrchestrationStatus>(queryNameValuePairs, "runtimeStatus");

            PurgeHistoryResult purgeHistoryResult = await client.PurgeInstanceHistoryAsync(createdTimeFrom, createdTimeTo, runtimeStatusCollection);

            if (purgeHistoryResult == null || purgeHistoryResult.InstancesDeleted == 0)
            {
                return new NotFoundResult();
            }

            return purgeHistoryResult;
        }

        private async Task<IActionResult> HandleGetStatusRequestAsync(
            HttpRequest request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            var queryNameValuePairs = request.Query;
            var showHistory = GetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryParameter, defaultValue: false);
            var showHistoryOutput = GetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryOutputParameter, defaultValue: false);

            bool showInput = GetBooleanQueryParameterValue(queryNameValuePairs, ShowInputParameter, defaultValue: true);

            var status = await client.GetStatusAsync(instanceId, showHistory, showHistoryOutput, showInput);
            if (status == null)
            {
                return new NotFoundResult();
            }

            switch (status.RuntimeStatus)
            {
                // The orchestration is running - return 202 w/Location header
                case OrchestrationRuntimeStatus.Running:
                case OrchestrationRuntimeStatus.Pending:
                case OrchestrationRuntimeStatus.ContinuedAsNew:
                    return new AcceptedWithRetryResult(request.GetDisplayUrl(), TimeSpan.FromSeconds(5));

                // The orchestration has failed - return 500 w/out Location header
                case OrchestrationRuntimeStatus.Failed:
                    return new StatusCodeResult((int)HttpStatusCode.InternalServerError);

                // The orchestration is not running - return 200 w/out Location header
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    return new OkResult();
                default:
                    this.logger.LogError($"Unknown runtime state '{status.RuntimeStatus}'.");
                    return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }
        }

        private StatusResponsePayload ConvertFrom(DurableOrchestrationStatus status)
        {
            return new StatusResponsePayload
            {
                Name = status.Name,
                InstanceId = status.InstanceId,
                RuntimeStatus = status.RuntimeStatus.ToString(),
                Input = status.Input,
                CustomStatus = status.CustomStatus,
                Output = status.Output,
                CreatedTime = status.CreatedTime.ToString("s") + "Z",
                LastUpdatedTime = status.LastUpdatedTime.ToString("s") + "Z",
                HistoryEvents = status.History,
            };
        }

        private static IEnumerable<T> GetIEnumerableQueryParameterValue<T>(IQueryCollection queryStringNameValueCollection, string queryParameterName)
            where T : struct
        {
            var results = new List<T>();
            StringValues parameters = queryStringNameValueCollection[queryParameterName];

            foreach (var value in parameters.SelectMany(x => x.Split(',')))
            {
                if (Enum.TryParse(value, out T result))
                {
                    results.Add(result);
                }
            }

            return results;
        }

        private static DateTime GetDateTimeQueryParameterValue(IQueryCollection queryStringNameValueCollection, string queryParameterName, DateTime defaultDateTime)
        {
            var value = queryStringNameValueCollection[queryParameterName];
            return DateTime.TryParse(value, out DateTime dateTime) ? dateTime : defaultDateTime;
        }

        private static bool GetBooleanQueryParameterValue(IQueryCollection queryStringNameValueCollection, string queryParameterName, bool defaultValue)
        {
            var value = queryStringNameValueCollection[queryParameterName];
            return bool.TryParse(value, out bool parsedValue) ? parsedValue : defaultValue;
        }

        private static int GetIntQueryParameterValue(IQueryCollection queryStringNameValueCollection, string queryParameterName)
        {
            var value = queryStringNameValueCollection[queryParameterName];
            return int.TryParse(value, out var intValue) ? intValue : 0;
        }

        private async Task<IActionResult> HandleTerminateInstanceRequestAsync(
            HttpRequest request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            var status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return new NotFoundResult();
            }

            switch (status.RuntimeStatus)
            {
                case OrchestrationRuntimeStatus.Failed:
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    return new StatusCodeResult((int)HttpStatusCode.Gone);
            }

            string reason = request.Query["reason"];

            await client.TerminateAsync(instanceId, reason);

            return new AcceptedResult();
        }

        private async Task<IActionResult> HandleStartOrchestratorRequestAsync(
            HttpRequest request,
            string functionName,
            string instanceId)
        {
            try
            {
                IDurableOrchestrationClient client = this.GetClient(request);

                object input = null;
                if (request.Body != null)
                {
                    using (Stream s = request.Body)
                    using (StreamReader sr = new StreamReader(s))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        JsonSerializer serializer = JsonSerializer.Create(MessagePayloadDataConverter.MessageSettings);
                        input = serializer.Deserialize<object>(reader);
                    }
                }

                string id = await client.StartNewAsync(functionName, instanceId, input);

                TimeSpan? timeout = GetTimeSpan(request, "timeout");
                TimeSpan? pollingInterval = GetTimeSpan(request, "pollingInterval");

                if (timeout.HasValue && pollingInterval.HasValue)
                {

                    return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, id, timeout.Value, pollingInterval.Value);
                }
                else
                {
                    return client.CreateCheckStatusResponse(request, id).Result;
                }
            }
            catch (JsonReaderException e)
            {
                return new BadRequestObjectResult(new
                {
                    Message = "Invalid JSON content",
                    ExceptionMessage = e.Message,
                    ExceptionType = e.GetType().FullName,
                    e.StackTrace,
                });
            }
        }

        private async Task<IActionResult> HandleRewindInstanceRequestAsync(
           HttpRequest request,
           string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            var status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return new NotFoundResult();
            }

            switch (status.RuntimeStatus)
            {
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    return new StatusCodeResult((int)HttpStatusCode.Gone);
            }

            string reason = request.Query["reason"];

            await client.RewindAsync(instanceId, reason);

            return new AcceptedResult();
        }

        private async Task<IActionResult> HandleRaiseEventRequestAsync(
            HttpRequest request,
            string instanceId,
            string eventName)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            var status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return new NotFoundResult();
            }

            switch (status.RuntimeStatus)
            {
                case OrchestrationRuntimeStatus.Failed:
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    return new StatusCodeResult((int) HttpStatusCode.Gone);
            }

            string mediaType = request.ContentType;
            if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { Message = "Only application/json request content is supported" });
            }

            var contentStream = new StreamReader(request.Body);
            string stringData = await contentStream.ReadToEndAsync();

            object eventData;
            try
            {
                eventData = !string.IsNullOrEmpty(stringData) ? JToken.Parse(stringData) : null;
            }
            catch (JsonReaderException e)
            {
                return new BadRequestObjectResult(new
                {
                    Message = "Invalid JSON content",
                    ExceptionMessage = e.Message,
                    ExceptionType = e.GetType().FullName,
                    e.StackTrace,
                });
            }

            await client.RaiseEventAsync(instanceId, eventName, eventData);
            return new AcceptedResult();
        }

        private async Task<IActionResult> HandleGetEntityRequestAsync(
            HttpRequest request,
            EntityId entityId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            var response = await client.ReadEntityStateAsync<JToken>(entityId);

            if (!response.EntityExists)
            {
                return new NotFoundResult();
            }
            else
            {
                return new JsonResult(response.EntityState);
            }
        }

        private async Task<IActionResult> HandlePostEntityOperationRequestAsync(
            HttpRequest request,
            EntityId entityId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            string operationName = request.Query["op"].ToString() ?? string.Empty;

            if (request.Body == null)
            {
                await client.SignalEntityAsync(entityId, operationName);
                return new AcceptedResult();
            }
            else
            {
                var contentStream = new StreamReader(request.Body);
                var requestContent = await contentStream.ReadToEndAsync();
                string mediaType = request.ContentType;
                object operationInput;
                if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        operationInput = JToken.Parse(requestContent);
                    }
                    catch (JsonException e)
                    {
                        return new BadRequestObjectResult(new { Message = "Could not parse JSON content: " + e.Message });
                    }
                }
                else
                {
                    operationInput = requestContent;
                }

                await client.SignalEntityAsync(entityId, operationName, operationInput);
                return new OkResult();
            }
        }

        private IDurableOrchestrationClient GetClient(HttpRequest request)
        {
            string taskHub = null;
            string connectionName = null;

            foreach (var key in request.Query.Keys)
            {
                if (taskHub == null
                    && key.Equals(TaskHubParameter, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(request.Query[key]))
                {
                    taskHub = request.Query[key];
                }
                else if (connectionName == null
                    && key.Equals(ConnectionParameter, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(request.Query[key]))
                {
                    connectionName = request.Query[key];
                }
            }

            var attribute = new OrchestrationClientAttribute
            {
                TaskHub = taskHub,
                ConnectionName = connectionName,
            };

            return this.GetClient(attribute);
        }

        // protected virtual to allow mocking in unit tests.
        protected virtual IDurableOrchestrationClient GetClient(OrchestrationClientAttribute attribute)
        {
            return this.config.GetClient(attribute);
        }

        internal HttpCreationPayload GetInstanceCreationLinks()
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.config.Options.NotificationUrl;

            string hostUrl = notificationUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            string instancePrefix = baseUrl + OrchestratorsControllerSegment + "{functionName}[/{instanceId}]";

            string querySuffix = !string.IsNullOrEmpty(notificationUri.Query)
                ? notificationUri.Query.TrimStart('?')
                : string.Empty;

            HttpCreationPayload httpCreationPayload = new HttpCreationPayload
            {
                CreateNewInstancePostUri = instancePrefix + "?" + querySuffix,
                CreateAndWaitOnNewInstancePostUri = instancePrefix + "?timeout={timeoutInSeconds}&pollingInterval={intervalInSeconds}&" + querySuffix,
            };

            return httpCreationPayload;
        }

        private HttpManagementPayload GetClientResponseLinks(
            HttpRequest request,
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.config.Options.NotificationUrl;
            Uri baseUri = notificationUri;
            if (request != null)
            {
                baseUri = new Uri(request.GetDisplayUrl());
            }

            // e.g. http://{host}/admin/extensions/DurableTaskExtension?code={systemKey}
            string hostUrl = baseUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            string allInstancesPrefix = baseUrl + InstancesControllerSegment;
            string instancePrefix = allInstancesPrefix + WebUtility.UrlEncode(instanceId);

            string taskHub = WebUtility.UrlEncode(taskHubName ?? this.config.Options.HubName);
            string connection = WebUtility.UrlEncode(connectionName ?? this.config.Options.GetConnectionStringName() ?? ConnectionStringNames.Storage);

            string querySuffix = $"{TaskHubParameter}={taskHub}&{ConnectionParameter}={connection}";
            if (!string.IsNullOrEmpty(notificationUri.Query))
            {
                // This is expected to include the auto-generated system key for this extension.
                querySuffix += "&" + notificationUri.Query.TrimStart('?');
            }

            HttpManagementPayload httpManagementPayload = new HttpManagementPayload
            {
                Id = instanceId,
                StatusQueryGetUri = instancePrefix + "?" + querySuffix,
                SendEventPostUri = instancePrefix + "/" + RaiseEventOperation + "/{eventName}?" + querySuffix,
                TerminatePostUri = instancePrefix + "/" + TerminateOperation + "?reason={text}&" + querySuffix,
                RewindPostUri = instancePrefix + "/" + RewindOperation + "?reason={text}&" + querySuffix,
                PurgeHistoryDeleteUri = instancePrefix + "?" + querySuffix,
            };

            return httpManagementPayload;
        }

        private ActionResult<CheckStatusResponse> CreateCheckStatusResponseMessage(HttpRequest request, string instanceId, string statusQueryGetUri, string sendEventPostUri, string terminatePostUri, string rewindPostUri, string purgeHistoryDeleteUri)
        {
            return new AcceptedWithRetryResult(
                statusQueryGetUri,
                TimeSpan.FromSeconds(10),
                new CheckStatusResponse
                {
                    Id = instanceId,
                    StatusQueryGetUri = statusQueryGetUri,
                    SendEventPostUri = sendEventPostUri,
                    TerminatePostUri = terminatePostUri,
                    RewindPostUri = rewindPostUri,
                    PurgeHistoryDeleteUri = purgeHistoryDeleteUri,
                });
        }

        private void ThrowIfWebhooksNotConfigured()
        {
            if (this.config.Options.NotificationUrl == null)
            {
                throw new InvalidOperationException("Webhooks are not configured");
            }
        }

        private static TimeSpan? GetTimeSpan(HttpRequest request, string queryParameterName)
        {
            string queryParameterStringValue = request.Query[queryParameterName];
            if (string.IsNullOrEmpty(queryParameterStringValue))
            {
                return null;
            }

            return TimeSpan.FromSeconds(double.Parse(queryParameterStringValue));
        }
    }
}
