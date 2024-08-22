using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.Tools.CPUUsage;
using GPMVehicleControlSystem.Tools.NetworkStatus;
using System.Runtime.InteropServices;

namespace GPMVehicleControlSystem.Service
{
    public class SystemLoadingMonitorBackgroundServeice : BackgroundService
    {
        ILogger<SystemLoadingMonitorBackgroundServeice> logger;
        public SystemLoadingMonitorBackgroundServeice(ILogger<SystemLoadingMonitorBackgroundServeice> logger)
        {

            this.logger = logger;
        }

        public static double CurrentCPU = 0;
        public static double CurrentRAM = 0;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            CPUUaageBase cpuUsage = _GetCPUUsageInstance();
            NetworkStatusBase networkStatus = _GetNetworkStatusInstance();

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(200);
                    (double net_trasmited, double net_recieved) = await networkStatus.GetNetworkStatus();
                    logger.LogInformation($"Network-Tranmit:{net_trasmited} MB/s / Network-Recieve:{net_recieved} MB/s");

                }
            });
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000);
                double cpu = await cpuUsage.GetCPU();
                double ram = LinuxTools.GetMemUsedMB();
                logger.LogInformation($"CPU:{cpu} % / RAM:{ram} MB");

                //if (ram > 500)
                //{
                //    GC.Collect();
                //    LOG.WARN($"RAM:{ram} MB > 500MB , Run GC.Collect()");
                //}
                CurrentCPU = Math.Round(cpu, 1);
                CurrentRAM = ram;
            }
        }


        private CPUUaageBase _GetCPUUsageInstance()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxCPUUsage();
            else
                return new CPUUaageBase();
        }

        private NetworkStatusBase _GetNetworkStatusInstance()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxNetworkStatus();
            else
                return new NetworkStatusBase();
        }
    }
}
