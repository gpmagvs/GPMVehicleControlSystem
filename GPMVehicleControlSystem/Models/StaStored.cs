using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;

namespace GPMVehicleControlSystem.Models
{
    public static class StaStored
    {
        public static string APPVersion = "10.28.1";
        public static Vehicle CurrentVechicle;
        public static clsEQHandshakeModbusTcp ConnectingEQHSModbus { get; internal set; } = new clsEQHandshakeModbusTcp();
    }
}
