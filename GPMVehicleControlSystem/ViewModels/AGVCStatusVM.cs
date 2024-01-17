using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using AGV_VMS.ViewModels;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using static AGVSystemCommonNet6.clsEnums;
using AGVSystemCommonNet6.MAP;
using clsAlarmCode = AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM.clsAlarmCode;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

namespace GPMVehicleControlSystem.ViewModels
{
    public class AGVCStatusVM
    {
        public string APPVersion { get; set; } = "1.0.0";
        public bool Simulation { get; set; }
        public AGV_TYPE Agv_Type { get; set; }
        public string MainState { get; set; } = "";
        public string SubState { get; set; } = "";
        public bool IsInitialized { get; set; }
        public bool IsSystemIniting { get; set; }
        public bool IsForkHeightAboveSafty { get; set; }
        public REMOTE_MODE OnlineMode { get; set; } = REMOTE_MODE.OFFLINE;
        public OPERATOR_MODE AutoMode { get; set; } = OPERATOR_MODE.MANUAL;
        public string CarName { get; set; } = "";
        public string AGVC_ID { get; set; } = "";
        public string CST_Data { get; set; } = "";
        public string InitializingStatusText { get; set; } = "";
        public int Tag { get; set; }
        public MapPoint Last_Visit_MapPoint { get; set; } = new MapPoint();
        public int Last_Visited_Tag { get; set; }
        public BatteryStateVM[] BatteryStatus { get; set; } = new BatteryStateVM[0]; //TODO Multi battery data should define
        public double Mileage { get; set; }
        public Pose Pose { get; set; } = new Pose();
        public double LinearSpeed { get; set; } = 0;
        public double AngularSpeed { get; set; } = 0;
        public double Angle { get; set; } = -1;
        public string AGV_Direct { get; set; }
        public BarcodeReaderState BCR_State_MoveBase { get; set; } = new BarcodeReaderState();
        public DriverState ZAxisDriverState { get; set; } = new DriverState();
        public string ZAxisActionName { get; set; } = "";
        /// <summary>
        /// 地圖比對率
        /// </summary>
        public double MapComparsionRate { get; set; }
        /// <summary>
        /// SICK 定位狀態
        /// </summary>
        public int LocStatus { get; set; }
        public bool ForkCSTExist { get; set; }
        public bool ForkFrontEndSensorTrigger { get; set; }
        public clsAlarmCode[] AlarmCodes { get; set; } = new clsAlarmCode[0];
        public object AlarmsGroup
        {
            get
            {
                List<clsAlarmCode> alarms = AlarmCodes.Where(alarm => alarm.EAlarmCode != AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM.AlarmCodes.None).ToList();
                if (alarms.Count > 0)
                {
                    Dictionary<int, object> group = new Dictionary<int, object>();
                    IEnumerable<int> codes = alarms.OrderBy(al => al.Time).Select(alarm => alarm.Code).Distinct();
                    foreach (var code in codes)
                    {
                        var cont = alarms.FindAll(alarm => alarm.Code == code).Count;
                        clsAlarmCode alarm = alarms.FindAll(alarm => alarm.Code == code).Last();
                        group.Add(code, new { Count = cont, Alarm = alarm });
                    }
                    return group;
                }
                else
                    return new Dictionary<int, object>();
            }
        }
        public clsAlarmCode NewestAlarm
        {
            get
            {
                if (AlarmCodes == null)
                    return null;

                if (AlarmCodes.Length != 0)
                {
                    return AlarmCodes.Last();
                }
                else
                    return null;
            }
        }
        public DriverState[] DriversStates { get; set; } = new DriverState[0];
        public int Laser_Mode { get; set; } = 0;
        public UltrasonicSensorState UltrSensorState { get; set; } = new UltrasonicSensorState();
        public bool IsAGVPoseError { get; set; } = false;
        public NavStateVM NavInfo { get; set; } = new NavStateVM();
        public string Current_LASER_MODE { get; set; } = "";
        public LightsStatesVM LightsStates { get; set; } = new LightsStatesVM();
        public bool IsLaserModeSettingError { get; set; } = false;
        public bool IsLDULD_No_Entry { get; set; } = false;
        public bool ForkHasLoading { get; set; }
        public bool CargoExist { get; set; }
        public object HandShakeSignals { get; set; } = new object();
        public object HandShakeTimers { get; set; } = new object();
        public clsSysLoading SysLoading { get; set; } = new clsSysLoading();
        public clsEQHandshake HandshakeStatus { get; set; } = new clsEQHandshake();
        public clsTaskDownloadData.clsOrderInfo OrderInfo { get; set; } = new clsTaskDownloadData.clsOrderInfo();
        public class clsTransferInfoViewModel
        {
            public string Action { get; set; } = "";
            public string Destine { get; set; } = "";
            public string Source { get; set; } = "";

            internal void Clear()
            {
                Action = Source = Destine = "";
            }
        }

        public class clsSysLoading
        {
            public double CPU { get; set; }
            public double Memory { get; set; }
        }
        public class clsEQHandshake
        {
            public EQ_HS_METHOD ConnectionType { get; set; }
            public bool Connected { get; set; }

            public bool IsHandshaking { get; set; }

            public string HandshakingInfoText { get; set; } = "Non-Handshake";

        }
    }
}
