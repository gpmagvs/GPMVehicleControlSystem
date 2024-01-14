using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsConnectionParam
    {
        public enum CONNECTION_ITEM
        {
            RosBridge,
            Wago,
            AGVS
        }
        public string IP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = int.MaxValue;
        public int Protocol_Interval_ms { get; set; } = 5;
    }
    public class clsAGVSConnParam
    {
        public string Notes { get; set; } = "";
        public string LocalIP { get; set; } = "192.168.0.1";
        public VMS_PROTOCOL Protocol { get; set; } = VMS_PROTOCOL.GPM_VMS;
        public string MapUrl { get; set; } = "http://127.0.0.1:5216/api/Map";
    }

}