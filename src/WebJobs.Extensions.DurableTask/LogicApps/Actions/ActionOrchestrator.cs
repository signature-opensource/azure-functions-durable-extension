using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.LogicApps.Schema;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.LogicApps.Actions
{
    internal class ActionOrchestrator
    {
        public static ValueTask<JToken> ExecuteAsync(WorkflowAction workflowAction, WorkflowContext context)
        {
            var action = ActionFactory.GetAction(workflowAction.Type);
            return action.ExecuteAsync(workflowAction.Inputs, context);
        }
    }
}
