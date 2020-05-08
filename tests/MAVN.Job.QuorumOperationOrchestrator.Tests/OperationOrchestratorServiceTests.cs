using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MAVN.Job.QuorumOperationOrchestrator.DomainServices;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using MAVN.Service.PrivateBlockchainFacade.Client;
using MAVN.Service.PrivateBlockchainFacade.Client.Models;
using MAVN.Service.QuorumOperationExecutor.Client;
using MAVN.Service.QuorumOperationExecutor.Client.Models.Requests;
using MAVN.Service.QuorumOperationExecutor.Client.Models.Responses;
using Moq;
using Xunit;

namespace MAVN.Job.QuorumOperationOrchestrator.Tests
{
    public class OperationOrchestratorServiceTests
    {
        private readonly TimeSpan _acceptedOperationsWarningTimeout = TimeSpan.FromDays(1);

        [Fact]
        public async Task TryToProcessBatchOfOperations_EverythingValid_ProcessedSuccessfully()
        {
            var maxDegreeOfParallelism = 5;
            var maxAddressFailures = 5;
            var cleanAddressesWithFailuresTimeSpan = TimeSpan.FromDays(1);
            var pBfClient = new Mock<IPrivateBlockchainFacadeClient>();

            var pbfResponse = GetOperationsData();

            pBfClient.SetupSequence(x => x.OperationsApi.GetNewOperationsAsync())
                .ReturnsAsync(pbfResponse)
                .ReturnsAsync(new List<NewOperationResponseModel>());

            pBfClient.Setup(x => x.OperationsApi.AcceptBatchAsync(It.IsAny<Dictionary<Guid, string>>()))
                .ReturnsAsync(new OperationStatusUpdateResponseModel())
                .Verifiable();

            var executorClient = new Mock<IQuorumOperationExecutorClient>();

            executorClient.Setup(x => x.OperationsApi.ExecuteOperationsBatchAsync(It.IsAny<ExecuteOperationsBatchRequest>()))
                .ReturnsAsync(new ExecuteOperationsBatchResponse
                {
                    Error = ExecuteOperationError.None,
                    TxHashesDict = new Dictionary<Guid, string>()
                });

            OperationOrchestratorService operationOrchestratorService;
            using (var logFactory = LogFactory.Create().AddUnbufferedConsole())
            {
                operationOrchestratorService = new OperationOrchestratorService(
                    maxDegreeOfParallelism,
                    maxAddressFailures,
                    cleanAddressesWithFailuresTimeSpan,
                    _acceptedOperationsWarningTimeout,
                    pBfClient.Object,
                    executorClient.Object,
                    logFactory);
            }

            await operationOrchestratorService.ProcessOperationsBatchAsync();

            pBfClient.Verify(x => x.OperationsApi.AcceptBatchAsync(It.IsAny<Dictionary<Guid, string>>()), Times.Exactly(3));
        }

