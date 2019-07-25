// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OutOfProcTests
    {
        [Fact]
        public async Task SequencialOrchestration()
        {
            DurableHttpRequest request = null;

            var contextMock = new Mock<IDurableOrchestrationContext>();
            contextMock
                .Setup(ctx => ctx.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .Callback<DurableHttpRequest>(req => request = req)
                .Returns(Task.FromResult(new DurableHttpResponse(System.Net.HttpStatusCode.OK)));

            var shim = new OutOfProcOrchestrationShim(contextMock.Object);

            var jsonResponse = @"
{
    ""isDone"": false,
    ""actions"": [
        [{
            ""actionType"": ""CallHttp"",
            ""httpRequest"":
            {
                ""method"": ""POST"",
                ""uri"": ""https://example.com"",
                ""headers"": {
                    ""Content-Type"": ""application/json"",
                    ""Accept"": [""application/json"",""application/xml""],
                    ""x-ms-foo"": []
                },
                ""content"": ""5""
            }
        }]
    ]
}";

            var jsonObject = JObject.Parse(jsonResponse);
            bool moreWork = await shim.HandleOutOfProcExecutionAsync(jsonObject);

            Assert.True(moreWork);
            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://example.com"), request.Uri);
            Assert.Equal("5", request.Content);
            Assert.Equal(3, request.Headers.Count);

            Assert.True(request.Headers.TryGetValue("Content-Type", out StringValues contentTypeValues));
            Assert.Single(contentTypeValues);
            Assert.Equal("application/json", contentTypeValues[0]);

            Assert.True(request.Headers.TryGetValue("Accept", out StringValues acceptValues));
            Assert.Equal(2, acceptValues.Count);
            Assert.Equal("application/json", acceptValues[0]);
            Assert.Equal("application/xml", acceptValues[1]);

            Assert.True(request.Headers.TryGetValue("x-ms-foo", out StringValues customHeaderValues));
            Assert.Empty(customHeaderValues);
        }
    }
}
