using NLog;
using System.Diagnostics;
using System.Text;

namespace GPMVehicleControlSystem.Tools
{
    public class LinuxTools
    {
        public enum ROS_VERSION
        {
            ROS1, ROS2
        }

        internal static double Memory = 0;

        static Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<ROS_VERSION> GetRosVersionAsync()
        {
            (string output, string error) result = ("", "");
            try
            {
                result = await RunShellCommandAsync("printenv | grep ROS");
            }
            catch (Exception)
            {
                return ROS_VERSION.ROS1; // Default to ROS1 if any error occurs
            }
            Logger.Info($"printenv | grep ROS::RESULT = {result.output}");
            if (string.IsNullOrEmpty(result.output))
            {
                Logger.Error($"Get ROS Version Error: {result.error}");
                return ROS_VERSION.ROS1; // Default to ROS1 if no environment variable found
            }

            if (result.output.Contains("ROS_VERSION=1"))
            {
                Logger.Info("Detected ROS Version: ROS1");
                return ROS_VERSION.ROS1;
            }
            return ROS_VERSION.ROS2;
        }

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
                Logger.Trace($"[Sys-Loading] CPU:-1, Memory:{Memory}Mb");
                //if (Memory > 500)
                //{
                //    GC.Collect();
                //}
            }
        }

        public static double GetMemUsedMB()
        {
            var currentProcess = Process.GetCurrentProcess();
            return currentProcess.WorkingSet64 / 1024 / 1024;
        }



        /// <summary>
        /// 創建 Process 來執行 shell 命令
        /// </summary>
        /// <param name="command"></param>
        public static async Task<(string output, string error)> RunShellCommandAsync(string command)
        {
            try
            {

                var output = new StringBuilder();
                var error = new StringBuilder();

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                var tcs = new TaskCompletionSource<int>();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };
                process.Exited += (s, e) => tcs.SetResult(process.ExitCode);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await tcs.Task; // 等待非同步完成
                return (output.ToString(), error.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return ("", "");
            }
        }
    }

}
