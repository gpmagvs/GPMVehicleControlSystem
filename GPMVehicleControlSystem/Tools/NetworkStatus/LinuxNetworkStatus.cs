
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GPMVehicleControlSystem.Tools.NetworkStatus
{
    public class LinuxNetworkStatus : NetworkStatusBase
    {
        public override async Task<(double trasmited, double recieved)> GetNetworkStatus()
        {
            try
            {
                // 執行 ifconfig 命令
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"ifconfig\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process process = new Process { StartInfo = psi };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                // 解析輸出
                (long transmit, long recieve) t1 = await ParseNetworkStatus(output);
                await Task.Delay(1000);
                // 再次執行 ifconfig 命令
                process.Start();
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                (long transmit, long recieve) t2 = await ParseNetworkStatus(output);
                return (Math.Round((t2.transmit - t1.transmit) / 1024.0 / 1024.0, 2), Math.Round((t2.recieve - t1.recieve) / 1024.0 / 1024.0, 2));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"獲取網絡狀態失敗: {ex.Message}");
                return (-1, -1);
            }
            //return base.GetNetworkStatus();
        }


        async Task<(long transmit, long recieve)> ParseNetworkStatus(string output)
        {
            await Task.Delay(1);
            long transmit = 0;
            long recieve = 0;
            // 使用正則表達式解析接收和傳送的字節數
            Regex rx = new Regex(@"RX bytes:(\d+) .* TX bytes:(\d+)", RegexOptions.Compiled);
            MatchCollection matches = rx.Matches(output);
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    transmit = long.Parse(match.Groups[1].Value);
                    recieve = long.Parse(match.Groups[2].Value);
                    break;
                }
            }
            return (transmit, recieve);
        }
    }
}
