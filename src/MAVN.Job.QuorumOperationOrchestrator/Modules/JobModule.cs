using Autofac;
using Common;
using JetBrains.Annotations;
using MAVN.Job.QuorumOperationOrchestrator.Domain.Services;
using MAVN.Job.QuorumOperationOrchestrator.DomainServices;
using MAVN.Job.QuorumOperationOrchestrator.PeriodicalHandlers;
using MAVN.Job.QuorumOperationOrchestrator.Services;
using MAVN.Job.QuorumOperationOrchestrator.Settings;
using Lykke.Sdk;
using Lykke.Sdk.Health;
using Lykke.Service.PrivateBlockchainFacade.Client;
using Lykke.Service.QuorumOperationExecutor.Client;
using Lykke.SettingsReader;

namespace MAVN.Job.QuorumOperationOrchestrator.Modules
{
    [UsedImplicitly]
    public class JobModule : Module
    {
        private readonly AppSettings _appSettings;

        public JobModule(IReloadingManager<AppSettings> appSettings)
        {
            _appSettings = appSettings.CurrentValue;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            builder.RegisterType<StartupManager>()
                .As<IStartupManager>()
                .SingleInstance();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>()
                .AutoActivate()
                .SingleInstance();

            builder.RegisterType<OperationOrchestratorService>()
                .As<IOperationOrchestratorService>()
                .SingleInstance()
                .WithParameter("maxDegreeOfParallelism", _appSettings.QuorumOperationOrchestratorJob.OperationsPeriodicalHandler.MaxDegreeOfParallelism)
                .WithParameter("maxFailuresPerAddress", _appSettings.QuorumOperationOrchestratorJob.OperationsPeriodicalHandler.MaxFailuresPerAddress)
                .WithParameter("cleanAddressesWithFailuresTimeSpan", _appSettings.QuorumOperationOrchestratorJob.OperationsPeriodicalHandler.CleanAddressesWithFailuresTimespan)
                .WithParameter("acceptedOperationsWarningTimeout", _appSettings.QuorumOperationOrchestratorJob.HangedOperationsPeriodicalHandler.AcceptedOperationsWarningTimeout);

            builder.RegisterType<OperationsPeriodicalHandler>()
                .WithParameter(TypedParameter.From(_appSettings.QuorumOperationOrchestratorJob.OperationsPeriodicalHandler.IdlePeriod))
                .As<IStartable>()
                .As<IStopable>()
                .SingleInstance();

            builder.RegisterType<HangedOperationsPeriodicalHandler>()
                .WithParameter(TypedParameter.From(_appSettings.QuorumOperationOrchestratorJob.HangedOperationsPeriodicalHandler.IdlePeriod))
                .As<IStartable>()
                .As<IStopable>()
                .SingleInstance();

            builder.RegisterPrivateBlockchainFacadeClientWithApiKey(_appSettings.PrivateBlockchainFacadeService, null);

            builder.RegisterQuorumOperationExecutorClient(_appSettings.QuorumOperationExecutorService, null);
        }
    }
}
