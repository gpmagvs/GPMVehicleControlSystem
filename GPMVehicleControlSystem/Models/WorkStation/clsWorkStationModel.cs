

namespace GPMVehicleControlSystem.Models.WorkStation
{
    public enum FORK_HEIGHT_POSITION
    {
        UP_,
        DOWN_
    }
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
        public WORKSTATION_HS_METHOD HandShakeModeHandShakeMode { get; set; } = WORKSTATION_HS_METHOD.NO_HS;
        public int ModbusTcpPort { get; set; } = 6502;
        public double Up_Pose_Limit { get; set; } = 5.0;
        public double Down_Pose_Limit { get; set; } = 0.0;
        public bool ForkArmExtend { get; set; } = false;
        public Dictionary<int, clsStationLayerData> LayerDatas { get; set; } = new Dictionary<int, clsStationLayerData>();

    }


    public class clsStationLayerData
    {
        public double Up_Pose { get; set; }
        public double Down_Pose { get; set; }
    }
}
