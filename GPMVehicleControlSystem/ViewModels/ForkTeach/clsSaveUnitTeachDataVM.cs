

using GPMVehicleControlSystem.Models.WorkStation;

namespace GPMVehicleControlSystem.ViewModels.WorkStation
{
    public class clsSaveUnitTeachDataVM
    {
        public int Tag { get; set; } = 0;
        public int Layer { get; set; } = 0;
        public clsWorkStationData TeachData { get; set; } = new clsWorkStationData();
    }
}
