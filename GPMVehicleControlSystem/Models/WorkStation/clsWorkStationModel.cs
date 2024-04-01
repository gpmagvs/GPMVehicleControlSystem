

using AGVSystemCommonNet6.AGVDispatch;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

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
    public enum CARGO_TRANSFER_MODE
    {
        EQ_Pick_and_Place,
        AGV_Pick_and_Place,
    }
    public class clsWorkStationModel
    {
        public string Last_Editor { get; set; } = "dev";
        public string Version { get; set; } = "2023.07.26.08.44.1";

        public Dictionary<int, clsWorkStationData> Stations { get; set; } = new Dictionary<int, clsWorkStationData>();



        internal void SyncInfo(List<clsAGVSConnection.clsEQOptions> eqinfomations)
        {

            foreach (var item in eqinfomations)
            {
                if (Stations.TryGetValue(item.Tag, out clsWorkStationData? workstation))
                {
                    workstation.Name = item.EqName;
                    workstation.ModbusTcpPort = item.AGVModbusGatewayPort;
                }
                else
                {
                    Stations.Add(item.Tag, new clsWorkStationData()
                    {
                        Name = item.EqName,
                        ModbusTcpPort = item.AGVModbusGatewayPort,
                    });
                }
            }
        }
    }

    /// <summary>
    /// 每一個站點的資料
    /// </summary>
    public class clsWorkStationData
    {
        public Dictionary<string, string> Notes { get; set; } = new Dictionary<string, string>()
        {
            { "HandShakeModeHandShakeMode ","設備交握模式- 0:不需交握,1:需交握" },
            { "HandShakeConnectionMode ","設備交握通訊模式- 0:光IO,1:Modbuus,2:模擬" },
            { "CargoTransferMode ","貨物轉移模式- 0:設備動作,1:AGV動作" },
            { "ForkArmExtend ","牙叉是否需要伸出- true:需伸出,false:不需伸出" },
        };
        public int Tag { get; set; }
        public string Name { get; set; } = "";
        /// <summary>
        /// 工位交握方式
        /// </summary>
        public WORKSTATION_HS_METHOD HandShakeModeHandShakeMode { get; set; } = WORKSTATION_HS_METHOD.NO_HS;
        public EQ_HS_METHOD HandShakeConnectionMode { get; set; } = EQ_HS_METHOD.MODBUS;
        /// <summary>
        /// 貨物轉移給設備時,主動端設定
        /// </summary>
        public CARGO_TRANSFER_MODE CargoTransferMode { get; set; } = CARGO_TRANSFER_MODE.AGV_Pick_and_Place;
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
