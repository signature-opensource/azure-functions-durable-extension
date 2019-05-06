// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ActionResults
{
    internal class InternalServerErrorResponse : ObjectResult
    {
        public InternalServerErrorResponse(object value)
            : base(value)
        { }

        public async override Task ExecuteResultAsync(ActionContext context)
        {
            await base.ExecuteResultAsync(context);
            context.HttpContext.Response.StatusCode = 500;
        }
    }
}
