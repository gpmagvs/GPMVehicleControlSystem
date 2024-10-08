using NLog;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Models
{
    public static class StaSysControl
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        public static void KillRunningVCSProcesses()
        {
            var currentProcessId = Process.GetCurrentProcess().Id;
            var currentProcessName = Process.GetCurrentProcess().ProcessName;

            Process.GetProcessesByName(currentProcessName)
                                       .Where(p => p.Id != currentProcessId).ToList().ForEach(other_process => other_process.Kill());
        }

        public static void SystemClose()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                _logger.Warn("System will close after 1 sec...");
                await Task.Delay(1000);
                Environment.Exit(0);
            });
        }
        public static void SystemRestart()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                _logger.Warn($"System will restart after 1 sec...(exe:{Environment.ProcessPath})");
                await Task.Delay(1000);
                Process _p = new Process();
                _p.StartInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    WorkingDirectory = Path.GetDirectoryName(Environment.ProcessPath)
                };
                _p.Start();
            });
        }

    }
}
