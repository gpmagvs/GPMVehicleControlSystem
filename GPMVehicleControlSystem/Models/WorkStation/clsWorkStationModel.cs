
using GPMVehicleControlSystem.Models.WorkStation.ForkTeach;

namespace GPMVehicleControlSystem.Models.WorkStation
{
    public enum WORKSTATION_HS_METHOD
    {
        NO_HS,
        HS
    }
    public class clsWorkStationModel
    {
        public string Last_Editor { get; set; } = "dev";
        public string Version { get; set; } = "2023.07.26.08.44.1";

        public Dictionary<int, clsWorkStationData> Stations { get; set; } = new Dictionary<int, clsWorkStationData>();
    }

    /// <summary>
    /// 每一個站點的資料
    /// </summary>
    public class clsWorkStationData
    {

        public int Tag { get; set; } 
        public string Name { get; set; } = "";
        /// <summary>
        /// 工位交握方式
        /// </summary>
        public WORKSTATION_HS_METHOD HandShakeMode { get; set; } = WORKSTATION_HS_METHOD.NO_HS;
    }
}
