using GPMVehicleControlSystem.Models.WorkStation;

namespace GPMVehicleControlSystem.Models.WorkStation.ForkTeach
{
    public enum FORK_HEIGHT_POSITION
    {
        UP_,
        DOWN_
    }
    public class clsForkWorkStationModel : clsWorkStationModel
    {
        public  Dictionary<int, clsForkWorkStationData> Stations { get; set; } = new Dictionary<int, clsForkWorkStationData>();
    }
}
