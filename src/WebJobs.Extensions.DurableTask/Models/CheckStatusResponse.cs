// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Models
{
    /// <summary>
    /// A structured representation of the JSON returned from a successful call to the  Check Orchestration Status
    /// Api.
    /// </summary>
    public class CheckStatusResponse
    {
        /// <summary>
        /// The instance id of the orchestration.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The Uri of the API to query the status for the orchestration.
        /// </summary>
        public string StatusQueryGetUri { get; set; }

        /// <summary>
        /// The Uri of the API to send an event to the orchestration.
        /// </summary>
        public string SendEventPostUri { get; set; }

        /// <summary>
        /// The Uri of the API to terminate the orchestration.
        /// </summary>
        public string TerminatePostUri { get; set; }

        /// <summary>
        /// The Uri of the API to rewind the orchestration.
        /// </summary>
        public string RewindPostUri { get; set; }

        /// <summary>
        /// The Uri of the API to Purge the history for the orchestration.
        /// </summary>
        public string PurgeHistoryDeleteUri { get; set; }
    }
}
