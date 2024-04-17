using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class OtaUpdater
{
    private readonly string _downloadUrl;
    private readonly string _completionNotificationUrl;
    private readonly string _downloadFolderPath;
    private readonly string _extractionFolderPath;
    private readonly string _applicationToLaunch;

    public OtaUpdater(string downloadUrl, string completionNotificationUrl, string downloadFolderPath, string extractionFolderPath, string applicationToLaunch)
    {
        _downloadUrl = downloadUrl;
        _completionNotificationUrl = completionNotificationUrl;
        _downloadFolderPath = downloadFolderPath;
        _extractionFolderPath = extractionFolderPath;
        _applicationToLaunch = applicationToLaunch;
    }

    public async Task<(bool confirm, string message)> UpdateApplicationAsync()
    {
        string zipFilePath = null;
        try
        {
            zipFilePath = await DownloadUpdatePackageAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Download Update Package Fail-{ex.Message}");
        }//await SendVCSAppShutdownRequest();
        _ = Task.Run(async () =>
        {
            await WaitVCSClosed();
            //BackupFiles();
            ExtractAndReplaceFiles(zipFilePath);
            LaunchApplication();
        });
        return (true, "Update File Package download, Start Update Process.");

    }


    private async Task<bool> WaitVCSClosed()
    {
        Process[] processes = Process.GetProcessesByName("GPM_VCS");
        bool vcsClosed = processes.Length == 0;
        if (!vcsClosed)
        {
            await SendVCSAppShutdownRequest();
            Console.WriteLine("Wait VCS Closed...");
            await Task.Delay(1000);
            await WaitVCSClosed();
        }
        return true;
    }

    private async Task<string> DownloadUpdatePackageAsync()
    {
        using (var httpClient = new HttpClient())
        {
            Directory.CreateDirectory(_downloadFolderPath);
            string zipFilePath = Path.Combine(_downloadFolderPath, $"update-{DateTime.Now:yy-MM-dd-HH-mm-ss}.zip");

            using (var response = await httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseContentRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"下载失败: {response.StatusCode}");
                    throw new Exception($"下载失败: {response.StatusCode}");
                }

                using (Stream stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(zipFilePath, FileMode.CreateNew))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }

            return zipFilePath;
        }
    }

    private async Task SendVCSAppShutdownRequest()
    {
        try
        {
            var content = new StringContent("");
            var httpClient = new HttpClient();
            await httpClient.PostAsync(_completionNotificationUrl, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private void BackupFiles()
    {
        try
        {
            string backupFilePath = Path.Combine(_extractionFolderPath, $"VCSBackup-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.zip");
            Console.WriteLine("備份車載程式程序開始..compress destine ->" + backupFilePath);
            ZipFile.CreateFromDirectory(_extractionFolderPath, backupFilePath, CompressionLevel.SmallestSize, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"備份失敗!-{ex.Message}");
        }
    }
    private void ExtractAndReplaceFiles(string zipFilePath)
    {
        Console.WriteLine($"uncompress-{zipFilePath} to {_extractionFolderPath}");
        ZipFile.ExtractToDirectory(zipFilePath, _extractionFolderPath, true);
    }

    private void LaunchApplication()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _applicationToLaunch,
            WorkingDirectory = Path.GetDirectoryName(_applicationToLaunch),
            UseShellExecute = false,
        });
    }
}
