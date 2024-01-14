using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsLogParams
    {
        public bool ConsoleInfoShow
        {
            get=> LOG.InfoShow; set=> LOG.InfoShow = value;
        }
        public bool ConsoleTraceShow
        {
            get => LOG.TraceShow; set => LOG.TraceShow = value;
        }
        public bool ConsoleWarningShow
        {
            get => LOG.WarningShow; set => LOG.WarningShow= value;
        }
        public bool ConsoleErrorShow
        {
            get => LOG.ErrorShow; set => LOG.ErrorShow= value;
        }
        public bool ConsoleCriticalShow
        {
            get => LOG.CriticalShow; set => LOG.CriticalShow= value;
        }

    }
}
