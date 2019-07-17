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

        public TaskHttpActivityShim(
            DurableTaskExtension config,
            HttpClient httpClientFactory)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.httpClient = httpClientFactory;
        }

        public override string Run(TaskContext context, string input)
        {
            // This won't get called as long as we've implemented RunAsync.
            throw new NotImplementedException();
        }

        public async override Task<string> RunAsync(TaskContext context, string rawInput)
        {
            HttpRequestMessage requestMessage = await CreateHttpRequestMessageAsync(rawInput);
            HttpResponseMessage response = await this.httpClient.SendAsync(requestMessage);
            DurableHttpResponse durableHttpResponse = await CreateDurableHttpResponseAsync(response);

            string serializedOutput = MessagePayloadDataConverter.HttpConverter.Serialize(durableHttpResponse, true);
            return serializedOutput;
        }

        private static async Task<DurableHttpResponse> CreateDurableHttpResponseAsync(HttpResponseMessage response)
        {
            DurableHttpResponse durableHttpResponse = new DurableHttpResponse(response.StatusCode);
            durableHttpResponse.Headers = CreateStringValuesHeaderDictionary(response.Headers);
            durableHttpResponse.Content = await response.Content.ReadAsStringAsync();
            return durableHttpResponse;
        }

        private static IDictionary<string, StringValues> CreateStringValuesHeaderDictionary(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            IDictionary<string, StringValues> newHeaders = new Dictionary<string, StringValues>();
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    foreach (var headerValue in header.Value)
                    {
                        if (newHeaders.ContainsKey(header.Key))
                        {
                            StringValues values = StringValues.Concat(headerValue, newHeaders[header.Key]);
                            newHeaders[header.Key] = values;
                        }
                        else
                        {
                            StringValues stringValues = new StringValues(headerValue);
                            newHeaders.Add(header.Key, stringValues);
                        }
                    }
                }
            }

            return newHeaders;
        }

        private static async Task<HttpRequestMessage> CreateHttpRequestMessageAsync(string rawInput)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
            serializerSettings.TypeNameHandling = TypeNameHandling.Auto;

            IList<string> input = JsonConvert.DeserializeObject<List<string>>(rawInput, serializerSettings);
            string durableHttpRequestString = input.First();
            DurableHttpRequest durableHttpRequest = JsonConvert.DeserializeObject<DurableHttpRequest>(durableHttpRequestString, serializerSettings);

            // Creating HttpRequestMessage
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
                requestMessage.Content = JsonConvert.DeserializeObject<HttpContent>(durableHttpRequest.Content);
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