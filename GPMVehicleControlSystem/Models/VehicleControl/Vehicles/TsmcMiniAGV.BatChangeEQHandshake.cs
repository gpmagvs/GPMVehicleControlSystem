using GPMVehicleControlSystem.Models.WorkStation;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    ///  電池交換站交握
    /// </summary>
    public partial class TsmcMiniAGV
    {
        public override string WagoIOConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/IO_Wago_Inspection_AGV.ini");

    }
}
