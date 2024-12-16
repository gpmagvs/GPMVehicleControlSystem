
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

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
                    //store zip file and unzip to current folder
                    // 1. store zip file
                    string zipFilePath = Path.Combine(Directory.GetCurrentDirectory(), file.FileName);
                    using (FileStream stream = new FileStream(zipFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    //要先提高權限 _temp資料夾
                    Tools.LinuxTools.RunShellCommand($"sudo chmod -R 777 {zipFileTempFolder}", out _, out _);
                    //2 unzip to current folder
                    Tools.LinuxTools.RunShellCommand($"unzip \"{zipFilePath}\" -d \"{zipFileTempFolder}\"", out _, out _);

                    //ZipFile.ExtractToDirectory(zipFilePath, zipFileTempFolder, true);
                    File.Delete(zipFilePath);
                    //// backup 
                    //BackupCurrentProgram(out string errMsg);

                    string scriptFile = Path.Combine(currentDirectory, "update.sh");
                    File.WriteAllText(scriptFile, $"sleep 5 && killall -9 GPM_VCS 2>/dev/null || true && sleep 2 &&  cp -r {zipFileTempFolder}/* {currentDirectory}/" +
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
                string currentDirectory = Directory.GetCurrentDirectory();
                string backupFolder = Path.Combine(currentDirectory, "backup");
                string tempBackupFolder = Path.Combine(currentDirectory, "_tempBackup");

                // copy files to backup folder first 
                CopyFilesToBackupFolder(currentDirectory, tempBackupFolder);
                // create a local function  to get startInfo by OS
                CompressBackupFolder(currentDirectory, tempBackupFolder, backupFolder);

                RemoveFolder(tempBackupFolder);

                return true;

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
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
                int countDown = duration;
                while (true)
                {
                    hubContext?.Clients.All.SendAsync($"AGV-Notify-Message", new { title = $"系統重啟中{(string.IsNullOrEmpty(reason) ? "" : $"({reason})")}", message = $"System will restart after {countDown} second.", alarmCode = 3384 });
                    await Task.Delay(1000);
                    countDown--;
                    if (countDown == 0)
                    {
                        break;
                    }

                }
            });
        }
    }
}
