namespace GPMVehicleControlSystem.Models.WorkStation.ForkTeach
{

    /// <summary>
    /// Fork的工位資料,包含上下位置
    /// </summary>
    public class clsForkWorkStationData : clsWorkStationData
    {
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
