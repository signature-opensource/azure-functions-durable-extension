// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides functionality available to sources of tokens.
    /// </summary>
    public interface ITokenSource
    {
        /// <summary>
        /// Gets a token for a resource.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<string> GetTokenAsync();
    }
}
