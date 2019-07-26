// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class TaskHttpActivityShim : TaskActivity
    {
        private readonly HttpClient httpClient;
        private readonly DurableTaskExtension config;
        private readonly JsonSerializerSettings serializerSettings;

        public TaskHttpActivityShim(
            DurableTaskExtension config,
            HttpClient httpClientFactory)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.httpClient = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.serializerSettings = new JsonSerializerSettings();
        }

        public override string Run(TaskContext context, string input)
        {
            // This won't get called as long as we've implemented RunAsync.
            throw new NotImplementedException();
        }

        public async override Task<string> RunAsync(TaskContext context, string rawInput)
        {
            HttpRequestMessage requestMessage = await this.ReconstructHttpRequestMessage(rawInput);
            HttpResponseMessage response = await this.httpClient.SendAsync(requestMessage);
            DurableHttpResponse durableHttpResponse = await DurableHttpResponse.CreateDurableHttpResponseWithHttpResponseMessage(response);

            return MessagePayloadDataConverter.HttpConverter.Serialize(value: durableHttpResponse, formatted: true);
        }

        private static async Task<DurableHttpResponse> CreateDurableHttpResponseAsync(HttpResponseMessage response)
        {
            DurableHttpResponse durableHttpResponse = new DurableHttpResponse(response.StatusCode);
            durableHttpResponse.Headers = CreateStringValuesHeaderDictionary(response.Headers);
            durableHttpResponse.Content = await response.Content.ReadAsStringAsync();
            return durableHttpResponse;
        }

        internal static IDictionary<string, StringValues> CreateStringValuesHeaderDictionary(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            IDictionary<string, StringValues> newHeaders = new Dictionary<string, StringValues>();
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    newHeaders[header.Key] = new StringValues(header.Value.ToArray());
                }
            }

            return newHeaders;
        }

        private async Task<HttpRequestMessage> ReconstructHttpRequestMessage(string serializedRequest)
        {
            this.serializerSettings.TypeNameHandling = TypeNameHandling.Auto;

            // DeserializeObject deserializes into a List and then the first element
            // of that list is the serialized DurableHttpRequest
            IList<string> input = JsonConvert.DeserializeObject<List<string>>(serializedRequest, this.serializerSettings);
            string durableHttpRequestString = input.First();
            DurableHttpRequest durableHttpRequest = JsonConvert.DeserializeObject<DurableHttpRequest>(durableHttpRequestString, this.serializerSettings);

            HttpRequestMessage requestMessage = new HttpRequestMessage(durableHttpRequest.Method, durableHttpRequest.Uri);
            if (durableHttpRequest.Headers != null)
            {
                foreach (KeyValuePair<string, StringValues> entry in durableHttpRequest.Headers)
                {
                    requestMessage.Headers.Add(entry.Key, (IEnumerable<string>)entry.Value);
                }
            }

            if (durableHttpRequest.Content != null)
            {
                requestMessage.Content = new StringContent(durableHttpRequest.Content);

                // hack
                if (durableHttpRequest.Content.StartsWith("{") &&
                    !durableHttpRequest.Headers.ContainsKey("Content-Type"))
                {
                    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }
            }

            if (durableHttpRequest.TokenSource != null)
            {
                string accessToken = await durableHttpRequest.TokenSource.GetTokenAsync();
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return requestMessage;
        }
    }
}