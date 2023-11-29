using static GPMVehicleControlSystem.ViewModels.ForkTestVM;
using GPMVehicleControlSystem.Models.VehicleControl;
using GPMVehicleControlSystem.Models;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using AGVSystemCommonNet6;

using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models.RDTEST;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Tools;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;

namespace GPMVehicleControlSystem.ViewModels
{
    public class ViewModelFactory
    {
        internal static Vehicle AGV => StaStored.CurrentVechicle;

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
                    Simulation = AGV.Parameters.SimulationMode,
                    AutoMode = AGV.Operation_Mode,
                    OnlineMode = AGV.Remote_Mode,
                    IsInitialized = AGV.IsInitialized,
                    IsSystemIniting = !AGV.IsSystemInitialized,
                    AGVC_ID = AGV.Parameters.SID,
                    CarName = AGV.Parameters.VehicleName,
                    MainState = AGV.Main_Status.ToString(),
                    SubState = AGV.Sub_Status.ToString(),
                    Tag = AGV.BarcodeReader.CurrentTag,
                    Last_Visit_MapPoint = AGV.lastVisitedMapPoint,
                    Last_Visited_Tag = AGV.Navigation.LastVisitedTag,
                    CST_Data = AGV.Parameters.AgvType == clsEnums.AGV_TYPE.INSPECTION_AGV ? "" : (AGV as SubmarinAGV)?.CSTReader.ValidCSTID,
                    BatteryStatus = GetBatteryStatusVM(),
                    Pose = AGV.Navigation.Data.robotPose.pose,
                    Angle = AGV.SickData.HeadingAngle,
                    Mileage = AGV.Odometry,
                    BCR_State_MoveBase = AGV.BarcodeReader.Data,
                    AlarmCodes = AlarmManager.CurrentAlarms.Values.ToArray(),
                    MapComparsionRate = AGV.SickData.MapSocre,
                    LocStatus = AGV.SickData.Data.loc_status,
                    AGV_Direct = AGV.Navigation.Direction.ToString().ToUpper(),
                    LinearSpeed = AGV.Navigation.LinearSpeed,
                    AngularSpeed = AGV.Navigation.AngularSpeed,
                    DriversStates = driverStates.ToArray(),
                    Laser_Mode = (int)AGV.Laser.Mode,
                    NavInfo = new NavStateVM
                    {
                        Destination = AGV.ExecutingTaskModel == null && !AGV.IsWaitForkNextSegmentTask ? "" : AGV._RunTaskData.Destination + "",
                        DestinationMapPoint = AGV.Sub_Status != clsEnums.SUB_STATUS.RUN && !AGV.IsWaitForkNextSegmentTask ? new MapPoint { Name = "" } : AGV.DestinationMapPoint,
                        Speed_max_limit = AGV.AGVC.CurrentSpeedLimit,
                        PathPlan = AGV.Sub_Status != clsEnums.SUB_STATUS.RUN ? new int[0] : AGV.ExecutingTaskModel == null ? new int[0] : AGV.ExecutingTaskModel.RunningTaskData.ExecutingTrajecory.GetRemainPath(AGV.Navigation.LastVisitedTag),
                        IsSegmentTaskExecuting = AGV.IsWaitForkNextSegmentTask

                    },
                    Current_LASER_MODE = AGV.Laser.Mode.ToString() + $"({(int)AGV.Laser.CurrentLaserModeOfSick})",
                    ZAxisDriverState = AGV.VerticalDriverState.StateData == null ? new DriverState() : AGV.VerticalDriverState.StateData as DriverState,
                    IsLaserModeSettingError = AGV.Laser.SickSsystemState.application_error,
                    ForkHasLoading = AGV.HasAnyCargoOnAGV(),
                    CargoExist = AGV.HasAnyCargoOnAGV(),
                    HandShakeSignals = new
                    {
                        EQ = AGV.EQHsSignalStates,
                        AGV = AGV.AGVHsSignalStates
                    },
                    HandShakeTimers = AGV.EQHSTimers,
                    SysLoading = new AGVCStatusVM.clsSysLoading
                    {
                        Memory = LinuxTools.Memory
                    },
                    HandshakeStatus = new AGVCStatusVM.clsEQHandshake
                    {
                        ConnectionType = AGV.Parameters.EQHandshakeMethod,
                        Connected = AGV.Parameters.EQHandshakeMethod != Vehicle.EQ_HS_METHOD.MODBUS ? true : StaStored.ConnectingEQHSModbus.Connected
                    },
                    OrderInfo = AGV.orderInfoViewModel

                };
                return data_view_model;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex.Message, ex);
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
                    ChargeCurrent = bat.Value.Data.chargeCurrent,
                    IsCharging = bat.Value.IsCharging(),
                    IsError = bat.Value.CurrentAlarmState == CarComponent.STATE.ABNORMAL,
                    CircuitOpened = AGV.WagoDO.GetState(DO_ITEM.Recharge_Circuit),
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
            ConnectionStateVM data_view_model = new ConnectionStateVM()
            {
                RosbridgeServer = AGV.AGVC.IsConnected() ? ConnectionStateVM.CONNECTION.CONNECTED : ConnectionStateVM.CONNECTION.DISCONNECT,
                VMS = AGV.AGVS.IsConnected() ? ConnectionStateVM.CONNECTION.CONNECTED : ConnectionStateVM.CONNECTION.DISCONNECT,
                WAGO = AGV.WagoDI.IsConnected() ? ConnectionStateVM.CONNECTION.CONNECTED : ConnectionStateVM.CONNECTION.DISCONNECT,
            };
            return data_view_model;
        }

        internal static DIOTableVM GetDIOTableVM()
        {
            return new DIOTableVM
            {
                Inputs = AGV.WagoDI.VCSInputs.ToList(),
                Outputs = AGV.WagoDO.VCSOutputs.ToList(),
                IsE84HsUseEmulator = AGV.Parameters.EQHandshakeMethod == Vehicle.EQ_HS_METHOD.EMULATION,
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
