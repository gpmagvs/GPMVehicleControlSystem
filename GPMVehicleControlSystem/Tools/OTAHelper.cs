using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPMVehicleControlSystem.Tools
{
    public class OTAHelper
    {
        internal static async Task TryStartOTAServiceAPP()
        {
            string filename = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "OTAUpdateService.exe" : "OTAUpdateService";
            string appFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"VCSOTA/{filename}");
            bool isFileExist = File.Exists(appFilePath);
            if (!isFileExist)
            {
                Console.WriteLine($"OTA Service app file not exist....");
                return;
            }
            if (_IsOTAServiceOpened(filename))
            {
                KillOTAProcess(filename);
            }

            Console.WriteLine($"Try Start OTA Service app process..");
            Process.Start(new ProcessStartInfo
            {
                FileName = appFilePath,
                WorkingDirectory = Path.GetDirectoryName(appFilePath),
                UseShellExecute = false,
            });
            await Task.Delay(5000);
            if (_IsOTAServiceOpened(filename))
            {
                Console.WriteLine($"OTA Service Start SUCCESS!");
            }
            else
            {

                Console.WriteLine($"OTA Service Start FAIL...!");
            }
        }

        private static void KillOTAProcess(string filename)
        {
            try
            {
                var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(filename));
                foreach (var item in procs)
                {
                    item.Kill();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static bool _IsOTAServiceOpened(string appName)
        {
            try
            {
                var procs2 = Process.GetProcessesByName(appName);
                var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(appName));
                return procs != null && procs.Length != 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return false;
            }
        }
    }
}
