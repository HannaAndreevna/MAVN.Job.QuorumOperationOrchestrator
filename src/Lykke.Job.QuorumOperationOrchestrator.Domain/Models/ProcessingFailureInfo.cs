using System;

namespace Lykke.Job.QuorumOperationOrchestrator.Domain.Models
{
    public class ProcessingFailureInfo
    {
        public int FailureCount { get; set; }

        public DateTime TimestampOfLastFailure { get; set; }
    }
}
