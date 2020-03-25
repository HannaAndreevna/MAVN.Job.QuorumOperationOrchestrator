using System.Security.AccessControl;

namespace Lykke.Job.QuorumOperationOrchestrator.Settings.JobSettings
{
    public class QuorumOperationOrchestratorJobSettings
    {
        public DbSettings Db { get; set; }

        public OperationsPeriodicalHandlerSettings OperationsPeriodicalHandler { get; set; }

        public HangedOperationsPeriodicalHandlerSettings HangedOperationsPeriodicalHandler { get; set; }
    }
}
