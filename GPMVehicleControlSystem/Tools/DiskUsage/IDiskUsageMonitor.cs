using NLog;

namespace GPMVehicleControlSystem.Tools.DiskUsage
{
    public abstract class IDiskUsageMonitor
    {
        protected Logger _logger = LogManager.GetCurrentClassLogger();
        public abstract List<DiskUsageState> GetDiskUsageStates();

        public abstract Task DeleteOldVCSLogData(DateTime createdTimeLessThan);

        public async Task DeleteOldData(string folderPath, DateTime createdTimeLessThan)
        {
            try
            {
                _logger.Trace($"開始刪除舊LOG檔案-->(創建時間早於 {createdTimeLessThan} 的所有檔案與子目錄)");
                // 遞歸刪除文件和空目錄
                DeleteFilesAndDirectories(new DirectoryInfo(folderPath), createdTimeLessThan);
                _logger.Trace($"刪除創建時間早於 {createdTimeLessThan} 的所有檔案與子目錄已完成。");
            }
            catch (Exception ex)
            {
                _logger.Trace($"刪除過程中發生錯誤: {ex.Message}");
            }
        }


        private void DeleteFilesAndDirectories(DirectoryInfo directory, DateTime createdTimeLessThan)
        {
            // 刪除舊文件
            foreach (var file in directory.GetFiles())
            {
                if (file.CreationTime < createdTimeLessThan)
                {
                    file.Delete();
                    _logger.Trace($"已刪除文件: {file.FullName}");
                }
            }

            // 遞歸處理子目錄
            foreach (var subDirectory in directory.GetDirectories())
            {
                DeleteFilesAndDirectories(subDirectory, createdTimeLessThan);

                // 檢查子目錄是否為空，然後刪除
                if (subDirectory.GetFiles().Length == 0 && subDirectory.GetDirectories().Length == 0)
                {
                    subDirectory.Delete();
                    _logger.Trace($"已刪除空目錄: {subDirectory.FullName}");
                }
            }
        }
    }
}
