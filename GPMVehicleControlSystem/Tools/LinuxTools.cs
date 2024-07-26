using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Tools.CPUUsage;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
                CPUUaageBase cpuUsage = _GetCPUUsageInstance();

                while (true)
                {
                    double cpu = await cpuUsage.GetCPU();
                    Memory = GetMemUsedMB();
                    LOG.TRACE($"[Sys-Loading] CPU:{cpu}, Memory:{Memory}Mb", show_console: false);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            });
        }
        private static CPUUaageBase _GetCPUUsageInstance()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxCPUUsage();
            else
                return new CPUUaageBase();
        }
        public static double GetMemUsedMB()
        {
            var currentProcess = Process.GetCurrentProcess();
            return currentProcess.WorkingSet64 / 1024 / 1024;
        }
    }

}