        [Fact]
        public async Task TryToProcessBatchOfOperations_ErrorOnSomeAddressInOperationExecutor_QueuesForAddressesWithProblemsAreRemoved()
        {
            var maxDegreeOfParallelism = 5;
            var maxAddressFailures = 5;
            var cleanAddressesWithFailuresTimeSpan = TimeSpan.FromDays(1);
            var pBfClient = new Mock<IPrivateBlockchainFacadeClient>();

            var pbfResponse = GetOperationsData();

            pBfClient.SetupSequence(x => x.OperationsApi.GetNewOperationsAsync())
                .ReturnsAsync(pbfResponse)
                .ReturnsAsync(new List<NewOperationResponseModel>());

            pBfClient.Setup(x => x.OperationsApi.AcceptBatchAsync(It.IsAny<Dictionary<Guid, string>>()))
                .ReturnsAsync(new OperationStatusUpdateResponseModel())
                .Verifiable();

            var executorClient = new Mock<IQuorumOperationExecutorClient>();

            executorClient.Setup(x => x.OperationsApi.ExecuteOperationsBatchAsync(It.Is<ExecuteOperationsBatchRequest>(r => r.MasterWalletAddress == "address3")))
                .ReturnsAsync(
                    new ExecuteOperationsBatchResponse
                    {
                        Error = ExecuteOperationError.None,
                        TxHashesDict = pbfResponse.Where(o => o.MasterWalletAddress == "address3").ToDictionary(i => i.Id, i => "hash")
                    });

            executorClient.Setup(x => x.OperationsApi.ExecuteOperationsBatchAsync(It.Is<ExecuteOperationsBatchRequest>(r => r.MasterWalletAddress != "address3")))
                .ReturnsAsync(new ExecuteOperationsBatchResponse { Error = ExecuteOperationError.MasterWalletNotFound });

            OperationOrchestratorService operationOrchestratorService;
            using (var logFactory = LogFactory.Create().AddUnbufferedConsole())
            {
                operationOrchestratorService = new OperationOrchestratorService(
                    maxDegreeOfParallelism,
                    maxAddressFailures,
                    cleanAddressesWithFailuresTimeSpan,
                    _acceptedOperationsWarningTimeout,
                    pBfClient.Object,
                    executorClient.Object,
                    logFactory);
            }

            await operationOrchestratorService.ProcessOperationsBatchAsync();

            pBfClient.Verify(x => x.OperationsApi.AcceptBatchAsync(It.Is<Dictionary<Guid, string>>(d => d.Values.Any(v => v != "hash"))), Times.Never);

            pBfClient.Verify(x => x.OperationsApi.AcceptBatchAsync(It.Is<Dictionary<Guid, string>>(d => d.Values.Any(v => v == "hash"))), Times.Exactly(1));
        }

        [Fact]
        public async Task TryToProcessBatchOfOperations_OperationExecutorNotAvailable_QueuesForAddressesWithNoResponseAreRemoved()
        {
            var maxDegreeOfParallelism = 5;
            var maxAddressFailures = 5;
            var cleanAddressesWithFailuresTimeSpan = TimeSpan.FromDays(1);
            var pBfClient = new Mock<IPrivateBlockchainFacadeClient>();

            var pbfResponse = GetOperationsData();

            pBfClient.SetupSequence(x => x.OperationsApi.GetNewOperationsAsync())
                .ReturnsAsync(pbfResponse)
                .ReturnsAsync(new List<NewOperationResponseModel>());

            pBfClient.Setup(x => x.OperationsApi.AcceptBatchAsync(It.IsAny<Dictionary<Guid, string>>()))
                .ReturnsAsync(new OperationStatusUpdateResponseModel())
                .Verifiable();

            var executorClient = new Mock<IQuorumOperationExecutorClient>();

            executorClient.Setup(x =>
                    x.OperationsApi.ExecuteOperationsBatchAsync(It.Is<ExecuteOperationsBatchRequest>(r => r.MasterWalletAddress != "address1")))
                .ReturnsAsync(
                    new ExecuteOperationsBatchResponse
                    {
                        Error = ExecuteOperationError.None,
                        TxHashesDict = pbfResponse.Where(o => o.MasterWalletAddress != "address1").ToDictionary(i => i.Id, i => "hash")
                    });

            executorClient.SetupSequence(x =>
                    x.OperationsApi.ExecuteOperationsBatchAsync(It.Is<ExecuteOperationsBatchRequest>(r => r.MasterWalletAddress == "address1")))
                .ThrowsAsync(new Exception())
                .ReturnsAsync(new ExecuteOperationsBatchResponse { Error = ExecuteOperationError.MasterWalletNotFound });

            OperationOrchestratorService operationOrchestratorService;
            using (var logFactory = LogFactory.Create().AddUnbufferedConsole())
            {
                operationOrchestratorService = new OperationOrchestratorService(
                    maxDegreeOfParallelism,
                    maxAddressFailures,
                    cleanAddressesWithFailuresTimeSpan,
                    _acceptedOperationsWarningTimeout,
                    pBfClient.Object,
                    executorClient.Object,
                    logFactory);
            }

            await operationOrchestratorService.ProcessOperationsBatchAsync();

            pBfClient.Verify(x => x.OperationsApi.AcceptBatchAsync(It.Is<Dictionary<Guid, string>>(d => d.Values.Any(v => v != "hash"))), Times.Never);

            pBfClient.Verify(x => x.OperationsApi.AcceptBatchAsync(It.Is<Dictionary<Guid, string>>(d => d.Values.Any(v => v == "hash"))), Times.Exactly(2));
        }

