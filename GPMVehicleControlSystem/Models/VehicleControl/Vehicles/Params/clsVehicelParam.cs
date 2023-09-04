using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsVehicelParam
    {
        public string LogFolder { get; set; } = "GPM_AGV_LOG";
        public AGV_TYPE AgvType { get; set; } = AGV_TYPE.SUBMERGED_SHIELD;
        public string SID { get; set; } = "SID";
        public string VehicleName { get; set; } = "EQName";
        public bool SimulationMode { get; set; } = false;
        public bool WagoSimulation { get; set; } = true;
        public bool ActiveTrafficControl { get; set; } = false;
        public bool EQHandshakeBypass { get; set; } = false;
        public bool CST_READER_TRIGGER { get; set; } = false;
        public bool ForkLifer_Enable { get; set; } = false;
        public int LastVisitedTag { get; set; } = 8;
        public Dictionary<string, clsConnectionParam> Connections { get; set; } = new Dictionary<string, clsConnectionParam>()
        {
            { "RosBridge" , new clsConnectionParam
                {
                     IP = "127.0.0.1",
                     Port = 9090
                }
            },
            { "Wago" , new clsConnectionParam
                {
                     IP = "127.0.0.1",
                     Port = 9999
                }
            },
            { "AGVS" , new clsConnectionParam
                {
                     IP = "127.0.0.1",
                     Port = 5036,
                }
            }
        };

        public clsAGVSConnParam VMSParam { get; set; } = new clsAGVSConnParam();
        public clsMapParam MapParam { get; set; } = new clsMapParam();
        public EQ_HS_METHOD EQHandshakeMethod { get; set; } = EQ_HS_METHOD.E84;

        public clsObstacleDetection LOAD_OBS_DETECTION { get; set; } = new clsObstacleDetection();
        public clsCstExistDetection CST_EXIST_DETECTION { get; set; } = new clsCstExistDetection();
        public clsSensorBypass SensorBypass { get; set; } = new clsSensorBypass();
    }

}
