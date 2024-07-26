using AGVSystemCommonNet6.Log;

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
                LOG.ERROR("Get CPU Loading Fail(Linux Base) :" + ex.Message);
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
    }
}
