
using GPMVehicleControlSystem.Models;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace GPMVehicleControlSystem.Service
{
    public class SystemUpdateService
    {
        private readonly IHubContext<FrontendHub> hubContext;
        public SystemUpdateService(IHubContext<FrontendHub> hubContext)
        {
            this.hubContext = hubContext;
        }

        internal async Task<(bool confirm, string message)> SystemUpdateWithFileUpload(IFormFile file)
        {
            if (file.Length > 0)
            {

                try
                {
                    BackupCurrentProgram(out string backupMesg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"backup progrss exception:" + ex.Message);
                }

                try
                {
                    string currentDirectory = Directory.GetCurrentDirectory();
                    string zipFileTempFolder = Path.Combine(currentDirectory, "_temp");
                    try
                    {
                        Directory.Delete(zipFileTempFolder, true); //刪除現有的 _temp
                    }
                    catch (Exception)
                    {
                    }
                    Console.WriteLine($"Create temp folder : {zipFileTempFolder}");
                    Directory.CreateDirectory(zipFileTempFolder);
                    Directory.CreateDirectory(Path.Combine(zipFileTempFolder, "wwwroot"));

                    Console.WriteLine($"Create temp folder : {zipFileTempFolder} done");
                    //store zip file and unzip to current folder
                    // 1. store zip file

                    string zipFilePath = Path.Combine(Directory.GetCurrentDirectory(), file.FileName);
                    Console.WriteLine($"store zip file to : {zipFilePath} ");
                    using (FileStream stream = new FileStream(zipFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    Console.WriteLine($"store zip file to : {zipFilePath} done");
                    //要先提高權限 _temp資料夾
                    Tools.LinuxTools.RunShellCommand($"sudo chmod -R 777 {zipFileTempFolder}", out _, out _);
                    //2 unzip to current folder
                    Tools.LinuxTools.RunShellCommand($"unzip \"{zipFilePath}\" -d \"{zipFileTempFolder}\"", out _, out _);

                    //ZipFile.ExtractToDirectory(zipFilePath, zipFileTempFolder, true);
                    File.Delete(zipFilePath);
                    //// backup 
                    //BackupCurrentProgram(out string errMsg);

                    string scriptFile = Path.Combine(currentDirectory, "update.sh");

                    if (!File.Exists(scriptFile))
                        File.WriteAllText(scriptFile, $"sleep 5 && killall -9 GPM_VCS>/dev/null || true && sleep 2 &&  cp -r {zipFileTempFolder}/* {currentDirectory}/" +
                            $"&& cd {currentDirectory} && ./GPM_VCS");

                    Process.Start("chmod", $"777 {scriptFile}").WaitForExit();
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-c \"./update.sh\"",
                        WorkingDirectory = currentDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = false
                    };
                    // Start the process
                    Process process = Process.Start(startInfo);
                    return (true, "");
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            }
            else
            {
                return (false, "No File.");
            }
        }

        // create a function : backup current work directory files and folders (use command just like cvf)
        internal bool BackupCurrentProgram(out string ErrorMessage)
        {
            ErrorMessage = "";
            try
            {

                // 取得目前執行檔所在的目錄
                string currentDirectory = AppContext.BaseDirectory;
                string backupFolder = Path.Combine(currentDirectory, "backup");
                Directory.CreateDirectory(backupFolder);
                //嘗試取得版本號
                string version = StaStored.APPVersion;
                // 設定壓縮檔案的完整路徑（存放在同一目錄下）
                string zipPath = Path.Combine(backupFolder, $"backup_{version}.zip");

                // 若已有同名檔案，先刪除以避免例外
                if (File.Exists(zipPath))
                {
                    ErrorMessage = $"Version {version} 此版本已經有備份檔";
                    return true;
                }

                // 建立暫存資料夾來放要壓縮的檔案
                string tempDir = Path.Combine(Path.GetTempPath(), "vms_backup_temp");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);



                string[] excludesFileNames = new string[] { "vms.db" };
                string[] excludesFolderNames = new string[] { "backup", "uploads", "linux-x64", "updates" };

                CopyDirectory(currentDirectory, tempDir, excludesFileNames, excludesFolderNames);


                // 壓縮目前目錄下的所有內容到 backup.zip（不包含壓縮檔本身）
                ZipFile.CreateFromDirectory(
                    sourceDirectoryName: tempDir,
                    destinationArchiveFileName: zipPath,
                    compressionLevel: CompressionLevel.Optimal,
                    includeBaseDirectory: false

                );
                Directory.Delete(tempDir, true);
                ErrorMessage = $"✅ 備份完成：{zipPath}";
                Console.WriteLine(ErrorMessage);
                return true;

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
            }
        }
        public static void CopyDirectory(string sourceDir, string destinationDir, string[] excludesFileNames, string[] excludesFolderNames)
        {
            // 建立目標資料夾（若不存在）
            Directory.CreateDirectory(destinationDir);

            // 複製所有檔案
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                if (excludesFileNames.Contains(fileName.ToLower()))
                    continue;

                string targetFilePath = Path.Combine(destinationDir, fileName);
                File.Copy(file, targetFilePath, overwrite: true);
            }


            // 遞迴處理子資料夾
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                if (excludesFolderNames.Contains(dirName.ToLower()))
                    continue;
                string targetSubDir = Path.Combine(destinationDir, dirName);
                CopyDirectory(directory, targetSubDir, excludesFileNames, excludesFolderNames);
            }
        }
        // Create function : Copy current work directory files and folders to backup folder by OS 
        private void CopyFilesToBackupFolder(string currentDirectory, string backupFolder)
        {
            Console.WriteLine($"Create backup folder : {backupFolder}");
            Directory.CreateDirectory(backupFolder);
            string backupSubfolderName = Path.GetFileName(backupFolder);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string excludedFolders = $"\"backup\" \"{backupSubfolderName}\" \"src\\sounds\""; // 排除多个目录

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "robocopy",
                    Arguments = $"\"{currentDirectory}\" \"{backupFolder}\" /E /NP /NFL /NDL /NJH /NJS /NS /NC /XD {excludedFolders}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Start the process
                Process process = Process.Start(startInfo);
                process.WaitForExit();
            }
            else
            {
                // 要排除的目录
                string[] excludedDirectories = new string[]
                {
                    "backup",
                    backupSubfolderName,
                    "src/sounds"
                };

                // 构建 rsync 排除选项
                string excludeOptions = "";
                foreach (string dir in excludedDirectories)
                {
                    excludeOptions += $"--exclude=\"{dir}\" ";
                }
                string arguments = $"-av {excludeOptions} \"{currentDirectory}/\" \"{backupFolder}/\"";
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "rsync",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                // Start the process
                Process process = Process.Start(startInfo);
                process.WaitForExit();
            }
        }

        private void CompressBackupFolder(string currentDirectory, string tempBackupFolder, string compressFileStoreFolder)
        {
            Directory.CreateDirectory(compressFileStoreFolder);
            ProcessStartInfo processStartInfo;
            // create a local function  to get startInfo by OS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // modify above : finally compress the backup folder to a zip file (use command just like cvf)
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-zcvf \"{compressFileStoreFolder}/vcs_backup_{DateTime.Now.ToString("yyMMddHHmmss")}.tar.gz\" -C \"{tempBackupFolder}\" .",
                    WorkingDirectory = currentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
            }
            else
            {
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c tar -zcvf \"{compressFileStoreFolder}/vcs_backup_{DateTime.Now.ToString("yyMMddHHmmss")}.tar.gz\" -C \"{tempBackupFolder}\" .",
                    WorkingDirectory = currentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
            }

            Process process = Process.Start(processStartInfo);
            process.WaitForExit();
        }

        // create a function : remove spefic folder 
        private void RemoveFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }
        }

        internal async Task BrocastRestartSystemCountDownNotify(string? reason = "", int duration = 5)
        {
            _ = Task.Run(async () =>
            {
                hubContext?.Clients.All.SendAsync($"AGV-Notify-Message", new { title = $"系統重啟中{(string.IsNullOrEmpty(reason) ? "" : $"({reason})")}", message = $"System will restart after {duration} second.", alarmCode = 3384 });
                await Task.Delay(1000);
            });
        }
    }
}
