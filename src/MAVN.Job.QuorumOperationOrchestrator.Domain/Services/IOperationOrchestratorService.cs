using System.Threading.Tasks;

namespace MAVN.Job.QuorumOperationOrchestrator.Domain.Services
{
    public interface IOperationOrchestratorService
    {
        Task ProcessOperationsBatchAsync();

        Task ProcessHangedOperationsAsync();
    }
}
