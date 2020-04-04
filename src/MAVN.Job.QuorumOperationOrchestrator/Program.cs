using System.Threading.Tasks;
using Lykke.Sdk;

namespace MAVN.Job.QuorumOperationOrchestrator
{
    internal sealed class Program
    {
        public static async Task Main(string[] args)
        {
#if DEBUG
            await LykkeStarter.Start<Startup>(true);
#else
            await LykkeStarter.Start<Startup>(false);
#endif
        }
    }
}
