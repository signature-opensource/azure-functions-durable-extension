using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.LogicApps.Actions
{
    internal abstract class ActionBase
    {
        public abstract ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context);
    }
}
