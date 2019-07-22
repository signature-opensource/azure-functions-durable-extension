// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Response received from the HTTP request made by the Durable Function.
    /// </summary>
    // [JsonConverter(typeof(DurableHttpResponseJsonConverter))]
    public class DurableHttpResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableHttpResponse"/> class.
        /// </summary>
        /// <param name="statusCode">HTTP Status code returned from the HTTP call.</param>
        [JsonConstructor]
        public DurableHttpResponse(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
            this.Headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableHttpResponse"/> class.
        /// </summary>
        /// <param name="jObject">JObject containing information from HttpResponseMessage.</param>
        public DurableHttpResponse(JObject jObject)
        {
            int codeInt = int.Parse(jObject["StatusCode"].Value<string>());
            HttpStatusCode statusCode = (HttpStatusCode)codeInt;

            Dictionary<string, StringValues> headerDictStringValues = new Dictionary<string, StringValues>();
            Dictionary<string, IEnumerable<string>> headersDictEnumerable = jObject["Headers"].ToObject<Dictionary<string, IEnumerable<string>>>();
            foreach (var header in headersDictEnumerable)
            {
                string key = header.Key;
                string[] headerValues = header.Value.ToArray<string>();
                StringValues values = new StringValues(headerValues);
                headerDictStringValues.Add(key, values);
            }

            this.StatusCode = statusCode;
            this.Headers = headerDictStringValues;
            this.Content = jObject["Content"].Value<string>();
        }

        /// <summary>
        /// Status code returned from an HTTP request.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Headers in the response from an HTTP request.
        /// </summary>
        public IDictionary<string, StringValues> Headers { get; set; }

        /// <summary>
        /// Content returned from an HTTP request.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Creates a DurableHttpResponse from an HttpResponseMessage.
        /// </summary>
        /// <param name="httpResponseMessage">HttpResponseMessage returned from the HTTP call.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<DurableHttpResponse> CreateDurableHttpResponseWithHttpResponseMessage(HttpResponseMessage httpResponseMessage)
        {
            DurableHttpResponse durableHttpResponse = new DurableHttpResponse(httpResponseMessage.StatusCode);
            durableHttpResponse.Headers = TaskHttpActivityShim.CreateStringValuesHeaderDictionary(httpResponseMessage.Headers);
            durableHttpResponse.Content = await httpResponseMessage.Content.ReadAsStringAsync();

            return durableHttpResponse;
        }
    }
}