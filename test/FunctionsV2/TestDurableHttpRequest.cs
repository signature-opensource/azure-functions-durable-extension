// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [DataContract]
    public class TestDurableHttpRequest
    {
        public TestDurableHttpRequest(HttpMethod httpMethod, IDictionary<string, string> headers = null, string content = null, ITokenSource tokenSource = null)
        {
            this.HttpMethod = httpMethod;
            this.Headers = headers;
            this.Content = content;
            this.TokenSource = tokenSource;
        }

        [DataMember]
        public HttpMethod HttpMethod { get; set; }

        [DataMember]
        public IDictionary<string, string> Headers { get; set; }

        [DataMember]
        public string Content { get; set; }

        /// <summary>
        /// Information needed to get a token for a specified service.
        /// </summary>
        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        public ITokenSource TokenSource { get; set; }
    }
}