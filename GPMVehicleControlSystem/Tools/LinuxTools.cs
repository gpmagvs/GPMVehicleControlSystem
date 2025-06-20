using NLog;
using System.Diagnostics;

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

        public static ROS_VERSION GetRosVersion()
        {
            string errorMsg;
            string ouput;
            try
            {
                RunShellCommand("printenv | grep ROS", out ouput, out errorMsg);
            }
            catch (Exception)
            {
                return ROS_VERSION.ROS1; // Default to ROS1 if any error occurs
            }

            if (string.IsNullOrEmpty(ouput))
            {
                Logger.Error($"Get ROS Version Error: {errorMsg}");
                return ROS_VERSION.ROS1; // Default to ROS1 if no environment variable found
            }

            if (ouput.Contains("ROS_VERSION=1"))
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
        public static void RunShellCommand(string command, out string output, out string error)
        {
            output = "";
            error = "";
            Logger.Info($"Run Shell Command-> {command}");
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash", // 使用 Bash 作為 shell
                    Arguments = $"-c \"{command}\"", // 使用 -c 執行命令
                    RedirectStandardOutput = true, // 導向標準輸出
                    RedirectStandardError = true,  // 導向錯誤輸出
                    UseShellExecute = false, // 不使用 shell 執行
                    CreateNoWindow = true // 不創建窗口
                }
            };

            // 開始執行命令
            process.Start();

            // 讀取標準輸出與錯誤
            output = process.StandardOutput.ReadToEnd();
            error = process.StandardError.ReadToEnd();
            // 等待進程完成
            process.WaitForExit();

            // 顯示命令執行的輸出結果
            if (!string.IsNullOrEmpty(output))
            {
                Logger.Info("輸出: " + output);
            }

            // 顯示錯誤（如果有）
            if (!string.IsNullOrEmpty(error))
            {
                Logger.Error("錯誤: " + error);
            }
        }
    }

}
