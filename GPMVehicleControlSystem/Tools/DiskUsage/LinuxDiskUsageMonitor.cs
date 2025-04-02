
using AGVSystemCommonNet6;
using System.Collections.Generic;

namespace GPMVehicleControlSystem.Tools.DiskUsage
{
    public class LinuxDiskUsageMonitor : IDiskUsageMonitor
    {

        public override List<DiskUsageState> GetDiskUsageStates()
        {
            List<DiskUsageState> diskUsageStates = new List<DiskUsageState>();

            IEnumerable<DriveInfo> drivers = DriveInfo.GetDrives().Where(dri => dri.DriveType == DriveType.Fixed && dri.TotalSize != 0);

            foreach (DriveInfo drive in drivers)
            {
                if (drive.IsReady && drive.TotalSize != 0)
                {
                    DiskUsageState diskUsageState = new DiskUsageState();
                    diskUsageState.Name = drive.Name;
                    diskUsageState.TotalSizeOfDriver = Math.Round(drive.TotalSize / (1024.0 * 1024.0), 2);
                    diskUsageState.TotalAvailableSpace = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024.0), 2);
                    diskUsageState.DriverType = drive.DriveType.ToString();
                    diskUsageStates.Add(diskUsageState);
                }
            }

            return diskUsageStates;
        }
        public override async Task DeleteOldVCSLogData(DateTime createdTimeLessThan)
        {
            string logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "GPM_AGV_LOG");
            _logger.Trace($"Delete Old Log Data=>Files in {logFolder} where file created time less than {createdTimeLessThan}");
            await DeleteOldData(logFolder, createdTimeLessThan);
        }

    }
}
