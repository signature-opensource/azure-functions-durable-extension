// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ActionResults
{
    internal class ContinuationResult : OkObjectResult
    {
        private readonly string continuationToken;

        public ContinuationResult(string continuationToken, object value)
            : base(value)
        {
            this.continuationToken = continuationToken;
        }

        public async override Task ExecuteResultAsync(ActionContext context)
        {
            await base.ExecuteResultAsync(context);
            context.HttpContext.Response.Headers.Add("x-ms-continuation-token", this.continuationToken);
        }
    }
}
