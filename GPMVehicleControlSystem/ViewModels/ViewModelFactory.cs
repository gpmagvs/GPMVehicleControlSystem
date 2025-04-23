using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Exceptions;
using GPMVehicleControlSystem.Models.RDTEST;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Service;
using NLog;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.ViewModels
{
    public class ViewModelFactory
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        internal static Vehicle AGV => StaStored.CurrentVechicle;

        public static VehicleInstanceInitializeFailException VehicleInstanceCreateFailException { get; internal set; }

        internal static AGVCStatusVM GetVMSStatesVM()
        {
            if (AGV == null)
                return new AGVCStatusVM();
            //if (!AGV.IsSystemInitialized)
            //    return new AGVCStatusVM();
            try
            {

                List<DriverState> driverStates = new List<DriverState>();
                driverStates.AddRange(AGV.WheelDrivers.Select(d => d.Data).ToArray());
                driverStates.Add(AGV.VerticalDriverState.Data);
                string AppVersion = CreateAPPVersionString();
                AGVCStatusVM data_view_model = new AGVCStatusVM()
                {
                    APPVersion = AppVersion,
                    Agv_Type = AGV.Parameters.AgvType,
                    AutoMode = AGV.Operation_Mode,
                    OnlineMode = AGV.Remote_Mode,
                    IsInitialized = AGV.IsInitialized,
                    IsSystemIniting = !AGV.IsSystemInitialized,
                    IsLDULD_No_Entry = AGV.Parameters.LDULD_Task_No_Entry,
                    AGVC_ID = AGV.Parameters.SID,
                    CarName = AGV.Parameters.VehicleName,
                    MainState = AGV.Main_Status.ToString(),
                    SubState = AGV.GetSub_Status().ToString(),
                    Tag = AGV.BarcodeReader.CurrentTag,
                    Last_Visit_MapPoint = AGV.lastVisitedMapPoint,
                    Last_Visited_Tag = AGV.Navigation.LastVisitedTag,
                    CST_Data = AGV.Parameters.AgvType == clsEnums.AGV_TYPE.INSPECTION_AGV ? "" : (AGV as SubmarinAGV)?.CSTReader.ValidCSTID,
                    BatteryStatus = GetBatteryStatusVM(),
                    Pose = AGV.Navigation.Data.robotPose.pose,
                    Angle = AGV.Navigation.Angle,
                    Mileage = AGV.Odometry,
                    BCR_State_MoveBase = AGV.BarcodeReader.Data,
                    AlarmCodes = AlarmManager.CurrentAlarms.Values.ToArray(),
                    MapComparsionRate = AGV.SickData.MapSocre,
                    LocStatus = AGV.SickData.Data.loc_status,
                    AGV_Direct = AGV.Navigation.Direction.ToString().ToUpper(),
                    DriversStates = driverStates.ToArray(),
                    Laser_Mode = (int)AGV.Laser.Mode,
                    NavInfo = new NavStateVM
                    {
                        Destination = AGV.ExecutingTaskEntity == null && !AGV.IsWaitForkNextSegmentTask ? "" : AGV._RunTaskData.Destination + "",
                        DestinationMapPoint = AGV.GetSub_Status() != clsEnums.SUB_STATUS.RUN && !AGV.IsWaitForkNextSegmentTask ? new MapPoint { Name = "", Graph = new Graph { Display = "" } } : AGV.DestinationMapPoint,
                        PathPlan = AGV.GetSub_Status() != clsEnums.SUB_STATUS.RUN ? new int[0] : AGV.ExecutingTaskEntity == null ? new int[0] : AGV.ExecutingTaskEntity.RunningTaskData.ExecutingTrajecory.GetRemainPath(AGV.Navigation.LastVisitedTag),
                        IsSegmentTaskExecuting = AGV.IsWaitForkNextSegmentTask

                    },
                    Current_LASER_MODE = GetLaserModeDescription(),
                    ZAxisDriverState = AGV.VerticalDriverState.StateData == null ? new DriverState() : AGV.VerticalDriverState.StateData as DriverState,
                    IsLaserModeSettingError = AGV.Laser.SickSsystemState.application_error,
                    ForkHasLoading = AGV.CargoStateStorer.HasAnyCargoOnAGV(AGV.Parameters.LDULD_Task_No_Entry),
                    CargoExist = AGV.CargoStateStorer.HasAnyCargoOnAGV(AGV.Parameters.LDULD_Task_No_Entry),
                    IsForkExtenable = AGV.IsForkExtenable,
                    HandShakeSignals = new
                    {
                        EQ = AGV.EQHsSignalStates.ToDictionary(kp => kp.Key, kp => kp.Value.State),
                        AGV = AGV.AGVHsSignalStates
                    },
                    HandShakeTimers = AGV.EQHSTimers,
                    SysLoading = new AGVCStatusVM.clsSysLoading
                    {
                        Memory = SystemLoadingMonitorBackgroundServeice.CurrentRAM,
                        CPU = SystemLoadingMonitorBackgroundServeice.CurrentCPU
                    },
                    HandshakeStatus = new AGVCStatusVM.clsEQHandshake
                    {
                        IsHandshaking = AGV.IsHandshaking,
                        HandshakingInfoText = AGV.HandshakeStatusText,
                        ConnectionType = AGV.currentHandshakeProtocol,
                        Connected = AGV.currentHandshakeProtocol != Vehicle.EQ_HS_METHOD.MODBUS ? true : StaStored.ConnectingEQHSModbus.Connected
                    },
                    OrderInfo = AGV.orderInfoViewModel,
                    IsForkHeightAboveSafty = IsForkHeightAboveSaftyHeightSetting(),
                    InitializingStatusText = AGV.InitializingStatusText,
                    AMCAGVSensorState = GetSensorsActiveState(),
                    IMUMaxMinValRecord = AGV.IMU.MaxMinGValRecord,
                    BuzzerState = new AGVCStatusVM.BuzzerModuleState
                    {
                        isPlaying = BuzzerPlayer.IsPlaying,
                        player = BuzzerPlayer.APLAYER != null ? "linux-aplay" : "ros-sound-play",
                        playingAudio = BuzzerPlayer.PlayingAudio
                    }


                };
                return data_view_model;
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message, ex);
                return new AGVCStatusVM()
                {
                    AlarmCodes = new clsAlarmCode[]
                    {
                        new clsAlarmCode
                        {
                            Time=DateTime.Now,
                             Code =  (int)AlarmCodes.AGVS_ALIVE_CHECK_TIMEOUT,
                              Description ="系統異常",
                              ELevel = clsAlarmCode.LEVEL.Alarm
                        },
                        new clsAlarmCode
                        {
                            Time=DateTime.Now,

                             Code =  (int)AlarmCodes.Code_Error_In_System,
                              Description ="系統異常",
                              ELevel = clsAlarmCode.LEVEL.Alarm
                        }
                    },
                    Agv_Type = clsEnums.AGV_TYPE.INSPECTION_AGV,

                    BatteryStatus = new BatteryStateVM[2]
                    {
                        new BatteryStateVM
                        {
                            BatteryLevel = 69,
                             BatteryID=0
                        },
                        new BatteryStateVM
                        {
                            BatteryLevel = 21,
                            BatteryID=2
                        }
                    },
                    BCR_State_MoveBase = new BarcodeReaderState
                    {
                        tagID = 69,
                        theta = -44,
                        xValue = 3,
                        yValue = -2
                    }
                };
            }

            static string GetLaserModeDescription()
            {
                string laserModeText = AGV.Laser.GetType().Name == typeof(clsAMCLaser).Name ? (AGV.Laser as clsAMCLaser).Mode.ToString() : AGV.Laser.Mode.ToString();
                return $"{laserModeText}({(int)AGV.Laser.CurrentLaserModeOfSick})";
            }

            static bool IsForkHeightAboveSaftyHeightSetting()
            {
                if (AGV.Parameters.AgvType != clsEnums.AGV_TYPE.FORK || AGV.ForkLifter.fork_ros_controller.verticalActionService == null)
                    return false;
                return AGV.ForkLifter.fork_ros_controller.verticalActionService.CurrentPosition > AGV.Parameters.ForkAGV.SaftyPositionHeight;
            }
        }

        private static Dictionary<string, bool> GetSensorsActiveState()
        {
            if (AGV.Parameters.AgvType != clsEnums.AGV_TYPE.INSPECTION_AGV)
                return new Dictionary<string, bool>();

            var amc_agv_con = (AGV.AGVC as InspectorAGVCarController);
            var equipmentActiveStates = amc_agv_con.EquipmentAvtiveState.ToDictionary(eq => eq.Key.ToString(), eq => eq.Value);
            var sensorActiveStates = amc_agv_con.SensorAvtiveState.ToDictionary(sensor => sensor.Key.ToString(), sensor => sensor.Value);
            // 使用LINQ合併兩個字典，後者的鍵值將覆蓋前者的
            var mergedDict = equipmentActiveStates.Concat(sensorActiveStates)
                                  .GroupBy(kvp => kvp.Key)
                                  .ToDictionary(group => group.Key, group => group.Last().Value);
            return mergedDict;
        }

        private static string CreateAPPVersionString()
        {
            string headStr = "";
            var agv_type = AGV.Parameters.AgvType;
            if (agv_type == clsEnums.AGV_TYPE.SUBMERGED_SHIELD)
                headStr = "S";
            else if (agv_type == clsEnums.AGV_TYPE.FORK)
                headStr = "F";
            else if (agv_type == clsEnums.AGV_TYPE.INSPECTION_AGV)
                headStr = "I";
            else
                headStr = "V";
            return headStr + StaStored.APPVersion;
        }

        private static BatteryStateVM[] GetBatteryStatusVM()
        {
            BatteryStateVM[] viewmodel = AGV.Batteries.Count == 0 ?
            CreateFakeBatteryViewModelData() :
                AGV.Batteries.ToList().FindAll(b => b.Value != null).Select(bat => new BatteryStateVM
                {
                    BatteryLevel = bat.Value.Data.batteryLevel,
                    Voltage = bat.Value.Data.voltage,
                    ChargeCurrent = bat.Value.Data.chargeCurrent,
                    IsCharging = bat.Value.IsCharging(),
                    IsError = bat.Value.Current_Alarm_Code != AlarmCodes.None,
                    CircuitOpened = AGV.IsChargeCircuitOpened,
                    BatteryID = bat.Key,
                    SensorInfo = AGV.Parameters.AgvType == clsEnums.AGV_TYPE.INSPECTION_AGV ? new BatteryPositionInfoVM()
                    {
                        IsExistSensor1ON = AGV.WagoDI.GetState(bat.Key == 1 ? DI_ITEM.Battery_1_Exist_1 : DI_ITEM.Battery_2_Exist_1),
                        IsExistSensor2ON = AGV.WagoDI.GetState(bat.Key == 1 ? DI_ITEM.Battery_1_Exist_2 : DI_ITEM.Battery_2_Exist_2),
                        IsDockingSensor1ON = !AGV.WagoDI.GetState(bat.Key == 1 ? DI_ITEM.Battery_1_Exist_3 : DI_ITEM.Battery_2_Exist_3),
                        IsDockingSensor2ON = !AGV.WagoDI.GetState(bat.Key == 1 ? DI_ITEM.Battery_1_Exist_4 : DI_ITEM.Battery_2_Exist_4),
                        IsLockSensorON = AGV.WagoDI.GetState(bat.Key == 1 ? DI_ITEM.Battery_1_Lock_Sensor : DI_ITEM.Battery_2_Lock_Sensor),
                        IsUnlockSensorON = AGV.WagoDI.GetState(bat.Key == 1 ? DI_ITEM.Battery_1_Unlock_Sensor : DI_ITEM.Battery_2_Unlock_Sensor)
                    } : new BatteryPositionInfoVM()
                }).ToArray();
            return viewmodel;
        }

        private static BatteryStateVM[] CreateFakeBatteryViewModelData()
        {
            return new BatteryStateVM[2]
                            {
                    new BatteryStateVM
                    {
                        BatteryID=1,
                        BatteryLevel = 66,
                         SensorInfo = AGV.Parameters.AgvType == clsEnums.AGV_TYPE.INSPECTION_AGV ? new BatteryPositionInfoVM()
                         {
                             IsExistSensor1ON = true,
                             IsExistSensor2ON = true,
                             IsDockingSensor1ON = false,
                             IsDockingSensor2ON = false,
                             IsLockSensorON = true,
                             IsUnlockSensorON = false
                         } : new BatteryPositionInfoVM()
                    },
                    new BatteryStateVM
                    {
                        BatteryID=2,
                        BatteryLevel = 10,
                         SensorInfo = AGV. Parameters.AgvType == clsEnums.AGV_TYPE.INSPECTION_AGV ? new BatteryPositionInfoVM()
                         {
                            IsExistSensor1ON = true,
                            IsExistSensor2ON = true,
                            IsDockingSensor1ON = false,
                            IsDockingSensor2ON = false,
                            IsLockSensorON = false,
                            IsUnlockSensorON = false
                         } : new BatteryPositionInfoVM()
                    }, };
        }

        internal static ConnectionStateVM GetConnectionStatesVM()
        {
            try
            {
                if (AGV == null)
                    return new ConnectionStateVM();
                ConnectionStateVM data_view_model = new ConnectionStateVM()
                {
                    RosbridgeServer = IsCarControlAbnormal() ? ConnectionStateVM.CONNECTION.DISCONNECT : ConnectionStateVM.CONNECTION.CONNECTED,
                    VMS = AGV.AGVS == null ? ConnectionStateVM.CONNECTION.DISCONNECT : AGV.AGVS.IsConnected() ? ConnectionStateVM.CONNECTION.CONNECTED : ConnectionStateVM.CONNECTION.DISCONNECT,
                    WAGO = AGV.WagoDI == null ? ConnectionStateVM.CONNECTION.DISCONNECT : AGV.WagoDI.IsConnected() ? ConnectionStateVM.CONNECTION.CONNECTED : ConnectionStateVM.CONNECTION.DISCONNECT,
                };
                return data_view_model;
            }
            catch (Exception ex)
            {
                Console.WriteLine("尚無法取得連線狀態:" + ex.Message);
                return new ConnectionStateVM()
                {
                    RosbridgeServer = ConnectionStateVM.CONNECTION.DISCONNECT,
                    VMS = ConnectionStateVM.CONNECTION.DISCONNECT,
                    WAGO = ConnectionStateVM.CONNECTION.DISCONNECT,
                };
            }

            bool IsCarControlAbnormal()
            {
                return AGV.AGVC == null || !AGV.AGVC.IsConnected() || AGV.Navigation.IsCommunicationError;
            }
        }

        internal static DIOTableVM GetDIOTableVM()
        {
            if (AGV == null || AGV.WagoDI == null)
                return new DIOTableVM();

            return new DIOTableVM
            {
                Inputs = AGV.WagoDI.VCSInputs.ToList(),
                Outputs = AGV.WagoDO.VCSOutputs.ToList(),
                IsE84HsUseEmulator = AGV.currentHandshakeProtocol == Vehicle.EQ_HS_METHOD.EMULATION,
            };
        }
        internal static object GetRDTestData()
        {
            return new
            {
                move_test = StaRDTestManager.MoveTester.testing_data
            };
        }
    }
}
