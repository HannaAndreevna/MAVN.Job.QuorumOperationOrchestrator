using Lykke.SettingsReader.Attributes;

namespace MAVN.Job.QuorumOperationOrchestrator.Settings.JobSettings
{
    public class DbSettings
    {
        [AzureTableCheck]
        public string LogsConnString { get; set; }
    }
}
