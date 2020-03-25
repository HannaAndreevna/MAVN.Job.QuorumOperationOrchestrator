using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.QuorumOperationOrchestrator.Domain.Services;

namespace Lykke.Job.QuorumOperationOrchestrator.PeriodicalHandlers
{
    public class HangedOperationsPeriodicalHandler : IStartable,IStopable
    {
        private readonly IOperationOrchestratorService _operationOrchestratorService;
        private readonly TimeSpan _idlePeriod;
        private readonly TimerTrigger _timerTrigger;
        private readonly ILog _log;

        public HangedOperationsPeriodicalHandler(IOperationOrchestratorService operationOrchestratorService
            , ILogFactory logFactory, TimeSpan idlePeriod)
        {
            _operationOrchestratorService = operationOrchestratorService;
            _idlePeriod = idlePeriod;
            _log = logFactory.CreateLog(this);
            _timerTrigger = new TimerTrigger(nameof(HangedOperationsPeriodicalHandler), _idlePeriod, logFactory);
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
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await _operationOrchestratorService.ProcessHangedOperationsAsync();

            stopWatch.Stop();

            if (stopWatch.Elapsed > _idlePeriod)
            {
                _log.Warning("Processing of hanged operations takes more time than the idle period of the handler.");
            }
        }
    }
}
