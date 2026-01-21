using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Security.Cryptography;

namespace GPMVehicleControlSystem.Tools.CPUUsage
{
    public class LinuxCPUUsage : CPUUaageBase
    {
        private long _prevIdleTime;
        private long _prevTotalTime;
        public override async Task<double> GetCPU()
        {
            try
            {

                var cpuTimes = await ReadCpuTimesAsync();
                var idleTime = cpuTimes[3];
                var totalTime = cpuTimes.Sum();

                var idleDelta = idleTime - _prevIdleTime;
                var totalDelta = totalTime - _prevTotalTime;

                _prevIdleTime = idleTime;
                _prevTotalTime = totalTime;
                return (1.0 - (double)idleDelta / totalDelta) * 100.0;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        private async Task<long[]> ReadCpuTimesAsync()
        {
            using (var reader = new StreamReader("/proc/stat"))
            {
                var line = await reader.ReadLineAsync();
                var cpuTimes = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                   .Skip(1)
                                   .Select(long.Parse)
                                   .ToArray();
                return cpuTimes;
            }
        }

        public override async Task<string> GetTop10CupUseProcess()
        {
            (string output, string errmsg) = await LinuxTools.RunShellCommandAsync("ps -eo pid,comm,%mem,%cpu --sort=-%cpu | head -n 11 | awk 'NR==1 {print} NR>1 {print $1\",\"$2\",\"$3\",\"$4}'");
            return output;
        }
    }
}
