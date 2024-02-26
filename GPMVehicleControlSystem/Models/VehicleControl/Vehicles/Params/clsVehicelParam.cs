using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.TaskExecute.LoadTask;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public enum IO_CONEECTION_POINT_TYPE
    {
        A,
        B
    }
    public class clsVehicelParam
    {
        public Dictionary<string, string> Descrption { get; set; } = new Dictionary<string, string>() {
            { "AgvType(車款)","0:叉車AGV,1:YUNTECH_FORK_AGV, 2:巡檢AGV, 3:潛盾AGV" },
            { "OrderInfoFetchSource(任務訂單訊息來源)","0:從派車任務內容, 1:接收CIM呼叫API" },
            { "LDULD_Task_No_Entry(空取空放)","true:啟用 , false:禁用" },
            { "EQHandshakeMethod(設備交握預設方式)","0:光IO , 1:Modbus , 2:模擬器" },
            { "CstReadFailAction(CST ID讀取失敗後車載狀態)","0:正常,1:當機" },
            { "Cst_ID_Not_Match_Action(CST ID讀取結果與任務不符)","0:上報拍到的ID ,1:向派車查詢虛擬ID" },
            { "LsrObsDetectedAlarmLevel(雷射偵測到設備內有障礙物警報等級)","0:Warning,1:Alarm" },
            { "IO_VAL_TYPE(Modbus交握時使用的IO種類)","0:使用INPUT讀寫,1:使用 InputRegist 讀/SingleRegister寫(歐迪爾模組)" },
            { "HandshakeIOFlickDelayTime (交握 EQIO 瞬閃判斷秒數)","單位:毫秒" },
        };
        public string LogFolder { get; set; } = "GPM_AGV_LOG";
        public AGV_TYPE AgvType { get; set; } = AGV_TYPE.SUBMERGED_SHIELD;
        public int Version { get; set; } = 1;
        public string SID { get; set; } = "SID";
        public string VehicleName { get; set; } = "EQName";


        public Dictionary<clsConnectionParam.CONNECTION_ITEM, clsConnectionParam> Connections { get; set; } = new Dictionary<clsConnectionParam.CONNECTION_ITEM, clsConnectionParam>()
        {
            {  clsConnectionParam.CONNECTION_ITEM.RosBridge, new clsConnectionParam
                {
                     IP = "127.0.0.1",
                     Port = 9090
                }
            },
            {  clsConnectionParam.CONNECTION_ITEM.Wago , new clsConnectionParam
                {
                     IP = "127.0.0.1",
                     Port = 9999,
                     Protocol_Interval_ms =50
                }
            },
            {  clsConnectionParam.CONNECTION_ITEM.AGVS, new clsConnectionParam
                {
                     IP = "127.0.0.1",
                     Port = 5500,
                }
            }
        };
        public clsAGVSConnParam VMSParam { get; set; } = new clsAGVSConnParam();

        public clsLogParams Log { get; set; } = new clsLogParams();

        public bool SimulationMode { get; set; } = false;
        public bool WagoSimulation { get; set; } = true;
        public bool ActiveTrafficControl { get; set; } = false;
        public bool EQHandshakeBypass { get; set; } = false;
        public bool CST_READER_TRIGGER { get; set; } = false;
        public bool Auto_Cleaer_CST_ID_Data_When_Has_Data_But_NO_Cargo { get; set; } = false;
        public bool Auto_Read_CST_ID_When_No_Data_But_Has_Cargo { get; set; } = false;
        public bool ForkLifer_Enable { get; set; } = false;
        public bool FrontLighterFlashWhenNormalMove { get; set; } = false;
        public bool CheckObstacleWhenForkInit { get; set; } = true;
        public bool LocalTaskCheckCargoExist { get; set; } = false;
        public bool SyncEQInfoFromAGVS { get; set; } = false;

        /// <summary>
        /// 一搬走行時偵測貨物傾倒
        /// </summary>
        public bool CargoBiasDetectionWhenNormalMoving { get; set; } = true;
        /// <summary>
        /// 空取空放
        /// </summary>
        public bool LDULD_Task_No_Entry { get; set; } = false;
        public bool WebKeyboardMoveControl { get; set; } = false;
        public bool CSTIDReadNotMatchSimulation { get; set; } = false;
        public bool CIMConn { get; set; } = false;

        /// <summary>
        /// 等待交握訊號時撥放交握音效
        /// </summary>
        public bool PlayHandshakingMusic { get; set; } = true;

        /// <summary>
        /// 量測儀器服務模擬器
        /// </summary>
        public bool MeasureServiceSimulator { get; set; } = false;
        public bool ForkNoInitializeWhenPoseIsHome { get; set; } = false;
        public bool BuzzerOn { get; set; } = true;
        public bool EQHandshakeSimulationAutoRun { get; set; } = true;


        public int LastVisitedTag { get; set; } = 8;
        public ORDER_INFO_FETCH_SOURCE OrderInfoFetchSource { get; set; } = ORDER_INFO_FETCH_SOURCE.FROM_CIM_POST_IN;
        /// <summary>
        /// 停車的精度誤差值(mm)
        /// </summary>
        public double TagParkingTolerance { get; set; } = 5;
        /// <summary>
        /// 送Action任務給車控時等待Action狀態變為Active的Timeout時間(秒)
        /// </summary>
        public double ActionTimeout { get; set; } = 5;
        public double VehielLength { get; set; } = 145.0;
        /// <summary>
        /// 訂閱/Module_Information接收處理數據的週期
        /// </summary>
        /// <remarks>
        /// 單位:毫秒(ms)
        /// </remarks>
        public int ModuleInfoTopicRevHandlePeriod { get; set; } = 10;
        public int ModuleInfoTopicRevQueueSize { get; set; } = 5;

        public string AGVsMessageEncoding { get; set; } = "UTF-8";

        /// <summary>
        /// 禁止上線的TAG
        /// </summary>
        public List<int> ForbidToOnlineTags { get; set; } = new List<int>();

        /// <summary>
        /// 需要檢查EQ狀態的TAG
        /// </summary>
        public List<int> CheckEQDOStatusWorkStationTags { get; set; } = new List<int>();

        public LASER_MODE LDULD_Laser_Mode { get; set; } = LASER_MODE.Bypass;
        public LASER_MODE Spin_Laser_Mode { get; set; } = LASER_MODE.Turning;
        public List<int> StationNeedQueryVirtualID { get; set; } = new List<int>();
        /// <summary>
        /// 侵入設備取放或的前後雷射Bypass
        /// </summary>
        public bool LDULD_FrontBackLaser_Bypass { get; set; } = true;

        public clsMapParam MapParam { get; set; } = new clsMapParam();
        public EQ_HS_METHOD EQHandshakeMethod { get; set; } = EQ_HS_METHOD.PIO;

        internal EQ_HS_METHOD _EQHandshakeMethodStore = EQ_HS_METHOD.PIO;

        public int HandshakeIOFlickDelayTime { get; set; } = 300;
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
            {   HANDSHAKE_EQ_TIMEOUT.TP_3_Wait_AGV_BUSY_OFF , 90  },
            {   HANDSHAKE_EQ_TIMEOUT.TP_5_Wait_AGV_BUSY_OFF , 90 },
        };

        public EQ_INTERACTION_FAIL_ACTION CstReadFailAction { get; set; } = EQ_INTERACTION_FAIL_ACTION.SET_AGV_DOWN_STATUS;
        public CST_ID_NO_MATCH_ACTION Cst_ID_Not_Match_Action { get; set; } = CST_ID_NO_MATCH_ACTION.REPORT_READER_RESULT;
        public EQ_INTERACTION_FAIL_ACTION HandshakeFailWhenLoadFinish { get; set; } = EQ_INTERACTION_FAIL_ACTION.SET_AGV_DOWN_STATUS;
        public clsInspectionAGVParams InspectionAGV { get; set; } = new clsInspectionAGVParams();
        public clsForkAGVParams ForkAGV { get; set; } = new clsForkAGVParams();
        /// <summary>
        /// 模擬器參數
        /// </summary>
        public clsEmulatorParams Emulator { get; set; } = new clsEmulatorParams();
        public clsModbusDIOParams ModbusIO { get; set; } = new clsModbusDIOParams();

        public clsImpactDetectionParams ImpactDetection { get; set; } = new clsImpactDetectionParams();

        public clsBatteryParam BatteryModule { get; set; } = new clsBatteryParam();
        public clsLDULDParams LDULDParams { get; set; } = new clsLDULDParams();

        public clsCargoExistSensorParams CargoExistSensorParams { get; set; } = new clsCargoExistSensorParams();
    }
}
