using MAVN.Job.QuorumOperationOrchestrator.Settings.JobSettings;
using Lykke.Sdk.Settings;
using MAVN.Service.PrivateBlockchainFacade.Client;
using MAVN.Service.QuorumOperationExecutor.Client;

namespace MAVN.Job.QuorumOperationOrchestrator.Settings
{
    public class AppSettings : BaseAppSettings
    {
        public QuorumOperationOrchestratorJobSettings QuorumOperationOrchestratorJob { get; set; }

        public PrivateBlockchainFacadeServiceClientSettings PrivateBlockchainFacadeService { get; set; }

        public QuorumOperationExecutorServiceClientSettings QuorumOperationExecutorService { get; set; }
    }
}
