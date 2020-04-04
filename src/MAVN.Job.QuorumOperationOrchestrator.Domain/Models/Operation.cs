using System;

namespace MAVN.Job.QuorumOperationOrchestrator.Domain.Models
{
    public class Operation
    {
        /// <summary>The operation id</summary>
        public Guid Id { get; set; }

        /// <summary>Master wallet address which holds the operation</summary>
        public string MasterWalletAddress { get; set; }

        /// <summary>Unique nonce number</summary>
        public long Nonce { get; set; }

        /// <summary>The operation type</summary>
        public string Type { get; set; }

        /// <summary>The payload serialized into JSON</summary>
        public string PayloadJson { get; set; }
    }
}
