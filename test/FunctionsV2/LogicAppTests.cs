using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Diagnostics.Tracing;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class LogicAppTests : IDisposable
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;
        private readonly bool useTestLogger = !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        private readonly LogEventTraceListener eventSourceListener;

        public LogicAppTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
            this.eventSourceListener = new LogEventTraceListener();
            this.StartLogCapture();
        }

        public void Dispose()
        {
            this.eventSourceListener.Dispose();
        }

        private void OnEventSourceListenerTraceLog(object sender, LogEventTraceListener.TraceLogEventArgs e)
        {
            this.output.WriteLine($"      ETW: {e.ProviderName} [{e.Level}] : {e.Message}");
        }

        private void StartLogCapture()
        {
            if (this.useTestLogger)
            {
                var traceConfig = new Dictionary<string, TraceEventLevel>
                {
                    { "DurableTask-AzureStorage", TraceEventLevel.Informational },
                    { "7DA4779A-152E-44A2-A6F2-F80D991A5BEE", TraceEventLevel.Warning }, // DurableTask.Core
                };

                this.eventSourceListener.OnTraceLog += this.OnEventSourceListenerTraceLog;

                ////string sessionName = "DTFxTrace" + Guid.NewGuid().ToString("N");
                string sessionName = "DTFxTrace_LogicAppTests";
                this.eventSourceListener.CaptureLogs(sessionName, traceConfig);
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ComposeHttp()
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ComposeHttp),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                string functionName = $"LogicAppWF::{nameof(this.ComposeHttp)}";
                TestDurableClient client = await host.StartOrchestratorAsync(functionName, null, this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(400));

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                JObject output = Assert.IsType<JObject>(status.Output);
                JToken statusValue = Assert.Contains("statusCode", output);
                Assert.Equal(200, (int)statusValue);

                await host.StopAsync();
            }
        }
    }
}