        [Fact]
        public async Task TryToProcessBatchOfOperations_ErrorOnSomeAddressFromSeveralAttempts_OperationsForThisAddressAreNotGetForProcessing()
        {
            var maxDegreeOfParallelism = 5;
            var maxAddressFailures = 3;
            var cleanAddressesWithFailuresTimeSpan = TimeSpan.FromDays(1);
            var pBfClient = new Mock<IPrivateBlockchainFacadeClient>();

            var pbfResponse = GetOperationsData();

            pBfClient.SetupSequence(x => x.OperationsApi.GetNewOperationsAsync())
                .ReturnsAsync(pbfResponse)
                .ReturnsAsync(pbfResponse)
                .ReturnsAsync(pbfResponse)
                .ReturnsAsync(pbfResponse)
                .ReturnsAsync(new List<NewOperationResponseModel>());

            pBfClient.Setup(x => x.OperationsApi.AcceptAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(new OperationStatusUpdateResponseModel())
                .Verifiable();

            var executorClient = new Mock<IQuorumOperationExecutorClient>();

            executorClient.Setup(x =>
                    x.OperationsApi.ExecuteOperationAsync(It.IsAny<Guid>(), It.Is<ExecuteOperationRequest>(r => r.MasterWalletAddress != "address3")))
                .ReturnsAsync(new ExecuteOperationResponse { Error = ExecuteOperationError.None, TxHash = "hash" });

            executorClient.Setup(x =>
                    x.OperationsApi.ExecuteOperationAsync(It.IsAny<Guid>(), It.Is<ExecuteOperationRequest>(r => r.MasterWalletAddress == "address3")))
                .ReturnsAsync(new ExecuteOperationResponse { Error = ExecuteOperationError.MasterWalletNotFound, TxHash = "hash1" })
                .Verifiable();

            OperationOrchestratorService operationOrchestratorService;
            using (var logFactory = LogFactory.Create().AddUnbufferedConsole())
            {
                operationOrchestratorService = new OperationOrchestratorService(
                    maxDegreeOfParallelism,
                    maxAddressFailures,
                    cleanAddressesWithFailuresTimeSpan,
                    _acceptedOperationsWarningTimeout,
                    pBfClient.Object,
                    executorClient.Object,
                    logFactory);
            }

            await operationOrchestratorService.ProcessOperationsBatchAsync();

            executorClient.Verify(x => x.OperationsApi.ExecuteOperationAsync
                (It.IsAny<Guid>(), It.Is<ExecuteOperationRequest>(r => r.MasterWalletAddress == "address3")), Times.AtMost(maxAddressFailures));
        }


        private List<NewOperationResponseModel> GetOperationsData()
        {
            return new List<NewOperationResponseModel>
            {
                new NewOperationResponseModel
                {
                    Nonce = 1,
                    Id = Guid.NewGuid(),
                    MasterWalletAddress = "address1",
                    PayloadJson = "payload",
                    Type = "type"
                },
                new NewOperationResponseModel
                {
                    Nonce = 2,
                    Id = Guid.NewGuid(),
                    MasterWalletAddress = "address1",
                    PayloadJson = "payload",
                    Type = "type"
                },
                new NewOperationResponseModel
                {
                    Nonce = 1,
                    Id = Guid.NewGuid(),
                    MasterWalletAddress = "address2",
                    PayloadJson = "payload",
                    Type = "type"
                },
                new NewOperationResponseModel
                {
                    Nonce = 1,
                    Id = Guid.NewGuid(),
                    MasterWalletAddress = "address3",
                    PayloadJson = "payload",
                    Type = "type"
                },
                new NewOperationResponseModel
                {
                    Nonce = 2,
                    Id = Guid.NewGuid(),
                    MasterWalletAddress = "address3",
                    PayloadJson = "payload",
                    Type = "type"
                },
            };
        }
    }
}
