// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Used for Durable HTTP functionality.
    /// </summary>
    public class HttpOptions
    {
        /// <summary>
        /// Reserved name to know when a TaskActivity should be an HTTP activity.
        /// </summary>
        public const string HttpTaskActivityReservedName = "Durable:Http:Async:Activity:Function";

        /// <summary>
        /// Time between the async http requests.
        /// </summary>
        public int DefaultAsyncRequestSleepTime { get; set; } = 5000;

        /// <summary>
        /// Boolean value specifying if an HTTP Request is async.
        /// </summary>
        public bool AsynchronousPatternEnabled { get; set; } = true;
    }
}
