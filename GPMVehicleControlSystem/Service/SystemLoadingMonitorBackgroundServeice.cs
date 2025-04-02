using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.Tools.CPUUsage;
using GPMVehicleControlSystem.Tools.DiskUsage;
using GPMVehicleControlSystem.Tools.NetworkStatus;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NLog;
using System.Runtime.InteropServices;

namespace GPMVehicleControlSystem.Service
{
    public class SystemLoadingMonitorBackgroundServeice : BackgroundService
    {
        Logger logger;
        IHubContext<FrontendHub> _hubContext;
        public SystemLoadingMonitorBackgroundServeice(IHubContext<FrontendHub> hubContext)
        {

            logger = LogManager.GetLogger("SystemLoadingMonitor");
            _hubContext = hubContext;
        }

        public static double CurrentCPU = 0;
        public static double CurrentRAM = 0;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            CPUUaageBase cpuUsage = _GetCPUUsageInstance();
            //StartNetworkStatusMonitor();
            StartDiskStatusMonitorAsync();


            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000);
                double cpu = await cpuUsage.GetCPU();
                string top10Output = await cpuUsage.GetTop10CupUseProcess();
                double ram = LinuxTools.GetMemUsedMB();
                logger.Info($"CPU:{cpu} % / RAM:{ram} MB");
                logger.Info(top10Output);
                //if (ram > 500)
                //{
                //    GC.Collect();
                //    LOG.WARN($"RAM:{ram} MB > 500MB , Run GC.Collect()");
                //}
                CurrentCPU = Math.Round(cpu, 1);
                CurrentRAM = ram;
            }
        }

        private async Task StartNetworkStatusMonitor()
        {
            NetworkStatusBase networkStatus = _GetNetworkStatusInstance();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(5000);
                    (double net_trasmited, double net_recieved) = await networkStatus.GetNetworkStatus();
                    logger.Info($"Network-Tranmit:{net_trasmited} MB/s / Network-Recieve:{net_recieved} MB/s");

                }
            });
        }

        private async Task StartDiskStatusMonitorAsync()
        {
            DiskMonitorParams? param = LoadDiskMonitorParam() ?? new DiskMonitorParams();
            IDiskUsageMonitor diskMonitor = _GetDiskUsageMonitorInstance();
            await Task.Delay(TimeSpan.FromSeconds(10));
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    List<DiskUsageState> disksStates = diskMonitor.GetDiskUsageStates();
                    string[] homeDiskNames = new[] { "c:", "home" };
                    DiskUsageState homeDiskUsage = disksStates.FirstOrDefault(s => homeDiskNames.Any(n => s.Name.ToLower().Contains(n)));
                    logger.Info("Check Disk Status : " + homeDiskUsage?.ToJson(Newtonsoft.Json.Formatting.None));
                    if (homeDiskUsage != null && homeDiskUsage.TotalAvailableSpace < param.HardDiskSpaceWarningSize)
                    {
                        _hubContext.Clients.All.SendAsync("DiskUsageError", $"{homeDiskUsage.Name} 磁碟容量即將不足!剩餘:{homeDiskUsage.TotalAvailableSpace} Mb");
                        AlarmManager.AddWarning(AlarmCodes.Hard_Disk_Space_Is_Full);
                    }
                    await Task.Delay(TimeSpan.FromMinutes(param.HardDiskSpaceCheckTimer));
                }
            });
        }

        private DiskMonitorParams? LoadDiskMonitorParam()
        {
            string jsonFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/DiskMonitorParam.json");
            if (File.Exists(jsonFilePath))
            {
                try
                {
                    var existparams = JsonConvert.DeserializeObject<DiskMonitorParams>(File.ReadAllText(jsonFilePath));
                    _RollbackJsonFile(existparams);
                    return existparams;
                }
                catch (Exception)
                {
                    return new DiskMonitorParams();
                }
            }
            else
            {
                var defaultParams = new DiskMonitorParams();
                _RollbackJsonFile(defaultParams);
                return defaultParams;
            }

            void _RollbackJsonFile(DiskMonitorParams _defaultParam)
            {
                if (_defaultParam == null)
                    return;
                File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(_defaultParam, Formatting.Indented));
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

        private IDiskUsageMonitor _GetDiskUsageMonitorInstance()
        {
            return new LinuxDiskUsageMonitor();
        }
    }
}

public class DiskMonitorParams
{
    public double HardDiskSpaceWarningSize { get; set; } = 10240;
    public double HardDiskSpaceCheckTimer { get; set; } = 60;
}