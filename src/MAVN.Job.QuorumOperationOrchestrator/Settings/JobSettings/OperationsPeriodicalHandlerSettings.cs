using System;

namespace MAVN.Job.QuorumOperationOrchestrator.Settings.JobSettings
{
    public class OperationsPeriodicalHandlerSettings
    {
        public int MaxDegreeOfParallelism { get; set; }

        public TimeSpan IdlePeriod { get; set; }

        public int MaxFailuresPerAddress { get; set; }

        public TimeSpan CleanAddressesWithFailuresTimespan { get; set; }
    }
}
