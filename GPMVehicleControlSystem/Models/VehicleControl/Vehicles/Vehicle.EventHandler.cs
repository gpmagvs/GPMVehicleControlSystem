using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using static AGVSystemCommonNet6.clsEnums;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Alarm;
using System.Security.Claims;
using GPMVehicleControlSystem.Models.Buzzer;
using RosSharp.RosBridgeClient.Actionlib;
using AGVSystemCommonNet6.MAP;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private void AGVCTaskAbortedHandle(object? sender, clsTaskDownloadData e)
        {
            if (Navigation.Current_Warning_Code != AlarmCodes.None)
            {
                AGVC.AbortTask();
                ExecutingTask.Abort();
                Sub_Status = SUB_STATUS.DOWN;
                FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
            }
        }

        private async void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, EventArgs e)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                if (Remote_Mode == REMOTE_MODE.ONLINE)
                    await Online_Mode_Switch(REMOTE_MODE.OFFLINE);
            });
        }

        private void NearLaserTriggerHandler(object? sender, EventArgs e)
        {
            if (!AGVC.IsAGVExecutingTask)
                return;

            if (Operation_Mode == OPERATOR_MODE.AUTO && AGVC.IsAGVExecutingTask)
            {
                AGVC.NearLaserTriggerHandler(sender, e);
                Sub_Status = SUB_STATUS.ALARM;
                clsIOSignal LaserSignal = sender as clsIOSignal;
                DI_ITEM LaserType = LaserSignal.Input;
                AlarmCodes alarm_code = GetAlarmCodeByLsrDI(LaserType);
                if (alarm_code != AlarmCodes.None)
                    AlarmManager.AddAlarm(alarm_code, true);
            }
        }

        private static AlarmCodes GetAlarmCodeByLsrDI(DI_ITEM LaserType)
        {
            AlarmCodes alarm_code = AlarmCodes.None;
            if (LaserType == DI_ITEM.RightProtection_Area_Sensor_2)
                alarm_code = AlarmCodes.RightProtection_Area3;
            if (LaserType == DI_ITEM.LeftProtection_Area_Sensor_2)
                alarm_code = AlarmCodes.LeftProtection_Area3;

            if (LaserType == DI_ITEM.FrontProtection_Area_Sensor_2 | LaserType == DI_ITEM.FrontProtection_Area_Sensor_3)
                alarm_code = AlarmCodes.FrontProtection_Area3;
            if (LaserType == DI_ITEM.BackProtection_Area_Sensor_2 | LaserType == DI_ITEM.BackProtection_Area_Sensor_3)
                alarm_code = AlarmCodes.BackProtection_Area3;
            return alarm_code;
        }

        private void FarLaserTriggerHandler(object? sender, EventArgs e)
        {
            if (!AGVC.IsAGVExecutingTask)
                return;

            AGVC.FarLaserTriggerHandler(sender, e);

        }

        private void LaserRecoveryHandler(object? sender, ROBOT_CONTROL_CMD cmd)
        {

            AGVC.LaserRecoveryHandler(sender, cmd);
            if ((cmd == ROBOT_CONTROL_CMD.NONE))
                return;

            if (!AGVC.IsAGVExecutingTask)
                return;


            clsIOSignal LaserSignal = sender as clsIOSignal;
            DI_ITEM LaserType = LaserSignal.Input;

            if (cmd == ROBOT_CONTROL_CMD.SPEED_Reconvery)
            {
                AlarmManager.ClearAlarm(AlarmCodes.RightProtection_Area2);
                AlarmManager.ClearAlarm(AlarmCodes.RightProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.LeftProtection_Area2);
                AlarmManager.ClearAlarm(AlarmCodes.LeftProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area2);
                AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area2);
                AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area3);
            }
            if (Operation_Mode != OPERATOR_MODE.AUTO)
                return;
            if (!AGVC.IsAGVExecutingTask)
                return;

            if (LaserType != DI_ITEM.FrontProtection_Area_Sensor_3 && LaserType != DI_ITEM.BackProtection_Area_Sensor_3)
            {
                _Sub_Status = SUB_STATUS.RUN;
                if (ExecutingTask.action == ACTION_TYPE.None)
                    BuzzerPlayer.Move();
                else
                    BuzzerPlayer.Action();
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="taskDownloadData"></param>
        /// <returns></returns>
        internal bool AGVSTaskDownloadConfirm(clsTaskDownloadData taskDownloadData)
        {
            AGV_Reset_Flag = false;

            if (Main_Status == MAIN_STATUS.DOWN) //TODO More Status Confirm when recieve AGVS Task
                return false;

            return true;
        }

        internal bool AGVSTaskResetReqHandle(RESET_MODE mode, bool normal_state = false)
        {
            if (!AGVC.IsAGVExecutingTask)
                return true;
            AGV_Reset_Flag = true;
            Task.Factory.StartNew(async () =>
            {
                AGVC.AbortTask(mode);
                if (mode == RESET_MODE.ABORT)
                {
                    if (!normal_state)
                    {
                        AlarmManager.AddAlarm(AlarmCodes.AGVs_Abort_Task);
                        Sub_Status = SUB_STATUS.DOWN;
                    }
                    await Task.Delay(500);
                    await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                    ExecutingTask.Abort();
                }
            });

            return true;
        }

        private void Navigation_OnDirectionChanged(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            if (AGVC.IsAGVExecutingTask && ExecutingTask.action == ACTION_TYPE.None)
            {
                //方向燈
                DirectionLighter.LightSwitchByAGVDirection(sender, direction);
                //雷射
                if (ExecutingTask.action == ACTION_TYPE.None && direction != clsNavigation.AGV_DIRECTION.STOP)
                    Laser.LaserChangeByAGVDirection(sender, direction);
            }
        }

        protected virtual void EMOPushedHandler(object? sender, EventArgs e)
        {
            BuzzerPlayer.Alarm();
            _Sub_Status = SUB_STATUS.DOWN;
            StatusLighter.DOWN();
            AlarmManager.AddAlarm(AlarmCodes.EMO_Button, false);
            AGVC.EMOHandler(sender, e);
            IsInitialized = false;
            ExecutingTask?.Abort();
            HandleRemoteModeChangeReq(REMOTE_MODE.OFFLINE);
            DirectionLighter.CloseAll();
        }

        private void WagoDI_OnBumpSensorPressed(object? sender, EventArgs e)
        {
            AlarmManager.AddAlarm(AlarmCodes.Bumper, false);
        }

        /// <summary>
        /// 處理車控發佈的 Moduleinformation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="_ModuleInformation"></param>
        protected virtual void ModuleInformationHandler(object? sender, ModuleInformation _ModuleInformation)
        {
            if (_ModuleInformation.AlarmCode.Length > 0)
            {
                foreach (var agvc_alarm in _ModuleInformation.AlarmCode)
                {
                    var alarm = AlarmManager.ConvertAGVCAlarmCode(agvc_alarm, out var level);
                    if (level == AGVSystemCommonNet6.Alarm.VMS_ALARM.clsAlarmCode.LEVEL.Warning)
                        AlarmManager.AddWarning(alarm);
                    else
                        AlarmManager.AddAlarm(alarm, false);
                }
            }

            Odometry = _ModuleInformation.Mileage;
            Navigation.StateData = _ModuleInformation.nav_state;

            ushort battery_id = _ModuleInformation.Battery.batteryID;
            if (Batteries.TryGetValue(battery_id, out var battery))
            {
                battery.StateData = _ModuleInformation.Battery;
            }
            else
            {
                Batteries.Add(battery_id, new clsBattery()
                {
                    StateData = _ModuleInformation.Battery
                });
            }

            IMU.StateData = _ModuleInformation.IMU;
            GuideSensor.StateData = _ModuleInformation.GuideSensor;
            BarcodeReader.StateData = _ModuleInformation.reader;
            VerticalDriverState.StateData = _ModuleInformation.Action_Driver;

            for (int i = 0; i < _ModuleInformation.Wheel_Driver.driversState.Length; i++)
                WheelDrivers[i].StateData = _ModuleInformation.Wheel_Driver.driversState[i];

            var _lastVisitedMapPoint = GetLastVisitedMapPoint();
            lastVisitedMapPoint = _lastVisitedMapPoint == null ? new AGVSystemCommonNet6.MAP.MapPoint() { Name = "Unknown" } : _lastVisitedMapPoint;
            IsCharging = Batteries.Values.Any(battery => battery.IsCharging);


        }

        private MapPoint GetLastVisitedMapPoint()
        {
            if (NavingMap == null)
                return new MapPoint
                {
                    Name = Navigation.LastVisitedTag.ToString(),
                    TagNumber = Navigation.LastVisitedTag
                };
            return NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == Navigation.LastVisitedTag);//虛擬點
        }

        private void CarController_OnSickDataUpdated(object? sender, LocalizationControllerResultMessage0502 sick_loc_data)
        {
            SickData.StateData = sick_loc_data;
        }
    }
}
