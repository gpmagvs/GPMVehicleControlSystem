namespace GPMVehicleControlSystem.Tools.CPUUsage
{
    public class CPUUaageBase
    {
        public CPUUaageBase()
        {

        }

        public virtual async Task<double> GetCPU()
        {
            return 0;
        }
        public virtual async Task<string> GetTop10CupUseProcess()
        {
            return "";
        }
    }
}
