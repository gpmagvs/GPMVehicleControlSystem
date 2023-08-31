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
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using AGVSystemCommonNet6.AGVDispatch.Model;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.WorkStation;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private void CommonEventsRegist()
        {
            AlarmManager.OnUnRecoverableAlarmOccur += AlarmManager_OnUnRecoverableAlarmOccur;
            AGVSMessageFactory.OnWebAPIProtocolGetRunningStatus += HandleWebAPIProtocolGetRunningStatus;
            AGVSMessageFactory.OnTcpIPProtocolGetRunningStatus += HandleTcpIPProtocolGetRunningStatus;
            Navigation.OnDirectionChanged += Navigation_OnDirectionChanged;
            Navigation.OnLastVisitedTagUpdate += HandleLastVisitedTagChanged;
            BarcodeReader.OnTagLeave += OnTagLeaveHandler;

        }

        /// <summary>
        /// 註冊DIO狀態變化事件
        /// </summary>
        protected virtual void DIOStatusChangedEventRegist()
        {
            WagoDI.OnEMO += EMOPushedHandler;
            WagoDI.OnBumpSensorPressed += WagoDI_OnBumpSensorPressed;
            WagoDI.OnResetButtonPressed += async (s, e) => await ResetAlarmsAsync(true);
            WagoDI.SubsSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_3, HandleSideLaserSignal);
            WagoDI.SubsSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_3, HandleSideLaserSignal);
            WagoDI.SubsSignalStateChange(DI_ITEM.FrontProtection_Area_Sensor_1, HandleLaserArea1SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.BackProtection_Area_Sensor_1, HandleLaserArea1SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.FrontProtection_Area_Sensor_2, HandleLaserArea2SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.BackProtection_Area_Sensor_2, HandleLaserArea2SinalChange);

            WagoDI.SubsSignalStateChange(DI_ITEM.FrontProtection_Area_Sensor_3, HandleLaserArea3SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.BackProtection_Area_Sensor_3, HandleLaserArea3SinalChange);

            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_L_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ] = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_U_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ] = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_READY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_READY] = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_BUSY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY] = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_1, HandleWheelDriverStatusError);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_2, HandleWheelDriverStatusError);

            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_VALID, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_TR_REQ, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_READY, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_READY] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_BUSY, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_COMPT, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT] = state; });

            if (EQ_HS_Method == EQ_HS_METHOD.EMULATION)
            {
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_L_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ] = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_U_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ] = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_READY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_READY] = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_BUSY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY] = state; });
                LOG.INFO($"Handshake emulation mode, regist DO 0-6 ad PIO EQ Inputs ");
            }
        }

        /// <summary>
        /// 處理雷射第一段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleLaserArea1SinalChange(object? sender, bool e)
        {
            if (Operation_Mode == OPERATOR_MODE.MANUAL)
                return;
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;
            clsIOSignal diState = (clsIOSignal)sender;
            if (!diState.State && (diState.Input == DI_ITEM.FrontProtection_Area_Sensor_1 ? !WagoDO.GetState(DO_ITEM.Front_LsrBypass) : !WagoDO.GetState(DO_ITEM.Back_LsrBypass)))
            {

                LOG.INFO($"第一段雷射Trigger.ROBOT_CONTROL_CMD.DECELERATE");
                AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.DECELERATE);
            }
            else
            {

                if (IsAllLaserNoTrigger())
                {
                    LOG.INFO($"第一段雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.SPEED_Reconvery);
                    AGVStatusChangeToRunWhenLaserRecovery();
                }
            }
        }

        /// <summary>
        /// 處理雷射第二段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleLaserArea2SinalChange(object? sender, bool e)
        {
            if (Operation_Mode == OPERATOR_MODE.MANUAL)
                return;
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;

            clsIOSignal diState = (clsIOSignal)sender;
            if (!diState.State && (diState.Input == DI_ITEM.FrontProtection_Area_Sensor_2 ? !WagoDO.GetState(DO_ITEM.Front_LsrBypass) : !WagoDO.GetState(DO_ITEM.Back_LsrBypass)))
            {
                LOG.INFO($"第二段雷射Trigger.ROBOT_CONTROL_CMD.STOP");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);
                AlarmManager.AddAlarm(diState.Input == DI_ITEM.FrontProtection_Area_Sensor_2 ? AlarmCodes.FrontProtection_Area2 : AlarmCodes.BackProtection_Area2);
                AGVStatusChangeToAlarmWhenLaserTrigger();
            }
            else
            {
                if (WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3))
                {
                    LOG.INFO($"第二段雷射恢復.ROBOT_CONTROL_CMD.DECELERATE");
                    AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
                    AGVStatusChangeToRunWhenLaserRecovery();
                }
            }
        }

        /// <summary>
        ///  處理雷射第三段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="di_state"></param>
        private void HandleLaserArea3SinalChange(object? sender, bool di_state)
        {
            if (Operation_Mode == OPERATOR_MODE.MANUAL)
                return;
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;
            clsIOSignal diState = (clsIOSignal)sender;
            if (!di_state)
            {
                if (diState.Input == DI_ITEM.FrontProtection_Area_Sensor_3 && !WagoDO.GetState(DO_ITEM.Front_LsrBypass))
                {
                    AlarmManager.AddAlarm(AlarmCodes.FrontProtection_Area3);
                }
                if (diState.Input == DI_ITEM.BackProtection_Area_Sensor_3 && !WagoDO.GetState(DO_ITEM.Back_LsrBypass))
                {
                    AlarmManager.AddAlarm(AlarmCodes.BackProtection_Area3);
                }
            }
            else
            {
                AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area3);
            }
        }
        private void HandleSideLaserSignal(object? sender, bool di_state)
        {
            if (Operation_Mode == OPERATOR_MODE.MANUAL)
                return;
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;
            clsIOSignal diState = (clsIOSignal)sender;
            bool IsRightLaser = diState.Input == DI_ITEM.RightProtection_Area_Sensor_3;
            bool IsLeftLaser = diState.Input == DI_ITEM.LeftProtection_Area_Sensor_3;
            bool IsRightLsrBypass = WagoDO.GetState(DO_ITEM.Right_LsrBypass);
            bool IsLeftLsrBypass = WagoDO.GetState(DO_ITEM.Left_LsrBypass);

            if (IsRightLaser && IsRightLsrBypass)
                return;
            if (IsLeftLaser && IsLeftLsrBypass)
                return;

            if (!di_state)
            {
                if (IsRightLaser)
                {
                    AlarmManager.AddAlarm(AlarmCodes.RightProtection_Area3);
                    AGVStatusChangeToAlarmWhenLaserTrigger();
                }

                if (IsLeftLaser)
                {
                    AlarmManager.AddAlarm(AlarmCodes.LeftProtection_Area3);
                    AGVStatusChangeToAlarmWhenLaserTrigger();
                }
            }
            else
            {
                if (WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3))
                {
                    LOG.INFO($"側邊雷射雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                    AGVStatusChangeToRunWhenLaserRecovery();
                }
            }
        }


        private void AGVStatusChangeToRunWhenLaserRecovery()
        {
            AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area2);
            AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area3);
            AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area2);
            AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area3);
            AlarmManager.ClearAlarm(AlarmCodes.RightProtection_Area3);
            AlarmManager.ClearAlarm(AlarmCodes.LeftProtection_Area3);

            _Sub_Status = SUB_STATUS.RUN;
            StatusLighter.RUN();
            if (ExecutingTask.action == ACTION_TYPE.None)
                BuzzerPlayer.Move();
            else
                BuzzerPlayer.Action();
        }
        private void AGVStatusChangeToAlarmWhenLaserTrigger()
        {
            _Sub_Status = SUB_STATUS.ALARM;
            BuzzerPlayer.Alarm();
            StatusLighter.DOWN();
        }

        protected void HandleWheelDriverStatusError(object? sender, bool status)
        {
            if (status)
            {
                clsIOSignal signal = (clsIOSignal)sender;
                var input = signal?.Input;
                if (input == DI_ITEM.Horizon_Motor_Alarm_1)
                    AlarmManager.AddAlarm(AlarmCodes.Wheel_Motor_IO_Error_Left, false);
                if (input == DI_ITEM.Horizon_Motor_Alarm_2)
                    AlarmManager.AddAlarm(AlarmCodes.Wheel_Motor_IO_Error_Right, false);
                if (input == DI_ITEM.Vertical_Motor_Alarm)
                    AlarmManager.AddAlarm(AlarmCodes.Vertical_Motor_IO_Error, false);
            }
        }

        private DateTime previousSoftEmoTime = DateTime.MinValue;
        private async void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, EventArgs e)
        {

            _ = Task.Factory.StartNew(async () =>
            {
                SoftwareEMO();
                if (Remote_Mode == REMOTE_MODE.ONLINE)
                    await Online_Mode_Switch(REMOTE_MODE.OFFLINE);
            });

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="taskDownloadData"></param>
        /// <returns></returns>
        internal TASK_DOWNLOAD_RETURN_CODES AGVSTaskDownloadConfirm(clsTaskDownloadData taskDownloadData)
        {

            TASK_DOWNLOAD_RETURN_CODES returnCode = TASK_DOWNLOAD_RETURN_CODES.OK;
            AGV_Reset_Flag = false;

            var action_type = taskDownloadData.Action_Type;

            if (Sub_Status == SUB_STATUS.DOWN) //TODO More Status Confirm when recieve AGVS Task
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_STATUS_DOWN;
            if (BarcodeReader.CurrentTag == 0) //不在Tag上
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_NOT_ON_TAG;
            if (Batteries.Average(bat => bat.Value.Data.batteryLevel) < 10)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_BATTERY_LOW_LEVEL;
            if (taskDownloadData.Destination % 2 == 0 && action_type == ACTION_TYPE.None)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_GO_TO_WORKSTATION_WITH_NORMAL_MOVE_ACTION;
            if (action_type == ACTION_TYPE.Load | action_type == ACTION_TYPE.Unload | action_type == ACTION_TYPE.Park | action_type == ACTION_TYPE.Charge | action_type == ACTION_TYPE.LoadAndPark)
            {
                if (!WorkStations.Stations.TryGetValue(taskDownloadData.Destination, out clsWorkStationData workstation_data))
                {
                    returnCode = TASK_DOWNLOAD_RETURN_CODES.WORKSTATION_NOT_SETTING_YET;
                }
                else
                {

                }
            }

            LOG.INFO($"Check Status When AGVS Taskdownload, Return Code:{returnCode}({(int)returnCode})");
            return returnCode;
        }

        internal bool AGVSTaskResetReqHandle(RESET_MODE mode, bool normal_state = false)
        {
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
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
            if (AGVC.ActionStatus == ActionStatus.ACTIVE && ExecutingTask.action == ACTION_TYPE.None)
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
            string sender_ = sender.ToString();
            _Sub_Status = SUB_STATUS.DOWN;
            InitializeCancelTokenResourece.Cancel();
            if ((DateTime.Now - previousSoftEmoTime).TotalSeconds > 2)
            {
                BuzzerPlayer.Alarm();
                StatusLighter.DOWN();
                AlarmManager.AddAlarm(sender_ == "software_emo" ? AlarmCodes.SoftwareEMS : AlarmCodes.EMO_Button);
                ExecutingTask?.Abort();
                AGVC.AbortTask();
                if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                {
                    FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                }
                if (Remote_Mode == REMOTE_MODE.ONLINE)
                    HandleRemoteModeChangeReq(REMOTE_MODE.OFFLINE);
                DirectionLighter.CloseAll();
                DOSettingWhenEmoTrigger();
                IsInitialized = false;
            }
            AGVC._ActionStatus = ActionStatus.NO_GOAL;
            previousSoftEmoTime = DateTime.Now;

        }

        protected virtual async Task DOSettingWhenEmoTrigger()
        {
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
        }

        private void WagoDI_OnBumpSensorPressed(object? sender, EventArgs e)
        {
            AlarmManager.AddAlarm(AlarmCodes.Bumper, true);
        }

        /// <summary>
        /// 處理車控發佈的 Moduleinformation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="_ModuleInformation"></param>
        protected virtual void ModuleInformationHandler(object? sender, ModuleInformation _ModuleInformation)
        {
            Task.Factory.StartNew(() =>
            {
                Odometry = _ModuleInformation.Mileage;
                Navigation.StateData = _ModuleInformation.nav_state;

                IMU.StateData = _ModuleInformation.IMU;
                GuideSensor.StateData = _ModuleInformation.GuideSensor;
                BarcodeReader.StateData = _ModuleInformation.reader;
                VerticalDriverState.StateData = _ModuleInformation.Action_Driver;

                for (int i = 0; i < _ModuleInformation.Wheel_Driver.driversState.Length; i++)
                    WheelDrivers[i].StateData = _ModuleInformation.Wheel_Driver.driversState[i];

                var _lastVisitedMapPoint = NavingMap == null ? new AGVSystemCommonNet6.MAP.MapPoint
                {
                    Name = Navigation.LastVisitedTag.ToString(),
                    TagNumber = Navigation.LastVisitedTag
                } : NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == this.Navigation.LastVisitedTag);
                lastVisitedMapPoint = _lastVisitedMapPoint == null ? new AGVSystemCommonNet6.MAP.MapPoint() { Name = "Unknown" } : _lastVisitedMapPoint;

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
                IsCharging = Batteries.Values.Any(battery => battery.IsCharging);
            });

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
