using System.Threading.Tasks;

namespace Lykke.Job.QuorumOperationOrchestrator.Domain.Services
{
    public interface IOperationOrchestratorService
    {
        Task ProcessOperationsBatchAsync();

        Task ProcessHangedOperationsAsync();
    }
}
