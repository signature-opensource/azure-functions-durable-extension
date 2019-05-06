// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ActionResults
{
    internal class AcceptedWithRetryResult : AcceptedResult
    {
        private readonly TimeSpan retryAfter;

        public AcceptedWithRetryResult(string location, TimeSpan retryAfter)
             : base(location, null)
        {
            this.retryAfter = retryAfter;
        }

        public AcceptedWithRetryResult(string location, TimeSpan retryAfter, object value)
            : base(location, value)
        {
            this.retryAfter = retryAfter;
        }

        public async override Task ExecuteResultAsync(ActionContext context)
        {
            await base.ExecuteResultAsync(context);
            context.HttpContext.Response.Headers.Add(HeaderNames.RetryAfter, this.retryAfter.TotalSeconds.ToString());
        }
    }
}
