using AGVSystemCommonNet6.Log;
using GitVersion.Extensions;
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

        public static void SysLoadingLogProcess()
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    Memory = GetMemUsedMB();
                    LOG.TRACE($"[Sys-Loading] CPU: Memory:{Memory}Mb");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            });
        }

        public static double GetMemUsedMB()
        {
            var currentProcess = Process.GetCurrentProcess();
            return currentProcess.WorkingSet64 / 1024 / 1024;
        }
    }

}
