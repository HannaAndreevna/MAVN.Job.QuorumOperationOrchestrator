using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Common.Log;
using MAVN.Job.QuorumOperationOrchestrator.Domain;
using MAVN.Job.QuorumOperationOrchestrator.Domain.Models;
using MAVN.Job.QuorumOperationOrchestrator.Domain.Services;
using MAVN.Service.PrivateBlockchainFacade.Client;
using MAVN.Service.QuorumOperationExecutor.Client;
using MAVN.Service.QuorumOperationExecutor.Client.Models.Requests;
using MAVN.Service.QuorumOperationExecutor.Client.Models.Responses;

namespace MAVN.Job.QuorumOperationOrchestrator.DomainServices
{
    public class OperationOrchestratorService : IOperationOrchestratorService
    {
        private readonly int _maxDegreeOfParallelism;
        private readonly int _maxFailuresPerAddress;
        private readonly TimeSpan _cleanAddressesWithFailuresTimeSpan;
        private readonly TimeSpan _acceptedOperationsWarningTimeout;
        private readonly IPrivateBlockchainFacadeClient _privateBlockchainFacadeClient;
        private readonly IQuorumOperationExecutorClient _operationExecutorClient;
        private readonly ILog _log;
        private readonly Dictionary<string, ProcessingFailureInfo> _addressesWithFailures;

        public OperationOrchestratorService(
            int maxDegreeOfParallelism,
            int maxFailuresPerAddress,
            TimeSpan cleanAddressesWithFailuresTimeSpan,
            TimeSpan acceptedOperationsWarningTimeout,
            IPrivateBlockchainFacadeClient privateBlockchainFacadeClient,
            IQuorumOperationExecutorClient operationExecutorClient,
            ILogFactory logFactory)
        {
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _privateBlockchainFacadeClient = privateBlockchainFacadeClient;
            _operationExecutorClient = operationExecutorClient;
            _maxFailuresPerAddress = maxFailuresPerAddress;
            _cleanAddressesWithFailuresTimeSpan = cleanAddressesWithFailuresTimeSpan;
            _acceptedOperationsWarningTimeout = acceptedOperationsWarningTimeout;
            _log = logFactory.CreateLog(this);
            _addressesWithFailures = new Dictionary<string, ProcessingFailureInfo>();
        }

        public async Task ProcessOperationsBatchAsync()
        {
            while (true)
            {
                CleanAddressesWithFailures();

                var operations = await GetOperationsAsync();

                if (operations.Count == 0)
                    break;

                var addressGroups = operations
                    .OrderBy(o => o.Nonce)
                    .GroupBy(o => o.MasterWalletAddress)
                    .Take(_maxDegreeOfParallelism);

                var operationsTasks = addressGroups.Select(SetOperationsStatus);

                var tasks = Task.WhenAll(operationsTasks);
                try
                {
                    await tasks;
                }
                catch (OperationCannotBeProcessedException)
                {
                    //This is done like this cause WhenAll does not throw aggregate exception 
                    //https://stackoverflow.com/questions/12007781/why-doesnt-await-on-task-whenall-throw-an-aggregateexception
                    tasks.Exception.Handle(ex =>
                    {
                        if (ex is OperationCannotBeProcessedException exception)
                        {
                            //If processing of an operation fails we need to remove all other operations
                            //for the same address cause they should be processed one after another
                            RegisterAddressFailure(exception.WalletAddress);

                            return true;
                        }

                        return false;
                    });
                }
            }
        }

        public async Task ProcessHangedOperationsAsync()
        {
            var acceptedOperations = await _privateBlockchainFacadeClient.OperationsApi.GetAcceptedOperationsAsync();

            foreach (var operation in acceptedOperations)
            {
                if (DateTime.UtcNow - operation.Timestamp > _acceptedOperationsWarningTimeout)
                    _log.Warning("Detected hanged accepted operation", context: operation);
            }
        }

        private void RegisterAddressFailure(string walletAddress)
        {
            if (!_addressesWithFailures.ContainsKey(walletAddress))
            {
                _addressesWithFailures.Add(walletAddress, new ProcessingFailureInfo());
            }

            _addressesWithFailures[walletAddress].FailureCount++;
            _addressesWithFailures[walletAddress].TimestampOfLastFailure = DateTime.UtcNow;
        }

        private void CleanAddressesWithFailures()
        {
            var dateBeforeWhichAddressesShouldBeRemoved = DateTime.UtcNow.Add(-_cleanAddressesWithFailuresTimeSpan);
            var addressesToRemove = _addressesWithFailures
                .Where(x => x.Value.TimestampOfLastFailure < dateBeforeWhichAddressesShouldBeRemoved)
                .Select(x => x.Key)
                .ToArray();

            foreach (var address in addressesToRemove)
            {
                _addressesWithFailures.Remove(address);
            }
        }

        private async Task<List<Operation>> GetOperationsAsync()
        {
            var result = (await _privateBlockchainFacadeClient.OperationsApi.GetNewOperationsAsync())
                .Where(x => !_addressesWithFailures.ContainsKey(x.MasterWalletAddress)
                    || _addressesWithFailures[x.MasterWalletAddress].FailureCount < _maxFailuresPerAddress)
                .Select(x => new Operation
                {
                    Nonce = x.Nonce,
                    Id = x.Id,
                    MasterWalletAddress = x.MasterWalletAddress,
                    PayloadJson = x.PayloadJson,
                    Type = x.Type
                })
                .ToList();

            if (result.Count > 0)
                _log.Info("Got new operations for precessing from PBF",
                    context: new {operationIds = string.Join(',', result.Select(x => x.Id.ToString()))});

            return result;
        }

        private async Task SetOperationsStatus(IEnumerable<Operation> operations)
        {
            var operationIds = operations.Select(i => i.Id).ToList();
            var masterWalletAddress = operations.First().MasterWalletAddress;

            try
            {
                var operationExecutionResult = await _operationExecutorClient.OperationsApi.ExecuteOperationsBatchAsync(
                    new ExecuteOperationsBatchRequest
                    {
                        MasterWalletAddress = masterWalletAddress,
                        Operations = operations.Select(i => 
                            new OperationData
                            {
                                OperationId = i.Id,
                                Nonce = i.Nonce,
                                Type = i.Type,
                                PayloadJson = i.PayloadJson,
                            })
                            .ToList()
                    });

                _log.Info("Operations have been sent to executor", new { operationIds });

                if (operationExecutionResult.Error != ExecuteOperationError.None)
                {
                    _log.Warning($"Execution of operation failed with error: {operationExecutionResult.Error.ToString()}.",
                        context: new { operationIds });

                    throw new OperationCannotBeProcessedException(masterWalletAddress);
                }

                await _privateBlockchainFacadeClient.OperationsApi.AcceptBatchAsync(operationExecutionResult.TxHashesDict);

                _log.Info("Operations status have been updated to Accepted", new { operationIds });
            }
            catch (Exception e)
            {
                if (e is OperationCannotBeProcessedException)
                    throw;

                _log.Warning("Error during operations processing.", e, new { operationIds });

                throw new OperationCannotBeProcessedException(masterWalletAddress);
            }
        }
    }

}
