// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.WebApiCompatShim;
using Microsoft.Azure.Services.AppAuthentication;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Token Source implementation specific to Managed Identity Service.
    /// </summary>
    [DataContract]
    public class ManagedIdentityTokenSource : ITokenSource
    {
        [DataMember]
        private readonly string resource;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenSource"/> class.
        /// </summary>
        /// <param name="resource">The resource identifier of the target application.</param>
        public ManagedIdentityTokenSource(string resource)
        {
            this.resource = resource;
        }

        /// <inheritdoc/>
        public async Task<string> GetTokenAsync()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            string accessToken = await azureServiceTokenProvider.GetAccessTokenAsync(this.resource);
            return accessToken;
        }
    }
}