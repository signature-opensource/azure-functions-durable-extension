using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.LogicApps
{
    internal enum WorkflowStatus
    {
        None = 0,
        Succeeded,
        Failed,
        Cancelled,
    }
}
