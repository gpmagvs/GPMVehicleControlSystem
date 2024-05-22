using AGVSystemCommonNet6.Log;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Tools
{
    public class LinuxTools
    {
        internal static double Memory = 0;
        public static void FindTerminals()
        {
            string command = "ps aux | grep -E 'bash|sh'";

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    RedirectStandardOutput = true,
                    Arguments = $"-c \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Console.WriteLine(output);
        }
        public static void SaveCurrentProcessPID()
        {
            var currentProcess = Process.GetCurrentProcess();
            int pid = currentProcess.Id;
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "VCS_PID"), pid.ToString());
        }

        public static async Task SysLoadingLogProcess()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                Memory = GetMemUsedMB();
                LOG.TRACE($"[Sys-Loading] CPU:-1, Memory:{Memory}Mb", show_console: true);
                if (Memory > 500)
                {
                    GC.Collect();
                }
            }
        }

        public static double GetMemUsedMB()
        {
            var currentProcess = Process.GetCurrentProcess();
            return currentProcess.WorkingSet64 / 1024 / 1024;
        }
    }

}
