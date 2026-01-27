using GPMVehicleControlSystem.Models.Buzzer;
using NLog;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPMVehicleControlSystem.Tools
{
    public static class PCShutDownHelper
    {
        internal static bool IsInPCShutdownProcess { get; private set; } = false;
        internal static bool CancelPCShutdownFlag { get; set; } = false;

        internal static int ShutdownDelayTimeSec = 3;

        static NLog.Logger logger => LogManager.GetCurrentClassLogger();

        public static async Task<bool> ShutdownAsync()
        {
            IsInPCShutdownProcess = true;
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

                logger.Warn($"PC Will Shutdown after {ShutdownDelayTimeSec} sec...");
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.Elapsed.Seconds < ShutdownDelayTimeSec)
                {
                    if (CancelPCShutdownFlag)
                    {
                        logger.Trace($"User cancel PC Shutdwon when shutdown countdown...");
                        return false;
                    }
                    await Task.Delay(1000);
                }
                _ = Task.Run(async () =>
                {
                    BuzzerPlayer.APLAYER.PlayAudio("/home/gpm/param/sounds/shutdown.wav", out _);
                    await Task.Delay(1000);
                    proc.Start();
                    // Read the output (if any)
                    string result = proc.StandardOutput.ReadToEnd();
                    Console.WriteLine(result);
                    // Wait for process to finish
                    proc.WaitForExit();
                });

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return false;
            }
            finally
            {
                CancelPCShutdownFlag = IsInPCShutdownProcess = false;
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
