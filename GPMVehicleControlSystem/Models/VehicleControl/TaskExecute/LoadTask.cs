using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using static AGVSystemCommonNet6.clsEnums;
using System.Diagnostics;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
using System.Formats.Asn1;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsForkLifter;
using GPMVehicleControlSystem.Models.WorkStation;
using RosSharp.RosBridgeClient.Actionlib;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    /// <summary>
    /// 放貨任務
    /// </summary>
    public class LoadTask : TaskBase
    {
        public virtual bool CSTTrigger
        {
            get
            {
                return StaStored.CurrentVechicle.Parameters.CST_READER_TRIGGER;
            }
        }
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Load;

        private WORKSTATION_HS_METHOD eqHandshakeMode
        {
            get
            {
                if (Agv.WorkStations.Stations.TryGetValue(destineTag, out var data))
                {
                    WORKSTATION_HS_METHOD mode = data.HandShakeModeHandShakeMode;
                    LOG.WARN($"[{action}] Tag_{destineTag} Handshake Mode:{mode}({(int)mode})");
                    return mode;
                }
                else
                {
                    LOG.WARN($"[{action}] Tag_{destineTag} Handshake Mode Not Defined! Forcing Handsake to Safty Protection. ");
                    return WORKSTATION_HS_METHOD.HS;
                }
            }
        }

        internal bool back_to_secondary_flag = false;
        private WORKSTATION_HS_METHOD _eqHandshakeMode;

        public LoadTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }
        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                //啟用前後雷射偵測 + Loading 組數
                await Agv.Laser.SideLasersEnable(false);
                await Agv.Laser.FrontBackLasersEnable(false);
                await Task.Delay(200);
                return await Agv.Laser.ModeSwitch(LASER_MODE.Bypass);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return false;
            }
        }

        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            if (Agv.Parameters.CheckEQDOStatusWorkStationTags.Contains(RunningTaskData.Destination))
                CheckEQDIOStates();

            (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckBeforeHSStartInFrontOfEQ();
            if (!CstExistCheckResult.confirm)
                return (false, CstExistCheckResult.alarmCode);

            //(bool confirm, AlarmCodes alarmCode) CstBarcodeCheckResult = await CSTBarcodeReadBeforeAction();

            //if (!CstBarcodeCheckResult.confirm)
            //    return (false, CstBarcodeCheckResult.alarmCode);


            if (eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
            {
                if (Agv.Parameters.EQHandshakeMethod == Vehicle.EQ_HS_METHOD.MODBUS)
                {
                    var eqModbusConn = await Agv.ModbusTcpConnect();
                    if (!eqModbusConn)
                    {
                        return (false, AlarmCodes.Waiting_EQ_Handshake);
                    }
                }

                Agv.ResetHSTimers();
                await Task.Delay(1000);
                if (!Agv.IsEQGOOn())
                    return (false, AlarmCodes.Precheck_IO_Fail_EQ_GO);
                if (Agv.Parameters.PlayHandshakingMusic)
                    BuzzerPlayer.Handshaking();
                else
                    BuzzerPlayer.Action();

                (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyON(action);
                await Task.Delay(1500);
                if (!HSResult.eqready)
                {
                    return (false, HSResult.alarmCode);
                }
            }
            return await base.BeforeTaskExecuteActions();
        }

        private void CheckEQDIOStates()
        {
            if (Agv.EQDIOStates == null)
            {
                LOG.WARN($"無法取得EQ DIO狀態!");
                return;
            }
            if (Agv.EQDIOStates.TryGetValue(RunningTaskData.Destination, out var dio_status))
            {
                if (!dio_status.EQ_Status_Run)
                {
                    LOG.WARN($"EQ DO : EQ_STATUS_RUN Not ON");
                    return;
                }

                if (dio_status.Up_Pose == false && dio_status.Down_Pose == false)
                {
                    LOG.WARN($"EQ DO : EQ LD IN UNKNOWN POSITION.");
                    return;
                }

                if (action == ACTION_TYPE.Load)//放貨
                {
                    if (dio_status.PortExist)
                    {
                        LOG.WARN($"EQ DO : EQ PORT HAS CARGO!");
                        return;
                    }
                    if (dio_status.Up_Pose == true && dio_status.Down_Pose == false)
                    {
                        LOG.WARN($"EQ DO : EQ LD NOT DOWN_POSE");
                        return;
                    }
                    LOG.INFO($"EQ DO Status Check [Load] => OK", color: ConsoleColor.Green);
                }
                else if ((action == ACTION_TYPE.Unload))
                {
                    if (!dio_status.PortExist)
                    {
                        LOG.WARN($"EQ DO : EQ PORT NO CARGO!!");
                        return;
                    }
                    if (dio_status.Up_Pose == false && dio_status.Down_Pose == true)
                    {
                        LOG.WARN($"EQ DO : EQ LD NOT  UP_POSE");
                        return;
                    }
                    LOG.INFO($"EQ DO Status Check [Unload] => OK", color: ConsoleColor.Green);
                }

            }
            else
                LOG.WARN($"無法取得站點 Tag {RunningTaskData.Destination} 的DIO狀態.(Key Not Found..)");
        }

        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            Agv.DirectionLighter.CloseAll();
            (bool hs_success, AlarmCodes alarmCode) HSResult = new(false, AlarmCodes.None);
            _eqHandshakeMode = eqHandshakeMode;
            if (_eqHandshakeMode == WORKSTATION_HS_METHOD.HS )
            {
                HSResult = await Agv.WaitEQBusyOFF(action);
                if (!HSResult.hs_success)
                {
                    Agv.DirectionLighter.CloseAll();
                    return (false, HSResult.alarmCode);
                }
            }
            Agv.DirectionLighter.CloseAll();
            back_to_secondary_flag = false;
            await Task.Delay(1000);

            if (ForkLifter != null)
            {
                bool arm_move_Done = false;
                (bool confirm, string message) armMoveing = (false, "等待DO輸出");
                var isNeedArmExtend = Agv.WorkStations.Stations[destineTag].ForkArmExtend;

                if (isNeedArmExtend)
                {
                    var _arm_move_result = await ForkLifter.ForkExtendOutAsync();
                    arm_move_Done = _arm_move_result.confirm;
                    if (!arm_move_Done)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                    await Task.Delay(1000);
                }
                else
                {
                    arm_move_Done = true;
                }
                if (arm_move_Done)
                {
                    (bool success, AlarmCodes alarm_code) fork_height_change_result = await ChangeForkPositionInWorkStation();
                    if (!fork_height_change_result.success)
                        return (false, AlarmCodes.Fork_Height_Setting_Error);

                    await Task.Delay(1000);
                    if (isNeedArmExtend)
                    {
                        var arm_move_result = await ForkLifter.ForkShortenInAsync();
                        if (!arm_move_result.confirm)
                        {
                            return (false, AlarmCodes.Action_Timeout);
                        }
                        await Task.Delay(1000);
                    }
                }

            }

            //檢查在席
            (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckAfterEQBusyOff();
            if (!CstExistCheckResult.confirm)
                return (false, CstExistCheckResult.alarmCode);
            if (TaskCancelCTS.IsCancellationRequested)
                return (false, AlarmCodes.None);



            //下Homing Trajectory 任務讓AGV退出
            await Task.Factory.StartNew(async () =>
            {
                clsTaskDownloadData NoEntryEQTask = new clsTaskDownloadData()
                {
                    Action_Type = ACTION_TYPE.None,
                    Destination = Agv.Navigation.LastVisitedTag,
                    Task_Name = RunningTaskData.Task_Name,
                    Task_Sequence = RunningTaskData.Task_Sequence,
                    Trajectory = new clsMapPoint[1]
                         {
                           new clsMapPoint()
                           {
                                Point_ID =Agv.lastVisitedMapPoint.TagNumber,
                                 X = Agv.lastVisitedMapPoint.X,
                                 Y=Agv.lastVisitedMapPoint.Y,
                                  Theta = Agv.Navigation.Angle,
                           }
                         }
                };
                Agv.DirectionLighter.Backward(delay: 800);
                //await Agv.Laser.FrontBackLasersEnable(false, true);
                RunningTaskData = RunningTaskData.CreateGoHomeTaskDownloadData();
                Agv.ExecutingTask.RunningTaskData = RunningTaskData;

                AGVCActionStatusChaged += HandleBackToHomeActionStatusChanged;
                await Agv.AGVC.ExecuteTaskDownloaded(Agv.Parameters.LDULD_Task_No_Entry ? NoEntryEQTask : RunningTaskData);

            });


            return (true, AlarmCodes.None);
        }

        private async void HandleBackToHomeActionStatusChanged(ActionStatus status)
        {
            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                AGVCActionStatusChaged -= HandleBackToHomeActionStatusChanged;
                return;
            }
            LOG.WARN($"[ {RunningTaskData.Task_Simplex} -{action}-Back To Secondary Point of WorkStation] AGVC Action Status Changed: {status}.");

            if (status == ActionStatus.SUCCEEDED)
            {
                AGVCActionStatusChaged = null;
                back_to_secondary_flag = true;
                if (_eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
                {
                    (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyOFF(action);
                    if (!HSResult.eqready)
                    {
                        AlarmManager.AddAlarm(HSResult.alarmCode, false);
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                        Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                        return;
                    }
                }

                if (ForkLifter != null)
                {
                    var ForkGoHomeActionResult = await ForkLifter.ForkGoHome();
                    if (!ForkGoHomeActionResult.confirm)
                    {
                        AlarmManager.AddAlarm(ForkGoHomeActionResult.alarm_code);
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                        Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                    }
                }

                (bool confirm, AlarmCodes alarmCode) CstBarcodeCheckResult = await CSTBarcodeReadAfterAction();
                if (!CstBarcodeCheckResult.confirm)
                {
                    AlarmManager.AddAlarm(CstBarcodeCheckResult.alarmCode, false);
                    Agv.Sub_Status = SUB_STATUS.DOWN;
                    Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                }
                base.HandleAGVCActionSucceess();
            }
        }

        protected async virtual Task<(bool success, AlarmCodes alarm_code)> ChangeForkPositionInWorkStation()
        {
            return await ForkLifter.ForkGoTeachedPoseAsync(destineTag, 0, FORK_HEIGHT_POSITION.DOWN_, 0.3);

        }
        private bool WaitForkArmMoveDone(FORK_ARM_LOCATIONS locationExpect)
        {
            CancellationTokenSource timeout_check = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (ForkLifter.CurrentForkARMLocation != locationExpect)
            {
                if (TaskCancelCTS.IsCancellationRequested)
                    return false;
                if (Agv.Sub_Status != SUB_STATUS.RUN)
                    return false;
                Thread.Sleep(1);
                if (timeout_check.IsCancellationRequested)
                    return false;

            }
            return true;
        }

        protected virtual async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeReadBeforeAction()
        {
            if (!CSTTrigger)
                return (true, AlarmCodes.None);
            return await CSTBarcodeRead();
        }

        internal virtual async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeReadAfterAction()
        {

            Agv.CSTReader.ValidCSTID = "";
            //await Agv.AGVC.TriggerCSTReader();
            return (true, AlarmCodes.None);
        }

        protected async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeRead()
        {
            (bool request_success, bool action_done) result = await Agv.AGVC.TriggerCSTReader();
            if (!result.request_success | !result.action_done)
            {
                return (false, AlarmCodes.Read_Cst_ID_Fail);
            }
            if (Agv.CSTReader.Data.data != RunningTaskData.CST.First().CST_ID)
            {
                return (false, AlarmCodes.Cst_ID_Not_Match);
            }
            Agv.CSTReader.ValidCSTID = Agv.CSTReader.Data.data;
            return (true, AlarmCodes.None);
        }



        /// <summary>
        /// Load作業(放貨)=>車上應該有貨
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected virtual (bool confirm, AlarmCodes alarmCode) CstExistCheckBeforeHSStartInFrontOfEQ()
        {
            if (!StaStored.CurrentVechicle.Parameters.CST_EXIST_DETECTION.Before_In)
                return (true, AlarmCodes.None);

            if (!Agv.HasAnyCargoOnAGV())
                return (false, AlarmCodes.Has_Job_Without_Cst);

            return (true, AlarmCodes.None);
        }

        /// <summary>
        /// Load完成(放貨)=>車上應該有無貨
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected virtual (bool confirm, AlarmCodes alarmCode) CstExistCheckAfterEQBusyOff()
        {
            if (!StaStored.CurrentVechicle.Parameters.CST_EXIST_DETECTION.After_EQ_Busy_Off)
                return (true, AlarmCodes.None);

            if (Agv.HasAnyCargoOnAGV())
                return (false, AlarmCodes.Has_Cst_Without_Job);

            Agv.CSTReader.ValidCSTID = "";

            return (true, AlarmCodes.None);
        }

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Forward();
        }

    }
}
