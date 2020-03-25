using Lykke.Job.QuorumOperationOrchestrator.Settings.JobSettings;
using Lykke.Sdk.Settings;
using Lykke.Service.PrivateBlockchainFacade.Client;
using Lykke.Service.QuorumOperationExecutor.Client;

namespace Lykke.Job.QuorumOperationOrchestrator.Settings
{
    public class AppSettings : BaseAppSettings
    {
        public QuorumOperationOrchestratorJobSettings QuorumOperationOrchestratorJob { get; set; }

        public PrivateBlockchainFacadeServiceClientSettings PrivateBlockchainFacadeService { get; set; }

        public QuorumOperationExecutorServiceClientSettings QuorumOperationExecutorService { get; set; }
    }
}
