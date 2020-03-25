using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common;
using Lykke.Common.Log;
using Lykke.Job.QuorumOperationOrchestrator.Domain.Services;

namespace Lykke.Job.QuorumOperationOrchestrator.PeriodicalHandlers
{
    public class OperationsPeriodicalHandler : IStartable, IStopable
    {
        private readonly IOperationOrchestratorService _operationOrchestratorService;
        private readonly TimerTrigger _timerTrigger;

        public OperationsPeriodicalHandler(
            IOperationOrchestratorService operationOrchestratorService,
            TimeSpan idlePeriod,
            ILogFactory logFactory)
        {
            _operationOrchestratorService = operationOrchestratorService;
            _timerTrigger = new TimerTrigger(nameof(OperationsPeriodicalHandler), idlePeriod, logFactory);
            _timerTrigger.Triggered += Execute;
        }

        public void Start()
        {
            _timerTrigger.Start();
        }

        public void Stop()
        {
            _timerTrigger?.Stop();
        }

        public void Dispose()
        {
            _timerTrigger?.Stop();
            _timerTrigger?.Dispose();
        }

        private async Task Execute(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationToken)
        {
            await _operationOrchestratorService.ProcessOperationsBatchAsync();
        }
    }
}
