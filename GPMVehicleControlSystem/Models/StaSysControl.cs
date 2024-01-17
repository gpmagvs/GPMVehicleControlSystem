using AGVSystemCommonNet6.Log;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Models
{
    public static class StaSysControl
    {

        public static void SystemClose()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                LOG.WARN("System will close after 1 sec...");
                await Task.Delay(1000);
                Environment.Exit(0);
            });
        }
        public static void SystemRestart()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                LOG.WARN($"System will restart after 1 sec...(exe:{Environment.ProcessPath})");
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
