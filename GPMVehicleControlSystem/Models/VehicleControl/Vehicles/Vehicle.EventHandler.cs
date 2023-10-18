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
using System.Reflection.Metadata;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private void CommonEventsRegist()
        {
            BuzzerPlayer.OnBuzzerPlay += () => { return Parameters.BuzzerOn; };
            AlarmManager.OnUnRecoverableAlarmOccur += AlarmManager_OnUnRecoverableAlarmOccur;
            AGVSMessageFactory.OnWebAPIProtocolGetRunningStatus += HandleWebAPIProtocolGetRunningStatus;
            AGVSMessageFactory.OnTcpIPProtocolGetRunningStatus += HandleTcpIPProtocolGetRunningStatus;
            Navigation.OnDirectionChanged += Navigation_OnDirectionChanged;
            Navigation.OnLastVisitedTagUpdate += HandleLastVisitedTagChanged;
            BarcodeReader.OnTagLeave += OnTagLeaveHandler;
            DirectionLighter.OnAGVDirectionChangeToForward += () =>
            {
                return Parameters.FrontLighterFlashWhenNormalMove;
            };
        }

        /// <summary>
        /// 註冊DIO狀態變化事件
        /// </summary>
        protected virtual void DIOStatusChangedEventRegist()
        {

            WagoDI.OnDisonnected += WagoDI_OnDisonnected;
            WagoDI.OnReConnected += WagoDI_OnReConnected;
            WagoDI.OnEMO += EMOTriggerHandler;
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
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_GO, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_GO] = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_1, HandleWheelDriverStatusError);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_2, HandleWheelDriverStatusError);

            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_VALID, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_TR_REQ, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_READY, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_READY] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_BUSY, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_COMPT, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT] = state; });

            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_L_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ] = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_U_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ] = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_READY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_READY] = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_BUSY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY] = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_GO, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_GO] = state; });
                LOG.INFO($"Handshake emulation mode, regist DO 0-6 ad PIO EQ Inputs ");
            }
        }

        private void WagoDI_OnReConnected(object? sender, EventArgs e)
        {
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;

            if (WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3))
            {
                LOG.WARN("AGV Executing Task and Wago Module Reconnected,and No Obstacle,Send Complex Control Speed Reconvery");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
            }
        }

        private void WagoDI_OnDisonnected(object? sender, EventArgs e)
        {
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;
            LOG.WARN("AGV Executing Task but Wago Module Disconnect,Send Complex Control STOP => AGV STOP");
            AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);
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
            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_1;

            if (!diState.State && (isFrontLaser ? !WagoDO.GetState(DO_ITEM.Front_LsrBypass) : !WagoDO.GetState(DO_ITEM.Back_LsrBypass)))
            {
                LOG.INFO($"第一段雷射Trigger.ROBOT_CONTROL_CMD.DECELERATE");
                AlarmManager.AddWarning(isFrontLaser ? AlarmCodes.FrontProtection_Area2 : AlarmCodes.BackProtection_Area2);
                AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.DECELERATE);
            }
            else
            {
                IsLaserRecoveryHandled = false;
                if (IsAllLaserNoTrigger())
                {
                    LOG.INFO($"第一段雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    Task.Factory.StartNew(() =>
                    {
                        AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                    });
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
                AlarmManager.AddAlarm(diState.Input == DI_ITEM.FrontProtection_Area_Sensor_2 ? AlarmCodes.FrontProtection_Area3 : AlarmCodes.BackProtection_Area3);
                AGVStatusChangeToAlarmWhenLaserTrigger();
            }
            else
            {
                IsLaserRecoveryHandled = false;
                if (WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3))
                {
                    LOG.INFO($"第二段雷射恢復.ROBOT_CONTROL_CMD.DECELERATE");
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.DECELERATE);
                }
            }
        }

        /// <summary>
        ///  處理雷射第三段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="di_state"></param>
        protected virtual void HandleLaserArea3SinalChange(object? sender, bool di_state)
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
                IsLaserRecoveryHandled = false;
                AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area3);
            }
        }
        private async void HandleSideLaserSignal(object? sender, bool di_state)
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
                await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);

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
                IsLaserRecoveryHandled = false;
                if (WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3))
                {
                    LOG.INFO($"側邊雷射雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                }
            }
        }

        private bool IsLaserRecoveryHandled = false;
        private async void AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD speed_control)
        {
            await Task.Delay(1000);

            if (WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) && WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3))
            {
                await AGVC.CarSpeedControl(speed_control);
                AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area2);
                AlarmManager.ClearAlarm(AlarmCodes.FrontProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area2);
                AlarmManager.ClearAlarm(AlarmCodes.BackProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.RightProtection_Area3);
                AlarmManager.ClearAlarm(AlarmCodes.LeftProtection_Area3);
                if (AGVC.ActionStatus == ActionStatus.ACTIVE && !IsLaserRecoveryHandled)
                {
                    IsLaserRecoveryHandled = true;
                    LOG.WARN($"No obstacle. Running");
                    _Sub_Status = SUB_STATUS.RUN;
                    StatusLighter.RUN();
                    try
                    {
                        if (ExecutingTaskModel == null)
                        {
                        }
                        else
                        {
                            if (ExecutingTaskModel.action == ACTION_TYPE.None)
                            {
                                LOG.WARN($"No obstacle.  buzzer Move");
                                BuzzerPlayer.Move();
                            }
                            else
                            {
                                LOG.WARN($"No obstacle.  buzzer Action");
                                BuzzerPlayer.Action();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LOG.ERROR(ex);
                    }

                }
            }
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

        protected DateTime previousSoftEmoTime = DateTime.MinValue;
        protected virtual async void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, AlarmCodes alarm_code)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                SoftwareEMO(alarm_code);

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
            AGV_Reset_Flag = AGVSResetCmdFlag = false;

            var action_type = taskDownloadData.Action_Type;

            if (Sub_Status == SUB_STATUS.DOWN) //TODO More Status Confirm when recieve AGVS Task
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_STATUS_DOWN;

            if (Batteries.Average(bat => bat.Value.Data.batteryLevel) < 10)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_BATTERY_LOW_LEVEL;
            if (Parameters.AgvType != AGV_TYPE.INSPECTION_AGV && taskDownloadData.Destination % 2 == 0 && action_type == ACTION_TYPE.None)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_GO_TO_WORKSTATION_WITH_NORMAL_MOVE_ACTION;

            LOG.INFO($"Check Status When AGVS Taskdownload, Return Code:{returnCode}({(int)returnCode})");
            return returnCode;
        }

        /// <summary>
        /// 處理任務取消請求
        /// </summary>
        /// <param name="mode">取消模式</param>
        /// <param name="normal_state"></param>
        /// <returns></returns>
        internal async Task<bool> HandleAGVSTaskCancelRequest(RESET_MODE mode, bool normal_state = false)
        {
            if (AGVSResetCmdFlag)
                return true;
            AGVSResetCmdFlag = true;
            try
            {
                LOG.WARN($"AGVS TASK Cancel Request ({mode}),Current Action Status={AGVC.ActionStatus}, AGV SubStatus = {Sub_Status}");

                if (AGVC.ActionStatus != ActionStatus.ACTIVE && AGVC.ActionStatus != ActionStatus.PENDING && mode == RESET_MODE.CYCLE_STOP)
                {
                    AGVC.OnAGVCActionChanged = null;
                    AGV_Reset_Flag = false;
                    LOG.WARN($"AGVS TASK Cancel Request ({mode}),But AGV is stopped.(IDLE)");
                    await AGVC.SendGoal(new AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal());//下空任務清空
                    AGVC._ActionStatus = ActionStatus.NO_GOAL;
                    AGV_Reset_Flag = true;
                    Sub_Status = SUB_STATUS.IDLE;
                    return true;
                }
                else
                {
                    bool result = await AGVC.ResetTask(mode);
                    if (mode == RESET_MODE.ABORT)
                    {
                        _ = Task.Factory.StartNew(async () =>
                        {
                            if (!normal_state)
                            {
                                AlarmManager.AddAlarm(AlarmCodes.AGVs_Abort_Task);
                                Sub_Status = SUB_STATUS.DOWN;
                            }
                            await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: AlarmCodes.AGVs_Abort_Task);
                            ExecutingTaskModel.Abort();
                        });
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                if (mode == RESET_MODE.CYCLE_STOP)
                    AlarmManager.AddAlarm(AlarmCodes.Exception_When_AGVC_AGVS_Task_Reset_CycleStop, false);
                else
                    AlarmManager.AddAlarm(AlarmCodes.Exception_When_AGVC_AGVS_Task_Reset_Abort, false);
                return false;
            }

        }

        private void Navigation_OnDirectionChanged(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            //方向燈
            LOG.TRACE($"AGV Direction changed to = {direction}");
            DirectionLighter.LightSwitchByAGVDirection(sender, direction);
            if (AGVC.ActionStatus == ActionStatus.ACTIVE)
            {
                //雷射
                if (direction != clsNavigation.AGV_DIRECTION.STOP)
                    Laser.LaserChangeByAGVDirection(sender, direction);
            }
        }

        protected virtual void EMOTriggerHandler(object? sender, EventArgs e)
        {
            SoftwareEMO(AlarmCodes.EMS);
        }


        protected virtual async Task DOSettingWhenEmoTrigger()
        {
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
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
        protected virtual async void ModuleInformationHandler(object? sender, ModuleInformation _ModuleInformation)
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
                    StateData = _ModuleInformation.Battery,
                });
            }
            Batteries = Batteries.ToList().FindAll(b => b.Value != null).ToDictionary(b => b.Key, b => b.Value);
            if (Parameters.AgvType != AGV_TYPE.INSPECTION_AGV)
                IsCharging = Batteries.Values.Any(battery => battery.IsCharging());
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
