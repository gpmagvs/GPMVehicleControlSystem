using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.TaskExecute.LoadTask;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
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
        public bool FrontLighterFlashWhenNormalMove { get; set; } = false;

        public bool CheckObstacleWhenForkInit { get; set; } = true;
        /// <summary>
        /// 一搬走行時偵測貨物傾倒
        /// </summary>
        public bool CargoBiasDetectionWhenNormalMoving { get; set; } = true;
        /// <summary>
        /// 空取空放
        /// </summary>
        public bool LDULD_Task_No_Entry { get; set; } = false;
        public bool WebKeyboardMoveControl { get; set; } = false;
        public bool CIMConn { get; set; } = false;

        /// <summary>
        /// 等待交握訊號時撥放交握音效
        /// </summary>
        public bool PlayHandshakingMusic { get; set; } = true;

        /// <summary>
        /// 量測儀器服務模擬器
        /// </summary>
        public bool MeasureServiceSimulator { get; set; } = false;
        public int LastVisitedTag { get; set; } = 8;

        /// <summary>
        /// 禁止上線的TAG
        /// </summary>
        public List<int> ForbidToOnlineTags { get; set; } = new List<int>();

        /// <summary>
        /// 需要檢查EQ狀態的TAG
        /// </summary>
        public List<int> CheckEQDOStatusWorkStationTags { get; set; } = new List<int>();

        /// <summary>
        /// 切斷充電迴路的電壓閥值,當電壓大於此數值，將會切斷充電迴路，避免電池過度充電。
        /// 單位: mV
        /// </summary>
        public int CutOffChargeRelayVoltageThreshodlval { get; set; } = 28800;
        public LASER_MODE LDULD_Laser_Mode { get; set; } = LASER_MODE.Bypass;
        public LASER_MODE Spin_Laser_Mode { get; set; } = LASER_MODE.Turning;

        /// <summary>
        /// 侵入設備取放或的前後雷射Bypass
        /// </summary>
        public bool LDULD_FrontBackLaser_Bypass { get; set; } = true;

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

        /// <summary>
        /// TA Timeout : 偵測EQ訊號狀態變化Timeout秒數
        /// </summary>
        public Dictionary<HANDSHAKE_EQ_TIMEOUT, int> EQHSTimeouts { get; set; } = new Dictionary<HANDSHAKE_EQ_TIMEOUT, int>()
        {
            {   HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON , 5  },
            {   HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON , 5  },
            {   HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON , 5  },
            {   HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF , 90  },
            {   HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF , 5  },
        };

        public clsInspectionAGVParams InspectionAGV { get; set; } = new clsInspectionAGVParams();
        public EQ_INTERACTION_FAIL_ACTION CstReadFailAction { get; set; } = EQ_INTERACTION_FAIL_ACTION.SET_AGV_DOWN_STATUS;
        public EQ_INTERACTION_FAIL_ACTION HandshakeFailWhenLoadFinish { get; set; } = EQ_INTERACTION_FAIL_ACTION.SET_AGV_DOWN_STATUS;
    }

    public class clsInspectionAGVParams
    {
        public bool CheckBatteryLockStateWhenInit { get; set; } = false;
    }
}
