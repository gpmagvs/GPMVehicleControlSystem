using static GPMVehicleControlSystem.ViewModels.ForkTestVM;
using GPMVehicleControlSystem.Models.VehicleControl;
using GPMVehicleControlSystem.Models;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Abstracts;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using AGVSystemCommonNet6;
using GPMVehicleControlSystem.Models.VCSSystem;

namespace GPMVehicleControlSystem.ViewModels
{
    public class ViewModelFactory
    {
        internal static Vehicle AGV => StaStored.CurrentVechicle;

        internal static AGVCStatusVM GetVMSStatesVM()
        {

            try
            {
                List<DriverState> driverStates = new List<DriverState>();
                driverStates.AddRange(AGV.WheelDrivers.Select(d => d.Data).ToArray());

                AGVCStatusVM data_view_model = new AGVCStatusVM()
                {
                    APPVersion = StaStored.APPVersion,
                    Agv_Type = AGV.AgvType,
                    Simulation = AGV.SimulationMode,
                    AutoMode = AGV.Operation_Mode,
                    OnlineMode = AGV.Remote_Mode,
                    IsInitialized = AGV.IsInitialized,
                    IsSystemIniting = !AGV.IsSystemInitialized,
                    AGVC_ID = AGV.SID,
                    CarName = AGV.CarName,
                    MainState = AGV.Main_Status.ToString(),
                    SubState = AGV.Sub_Status.ToString(),
                    Tag = AGV.BarcodeReader.CurrentTag,
                    Last_Visit_MapPoint = AGV.lastVisitedMapPoint,
                    Last_Visited_Tag = AGV.Navigation.LastVisitedTag,
                    CST_Data = AGV.AgvType == clsEnums.AGV_TYPE.INSPECTION_AGV ? "" : (AGV as SubmarinAGV)?.CSTReader.ValidCSTID,
                    BatteryStatus = AGV.Batteries.Count == 0 ? new BatteryStateVM[1]
                    {
                        new BatteryStateVM
                        {
                        BatteryLevel = 66
                        }
                    } :
                    AGV.Batteries.Values.Select(bat => new BatteryStateVM
                    {
                        BatteryLevel = bat.Data.batteryLevel,
                        ChargeCurrent = bat.Data.chargeCurrent,
                        IsCharging = bat.Data.chargeCurrent != 0,
                        IsError = bat.State == CarComponent.STATE.ABNORMAL,
                        CircuitOpened = AGV.WagoDO.GetState(DO_ITEM.Recharge_Circuit),
                        BatteryID = bat.Data.batteryID

                    }).ToArray(),
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
                        Destination = AGV.ExecutingTask == null ? "" : AGV.ExecutingTask.RunningTaskData.Destination + "",
                        DestinationMapPoint = AGV.DestinationMapPoint,
                        Speed_max_limit = AGV.AGVC.CurrentSpeedLimit,
                        PathPlan = AGV.Sub_Status != clsEnums.SUB_STATUS.RUN ? new int[0] : AGV.ExecutingTask == null ? new int[0] : AGV.ExecutingTask.RunningTaskData.ExecutingTrajecory.GetRemainPath(AGV.Navigation.LastVisitedTag)
                    },
                    Current_LASER_MODE = AGV.Laser.Mode.ToString(),

                };
                return data_view_model;
            }
            catch (Exception ex)
            {
                return new AGVCStatusVM()
                {
                    AlarmCodes = new clsAlarmCode[]
                    {
                        new clsAlarmCode
                        {
                            Time=DateTime.Now,
                             Code =  (int)AlarmCodes.AGVS_Disconnect,
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


        internal static ConnectionStateVM GetConnectionStatesVM()
        {
            if (AGV.SimulationMode)
            {
                return new ConnectionStateVM
                {
                    AGVC = ConnectionStateVM.CONNECTION.CONNECTED,
                    RosbridgeServer = ConnectionStateVM.CONNECTION.CONNECTED,
                    WAGO = ConnectionStateVM.CONNECTION.CONNECTED,
                };
            }

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
                IsE84HsUseEmulator = AGV.EQ_HS_Method == Vehicle.EQ_HS_METHOD.EMULATION
            };
        }

        internal static object GetSystemMessagesVM()
        {
            return StaSysMessageManager.SysMessages;
        }
    }
}
