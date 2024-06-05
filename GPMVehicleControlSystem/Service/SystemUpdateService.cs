
using AGVSystemCommonNet6.Log;
using System.Diagnostics;
using System.IO.Compression;

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
                    File.WriteAllText(Path.Combine(currentDirectory, "update.sh"), $"sleep 3 && killall -9 GPM_VCS&&cp -r {zipFileTempFolder}/* {currentDirectory}/" +
                        $"&& cd {currentDirectory};./GPM_VCS;exec bash");

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
    }
}
