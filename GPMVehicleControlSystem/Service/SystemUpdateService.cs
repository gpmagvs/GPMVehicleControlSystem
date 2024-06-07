
using AGVSystemCommonNet6.Log;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace GPMVehicleControlSystem.Service
{
    public class SystemUpdateService
    {

        internal async Task<(bool confirm, string message)> SystemUpdateWithFileUpload(IFormFile file)
        {
            if (file.Length > 0)
            {
                try
                {
                    string currentDirectory = Directory.GetCurrentDirectory();
                    string zipFileTempFolder = Path.Combine(currentDirectory, "_temp");
                    Console.WriteLine($"Create temp folder : {zipFileTempFolder}");
                    Directory.CreateDirectory(zipFileTempFolder);

                    //store zip file and unzip to current folder
                    // 1. store zip file
                    var filePath = Path.Combine(zipFileTempFolder, file.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    //2 unzip to current folder
                    ZipFile.ExtractToDirectory(filePath, zipFileTempFolder, true);
                    File.Delete(filePath);

                    //// backup 
                    //BackupCurrentProgram(out string errMsg);

                    string scriptFile = Path.Combine(currentDirectory, "update.sh");
                    File.WriteAllText(scriptFile, $"sleep 1 && killall -9 GPM_VCS 2>/dev/null || true && sleep 2 &&  cp -r {zipFileTempFolder}/* {currentDirectory}/" +
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
    }
}
