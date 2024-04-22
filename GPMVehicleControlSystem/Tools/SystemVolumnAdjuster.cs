using Microsoft.AspNetCore.Mvc.ActionConstraints;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Tools
{
    public abstract class SystemVolumnAdjuster
    {
        public enum ADJUST_ACTION
        {
            INCREASE,
            DECREASE,
            SET
        }
        public abstract void VolumeControl(ADJUST_ACTION action, int value);
    }

    public class LinuxVolumeAdjuster : SystemVolumnAdjuster
    {
        private string GenArguments(ADJUST_ACTION action, int value)
        {
            string _controlSymbol = "";
            switch (action)
            {
                case ADJUST_ACTION.INCREASE:
                    _controlSymbol = "+";
                    break;
                case ADJUST_ACTION.DECREASE:
                    _controlSymbol = "-";
                    break;
                case ADJUST_ACTION.SET:
                    _controlSymbol = "";
                    break;
            }

            return $"sset Master {value}%{_controlSymbol}";
        }
        public override void VolumeControl(ADJUST_ACTION action, int value)
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "amixer";
                    process.StartInfo.Arguments = GenArguments(action, value);
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();

                    // Read output (if needed)
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(errors))
                    {
                        Console.WriteLine("Error: " + errors);
                    }
                    else
                    {
                        Console.WriteLine("Volume increased: " + output);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }
    }
}
