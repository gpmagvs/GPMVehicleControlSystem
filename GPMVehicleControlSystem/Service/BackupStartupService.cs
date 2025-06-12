using System.Threading;
using System.Threading.Tasks;

namespace GPMVehicleControlSystem.Service
{
    public class BackupStartupService : IHostedService
    {
        SystemUpdateService updateService;
        public BackupStartupService(SystemUpdateService updateService)
        {
            // Constructor logic if needed
            this.updateService = updateService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Factory.StartNew(() =>
            {
                updateService.BackupCurrentProgram(out string errMsg);
            });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
