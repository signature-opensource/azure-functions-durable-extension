using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.LogicApps;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.LogicApps.Actions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.LogicApps.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableLogicAppShim : TaskCommonShim
    {
        private readonly DurableOrchestrationContext context;

        public DurableLogicAppShim(DurableTaskExtension config, string name)
            : base(config)
        {
            this.context = new DurableOrchestrationContext(config, name);
        }

        public override DurableCommonContext Context => this.context;

        public override async Task<string> Execute(OrchestrationContext innerContext, string input)
        {
            string realFunctionName = this.context.FunctionName.Replace("LogicAppWF::", "");
            string workflowFilePath = Path.Combine(Environment.CurrentDirectory, realFunctionName, "workflow.json");

            string workflowJsonText;
            using (StreamReader reader = File.OpenText(workflowFilePath))
            {
                // NOTE: Cannot use async methods here since this is an orchestrator thread
                workflowJsonText = reader.ReadToEnd();
            }

            // Deserialize the workflow JSON into objects, and then sort those objects into the correct execution order
            WorkflowDocument doc = JsonConvert.DeserializeObject<WorkflowDocument>(workflowJsonText);
            IReadOnlyList<WorkflowAction> sortedActions = TopologicalSort.Sort(
                doc.Definition.Actions.Values,
                a => a.Dependencies.Select(p => p.Key).ToList(),
                a => a.Name);

            this.context.InnerContext = innerContext;
            this.context.InstanceId = innerContext.OrchestrationInstance.InstanceId;

            this.Config.TraceHelper.FunctionStarting(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                string.Empty /* input */,
                FunctionType.Orchestrator,
                this.context.IsReplaying);

            var workflowContext = new WorkflowContext(this.context);

            JToken lastOutput = JValue.CreateNull();

            try
            {
                // Execute the logic app!
                foreach (WorkflowAction action in sortedActions)
                {
                    lastOutput = await ActionOrchestrator.ExecuteAsync(action, workflowContext);

                    // Outputs are saved to the context, so that expressions can reference them in later steps
                    workflowContext.SaveOutput(action.Name, lastOutput);
                }
            }
            catch (Exception e)
            {
                // TODO: Find a way to remove the duplication between this and the orchestration shim
                string exceptionDetails = e.ToString();
                this.Config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    exceptionDetails,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying);

                if (!this.context.IsReplaying)
                {
                    this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        exceptionDetails,
                        this.context.IsReplaying));
                }

                var orchestrationException = new OrchestrationFailureException(
                    $"Orchestrator function '{this.context.Name}' failed: {e.Message}",
                    Utils.SerializeCause(e, MessagePayloadDataConverter.ErrorConverter));

                this.context.OrchestrationException = ExceptionDispatchInfo.Capture(orchestrationException);

                throw orchestrationException;
            }
            finally
            {
                this.context.IsCompleted = true;
            }

            string serializedOutput = lastOutput.ToString(Formatting.None);

            this.Config.TraceHelper.FunctionCompleted(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                this.Config.GetIntputOutputTrace(serializedOutput),
                this.context.ContinuedAsNew,
                FunctionType.Orchestrator,
                this.context.IsReplaying);

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.ContinuedAsNew,
                    this.context.IsReplaying));
            }

            return serializedOutput;
        }

        public override RegisteredFunctionInfo GetFunctionInfo()
        {
            throw new NotImplementedException();
        }

        public override string GetStatus()
        {
            return "nyi";
        }

        public override void RaiseEvent(OrchestrationContext context, string name, string input)
        {
            throw new NotImplementedException();
        }
    }
}
