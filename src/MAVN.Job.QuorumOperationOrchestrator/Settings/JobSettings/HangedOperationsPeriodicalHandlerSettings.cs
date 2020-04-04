using System;

namespace MAVN.Job.QuorumOperationOrchestrator.Settings.JobSettings
{
    public class HangedOperationsPeriodicalHandlerSettings
    {
        public TimeSpan IdlePeriod { get; set; }

        public TimeSpan AcceptedOperationsWarningTimeout { get; set; }
    }
}
