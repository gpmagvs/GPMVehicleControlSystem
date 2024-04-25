using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPMVehicleControlSystem.Tools
{
    public static class PCShutDownHelper
    {
        public static void Shutdown()
        {
            try
            {
                // Create a new process to run the shutdown command
                ProcessStartInfo procStartInfo = GetProcessStartInfoByPlatform();
                // Redirect the output so it doesn't display to the console
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;

                // Start the process
                Process proc = new Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                // Read the output (if any)
                string result = proc.StandardOutput.ReadToEnd();
                Console.WriteLine(result);

                // Wait for process to finish
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private static ProcessStartInfo GetProcessStartInfoByPlatform()
        {

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new ProcessStartInfo("bash", "-c \"sudo shutdown -h now\"");
            }
            else
                return new ProcessStartInfo("cmd.exe", "/c shutdown /s /f /t 0");
        }
    }
}
