// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Response received from the HTTP request made by the Durable Function.
    /// </summary>
    public class DurableHttpResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableHttpResponse"/> class.
        /// </summary>
        /// <param name="statusCode">HTTP Status code returned from the HTTP call.</param>
        public DurableHttpResponse(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
            this.Headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Status code returned from an HTTP request.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Headers in the response from an HTTP request.
        /// </summary>
        // TODO: Need to change this to IDictionary<string, StringValues>
        public IDictionary<string, StringValues> Headers { get; set; }

        /// <summary>
        /// Content returned from an HTTP request.
        /// </summary>
        public string Content { get; set; }
    }
}
