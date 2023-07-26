using GPMVehicleControlSystem.Models.ForkTeach;

namespace GPMVehicleControlSystem.ViewModels.ForkTeach
{
    public class clsSaveUnitTeachDataVM
    {
        public int Tag { get; set; } = 0;
        public int Layer { get; set; } = 0;
        public clsTeachData TeachData { get; set; } = new clsTeachData();
    }
}
