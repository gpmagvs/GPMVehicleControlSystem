using GPMVehicleControlSystem.Models.WorkStation.ForkTeach;

namespace GPMVehicleControlSystem.ViewModels.ForkTeach
{
    public class clsSaveUnitTeachDataVM
    {
        public int Tag { get; set; } = 0;
        public int Layer { get; set; } = 0;
        public clsForkWorkStationData TeachData { get; set; } = new clsForkWorkStationData();
    }
}
