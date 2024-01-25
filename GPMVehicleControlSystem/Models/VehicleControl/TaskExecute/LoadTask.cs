using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using RosSharp.RosBridgeClient.Actionlib;
using System.Threading.Tasks;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsForkLifter;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    /// <summary>
    /// 放貨任務
    /// </summary>
    public class LoadTask : TaskBase
    {
        private ManualResetEvent _WaitBackToHomeDonePause = new ManualResetEvent(false);
        private ActionStatus _BackHomeActionDoneStatus = ActionStatus.PENDING;
        public enum CST_ID_NO_MATCH_ACTION
        {
            REPORT_READER_RESULT,
            QUERY_VIRTUAL_ID
        }
        public enum EQ_INTERACTION_FAIL_ACTION
        {
            SET_AGV_NORMAL_STATUS,
            SET_AGV_DOWN_STATUS
        }
        public virtual bool CSTTrigger
        {
            get
            {
                return StaStored.CurrentVechicle.Parameters.CST_READER_TRIGGER;
            }
        }
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Load;
        private bool IsNeedQueryVirutalStation = false;
        public LDULDRecord lduld_record = new LDULDRecord();

        internal bool back_to_secondary_flag = false;
        internal WORKSTATION_HS_METHOD _eqHandshakeMode;


        public LoadTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            DetermineHandShakeSetting();
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
            if (Agv.Parameters.LDULD_Task_No_Entry)
            {
                return await base.BeforeTaskExecuteActions();
            }
            if (Agv.Parameters.CheckEQDOStatusWorkStationTags.Contains(RunningTaskData.Destination))
                CheckEQDIOStates();

            (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckBeforeHSStartInFrontOfEQ();
            if (!CstExistCheckResult.confirm)
                return (false, CstExistCheckResult.alarmCode);




            RecordLDULDStateToDB();

            if (IsNeedHandshake)
            {
                bool _is_modbus_hs = Agv.Parameters.EQHandshakeMethod == Vehicle.EQ_HS_METHOD.MODBUS;
                
                Agv.HandshakeStatusText = "AGV交握訊號重置...";
                Agv.ResetHandshakeSignals();
                Agv.ResetHSTimersAndEvents();
                await Task.Delay(400);
                Agv.HandshakeStatusText = _is_modbus_hs ? "建立Modbus連線..." : "確認光IO EQ GO訊號...";
                if (_is_modbus_hs)
                {
                    var modbusTcp = new clsEQHandshakeModbusTcp(Agv.Parameters.ModbusIO, destineTag, ModBusTcpPort);
                    if (!modbusTcp.Start(Agv.AGVS, Agv.AGVHsSignalStates, Agv.EQHsSignalStates))
                        return (false, AlarmCodes.Waiting_EQ_Handshake);
                }
                if (!Agv.Parameters.LDULD_Task_No_Entry)
                {
                    if (!Agv.IsEQGOOn())
                    {
                        await Task.Delay(200);
                        if (!Agv.IsEQGOOn())
                            return (false, AlarmCodes.Precheck_IO_Fail_EQ_GO);
                    }

                    if (!Agv.IsEQHsSignalInitialState())
                        return (false, AlarmCodes.Precheck_IO_EQ_PIO_State_Not_Reset);
                }

                if (Agv.Parameters.PlayHandshakingMusic)
                    BuzzerPlayer.Handshaking();

                (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyON(action);
                await Task.Delay(1000);
                if (!HSResult.eqready)
                {
                    return (false, HSResult.alarmCode);
                }
                if (Agv.Parameters.LDULDParams.LsrObstacleDetectionEnable)
                {
                    Agv.IsHandshaking = true;
                    Agv.HandshakeStatusText = "設備內障礙物檢查..";
                    LOG.TRACE($"EQ (TAG-{destineTag}) [Port雷射偵測障礙物]啟動");
                    bool _HasObstacle = await CheckPortObstacleViaLaser();
                    if (_HasObstacle)
                    {
                        Agv.HandshakeStatusText = "偵測到設備內有障礙物";
                        if (Agv.Parameters.LDULDParams.LsrObsDetectedAlarmLevel == ALARM_LEVEL.ALARM)
                        {
                            LOG.ERROR($"EQ (TAG-{destineTag}) [偵測到設備Port內有障礙物],Alarm等級=>不允許AGV侵入!");
                            return (false, AlarmCodes.EQP_PORT_HAS_OBSTACLE_BY_LSR);
                        }
                        else
                        {
                            LOG.WARN($"EQ (TAG-{destineTag}) [偵測到設備Port內有障礙物],警示等級=>允許侵入");
                            AlarmManager.AddWarning(AlarmCodes.EQP_PORT_HAS_OBSTACLE_BY_LSR);
                        }
                    }
                    else
                    {
                        LOG.TRACE($"EQ (TAG-{destineTag}) [設備Port內無障礙物] 允許侵入");
                    }
                }
                if (IsNeedHandshake)
                {
                    _ = Agv.Handshake_AGV_BUSY_ON(isBackToHome: false);
                }
                #region 前方障礙物預檢

                var _triggerLevelOfOBSDetected = Agv.Parameters.LOAD_OBS_DETECTION.AlarmLevelWhenTrigger;
                bool isNoObstacle = StartFrontendObstcleDetection(_triggerLevelOfOBSDetected);

                if (!isNoObstacle)
                    if (_triggerLevelOfOBSDetected == ALARM_LEVEL.ALARM)
                        return (false, FrontendSecondarSensorTriggerAlarmCode);
                    else
                        AlarmManager.AddWarning(FrontendSecondarSensorTriggerAlarmCode);
                #endregion

            }
            return await base.BeforeTaskExecuteActions();
        }
        protected override Task<SendActionCheckResult> TransferTaskToAGVC()
        {
            Agv.HandshakeStatusText = RunningTaskData.GoTOHomePoint ? "AGV退出設備中..." : "AGV進入設備中...";
            return base.TransferTaskToAGVC();
        }
        private async Task<bool> CheckPortObstacleViaLaser()
        {
            var _laserModeNumber = Agv.Parameters.LDULDParams.LsrObsLaserModeNumber;
            await Agv.Laser.ModeSwitch(_laserModeNumber);
            await Agv.WagoDO.SetState(DO_ITEM.Front_LsrBypass, false);
            await Task.Delay(800);

            bool _front_area_1_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_1);
            bool _front_area_2_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2);
            bool _front_area_3_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_3);
            bool _front_area_4_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_4);

            await Agv.Laser.ModeSwitch(LASER_MODE.Bypass);
            await Agv.WagoDO.SetState(DO_ITEM.Front_LsrBypass, true);

            return _front_area_1_obs || _front_area_2_obs || _front_area_3_obs || _front_area_4_obs;
        }

        private void RecordLDULDStateToDB()
        {
            lduld_record.StartTime = DateTime.Now;
            lduld_record.Action = this.action;
            lduld_record.WorkStationTag = destineTag;
            DBhelper.AddUDULDRecord(lduld_record);
        }

        private void DetermineHandShakeSetting()
        {
            if (Agv.WorkStations.Stations.TryGetValue(destineTag, out var data))
            {
                WORKSTATION_HS_METHOD mode = data.HandShakeModeHandShakeMode;
                eqHandshakeMode = mode;
                IsNeedHandshake = mode == WORKSTATION_HS_METHOD.HS;
                LOG.WARN($"[{action}] Tag_{destineTag} Handshake Mode:{mode}({(int)mode})");
            }
            else
            {
                LOG.WARN($"[{action}] Tag_{destineTag} Handshake Mode Not Defined! Forcing Handsake to Safty Protection. ");
                eqHandshakeMode = WORKSTATION_HS_METHOD.HS;
                IsNeedHandshake = true;
            }
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

        internal override void Abort(AlarmCodes alarm_code = AlarmCodes.None)
        {
            base.Abort(alarm_code);
            _WaitBackToHomeDonePause.Set();
        }
        /// <summary>
        /// AGV停車停好在設備後的動作
        /// </summary>
        /// <returns></returns>
        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            Agv.DirectionLighter.CloseAll();
            RecordParkLoction();
            (bool hs_success, AlarmCodes alarmCode) HSResult = new(false, AlarmCodes.None);
            _eqHandshakeMode = eqHandshakeMode;
            IsNeedQueryVirutalStation = Agv.Parameters.StationNeedQueryVirtualID.Contains(destineTag);
            LOG.TRACE($"Cargo Transfer Mode of TAG-{destineTag} ==> {CargoTransferMode}");
            if (_eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
            {
                if (CargoTransferMode == CARGO_TRANSFER_MODE.EQ_Pick_and_Place && ForkLifter != null)
                {
                    var _arm_move_result = await ForkLifter.ForkExtendOutAsync();
                }

                if (!TryCheckAGVStatus(out AlarmCodes alarmCode))
                    return (false, alarmCode);

                if (CargoTransferMode == CARGO_TRANSFER_MODE.EQ_Pick_and_Place)
                {
                    HSResult = await Agv.WaitEQBusyOnAndOFF(action);
                    if (HSResult.hs_success)
                    {
                        _ = Agv.Handshake_AGV_BUSY_ON(isBackToHome: true);
                        lduld_record.EQActionFinishTime = DateTime.Now;
                        DBhelper.ModifyUDLUDRecord(lduld_record);
                    }
                    else
                    {

                        LOG.ERROR($"Wait EQ Busy On/Off result Fail({HSResult.alarmCode})");
                        return (false, HSResult.alarmCode);
                    }
                }
                else
                {
                    LOG.TRACE($"AGV Pick and Place not need Wait EQ Busy ON/OFF.");
                }
                //放貨完成->清除CST帳籍
                if (action == ACTION_TYPE.Load)
                    Agv.CSTReader.ValidCSTID = "";

            }

            Agv.DirectionLighter.CloseAll();
            back_to_secondary_flag = false;
            await Task.Delay(1000);

            if (CargoTransferMode == CARGO_TRANSFER_MODE.EQ_Pick_and_Place && ForkLifter != null)
            {
                ForkLifter.ForkShortenInAsync();
            }
            else
            {
                (bool success, AlarmCodes alarmcode) forkActionResult = await ForkActionsInWorkStation();
                if (!forkActionResult.success)
                    return forkActionResult;

            }

            if (action == ACTION_TYPE.Unload && Agv.HasAnyCargoOnAGV())
            {
                Agv.CSTReader.ValidCSTID = "TrayUnknow";
            }
            else
                Agv.CSTReader.ValidCSTID = "";


            return await StartBackToHome();

        }

        private bool TryCheckAGVStatus(out AlarmCodes alarmCode)
        {
            alarmCode = AlarmCodes.None;
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while ((alarmCode = CheckAGVStatus()) != AlarmCodes.None)
            {
                Thread.Sleep(100);
                if (cts.IsCancellationRequested)
                {
                    break;
                }
            }
            return alarmCode == AlarmCodes.None;
        }

        private async Task<(bool success, AlarmCodes alarmCode)> StartBackToHome()
        {
            try
            {
                AlarmCodes checkstatus_alarm_code = AlarmCodes.None;
                if ((checkstatus_alarm_code = CheckAGVStatus(check_park_position: false, check_cargo_exist_state: true)) != AlarmCodes.None)
                {
                    return (false, checkstatus_alarm_code);
                }

                if (Agv.Parameters.SimulationMode)
                {
                    if (action == ACTION_TYPE.Unload)
                        HasCargoIOSimulation();
                    else
                        NoCargoIOSimulation();
                }
                RecordExistSensorState();

                Agv.DirectionLighter.Backward(delay: 800);
                RunningTaskData = RunningTaskData.CreateGoHomeTaskDownloadData();

                await Agv.Laser.AllLaserDisable();
                await Agv.Laser.ModeSwitch(LASER_MODE.Loading);
                await Agv.Laser.FrontBackLasersEnable(false, true);

                SendActionCheckResult send_task_result = await TransferTaskToAGVC();
                if (!send_task_result.Accept)
                {
                    LOG.ERROR($"{send_task_result.ToJson()}");
                    Agv.Sub_Status = SUB_STATUS.DOWN;
                    return (false, AlarmCodes.Can_not_Pass_Task_to_Motion_Control);
                }
                else
                {
                    await Task.Delay(500);
                    AlarmCodes _alarmcode = AlarmCodes.None;
                    if (Agv.AGVC.ActionStatus == ActionStatus.SUCCEEDED)
                    {
                        if (Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary)
                            ForkHomeProcess();

                        _alarmcode = await AfterBackHomeActions(ActionStatus.SUCCEEDED);
                    }
                    else if (Agv.AGVC.ActionStatus == ActionStatus.ACTIVE || Agv.AGVC.ActionStatus == ActionStatus.PENDING)
                    {
                        if (Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary)
                            ForkHomeProcess();

                        _WaitBackToHomeDonePause.Reset();

                        AGVCActionStatusChaged += BackToHomeActionDoneCallback;
                        await Agv.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.BACK_TO_SECONDARY_POINT, false);
                        LOG.TRACE("等待二次定位回HOME位置任務完成...", color: ConsoleColor.Magenta);
                        _WaitBackToHomeDonePause.WaitOne();
                        AGVCActionStatusChaged -= BackToHomeActionDoneCallback;
                        if (task_abort_alarmcode != AlarmCodes.None)
                        {
                            Agv.Sub_Status = SUB_STATUS.DOWN;
                            return (false, task_abort_alarmcode);
                        }
                        LOG.TRACE("車控回HOME位置任務完成", color: ConsoleColor.Magenta);
                        _alarmcode = await AfterBackHomeActions(_BackHomeActionDoneStatus);

                    }
                    Agv.Sub_Status = _alarmcode == AlarmCodes.None ? SUB_STATUS.IDLE : SUB_STATUS.DOWN;
                    return (_alarmcode == AlarmCodes.None, _alarmcode);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                throw;
            }
        }

        private void BackToHomeActionDoneCallback(ActionStatus status)
        {
            _BackHomeActionDoneStatus = status;
            LOG.WARN($"[AGVC Action Status Changed-ON-Action Actived][{RunningTaskData.Task_Simplex} -{action}-Back To Secondary Point of WorkStation] AGVC Action Status Changed: {status}.");
            _WaitBackToHomeDonePause.Set();
            _BackHomeActionDoneStatus = status;
        }
        private async Task<AlarmCodes> AfterBackHomeActions(ActionStatus status)
        {
            if (IsAGVCActionNoOperate(status) || Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                LOG.WARN($"車控/車載狀態錯誤(車控Action 狀態:{status},車載狀態 {Agv.Sub_Status})");
                var _task_abort_alarmcode = IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : AlarmCodes.AGV_State_Cant_do_this_Action;
                return IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : _task_abort_alarmcode;
            }

            if (status == ActionStatus.SUCCEEDED)
            {
                await Task.Delay(200);
                if (Agv.lastVisitedMapPoint.StationType != STATION_TYPE.Normal)
                {
                    //return AlarmCodes.AGV_Location_Not_Secondary;
                    AlarmManager.AddWarning(AlarmCodes.AGV_Location_Not_Secondary);
                }

                AGVCActionStatusChaged = null;
                back_to_secondary_flag = true;

                if (!Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary)
                {
                    await ForkHomeProcess();
                }

                if (IsNeedWaitForkHome)
                {
                    LOG.TRACE($"[Async Action] AGV Park Finish In Secondary, Waiting Fork Go Home Finish ");
                    Task.WaitAll(new Task[] { forkGoHomeTask });
                    LOG.TRACE($"[Async Action] Fork is at safe height Now");
                }

                var HSResult = await AGVCOMPTHandshake();
                //RestoreEQHandshakeConnectionMode();
                if (!HSResult.confirm)
                {
                    //await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: HSResult.alarmCode);
                    return HSResult.alarmCode;
                }

                (bool success, AlarmCodes alarmCode) CstBarcodeCheckResult = CSTBarcodeReadAfterAction().Result;
                if (!CstBarcodeCheckResult.success)
                {
                    AlarmCodes cst_read_fail_alarm = CstBarcodeCheckResult.alarmCode;
                    AlarmManager.AddAlarm(cst_read_fail_alarm, false);
                    //向派車詢問虛擬ID
                    //cst 類型
                    CST_TYPE cst_type = RunningTaskData.CST.First().CST_Type;
                    //詢問原因
                    clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE query_cause = cst_read_fail_alarm == AlarmCodes.Cst_ID_Not_Match ? clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.NOT_MATCH : clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.READ_FAIL;

                    switch (query_cause)
                    {
                        case clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.READ_FAIL:
                            LOG.TRACE($"Get Cargo From Station {destineTag} CST ID READ FAIL, query virtual id from AGVS ");
                            await Agv.QueryVirtualID(query_cause, cst_type);
                            break;
                        case clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.NOT_MATCH:

                            if (IsNeedQueryVirutalStation)
                            {
                                LOG.TRACE($"Station {destineTag} is need to query virtual id from AGVS when ID Not Match");
                                await Agv.QueryVirtualID(query_cause, cst_type);
                            }
                            break;
                        default:
                            break;
                    }
                    Agv.Sub_Status = Agv.Parameters.CstReadFailAction == EQ_INTERACTION_FAIL_ACTION.SET_AGV_DOWN_STATUS ? SUB_STATUS.DOWN : SUB_STATUS.IDLE;
                    if (action == ACTION_TYPE.Unload && Agv.Remote_Mode == REMOTE_MODE.ONLINE)
                        await WaitCSTIDReported();
                    //await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: cst_read_fail_alarm);
                }
                else
                {
                    if (action == ACTION_TYPE.Unload && Agv.Remote_Mode == REMOTE_MODE.ONLINE)
                        await WaitCSTIDReported();
                    //await base.HandleAGVCActionSucceess();
                }
                lduld_record.CargoID_Reader = Agv.CSTReader.ValidCSTID;
                lduld_record.EndTime = DateTime.Now;
                DBhelper.ModifyUDLUDRecord(lduld_record);
                return AlarmCodes.None;
            }
            else
            {
                var _task_abort_alarmcode = IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : AlarmCodes.AGV_State_Cant_do_this_Action;

                return IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : _task_abort_alarmcode;
            }
        }

        private void RecordParkLoction()
        {
            lduld_record.ParkLocX = Agv.Navigation.Data.robotPose.pose.position.x;
            lduld_record.ParkLocY = Agv.Navigation.Data.robotPose.pose.position.y;
            DBhelper.ModifyUDLUDRecord(lduld_record);
        }

        private void RecordExistSensorState()
        {
            if (Agv.Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            {
                lduld_record.ExistSensor1_State = Agv.WagoDI.GetState(DI_ITEM.Cst_Sensor_1);
                lduld_record.ExistSensor2_State = Agv.WagoDI.GetState(DI_ITEM.Cst_Sensor_2);
            }
            if (Agv.Parameters.AgvType == AGV_TYPE.FORK)
            {
                lduld_record.ExistSensor1_State = Agv.WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
                lduld_record.ExistSensor2_State = Agv.WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor);
            }

            DBhelper.ModifyUDLUDRecord(lduld_record);
        }

        private async Task<(bool success, AlarmCodes alarmcode)> ForkActionsInWorkStation()
        {
            if (ForkLifter == null)
                return (true, AlarmCodes.None);


            bool arm_move_Done = false;
            (bool confirm, string message) armMoveing = (false, "等待DO輸出");
            var isNeedArmExtend = Agv.WorkStations.Stations[destineTag].ForkArmExtend;

            if (isNeedArmExtend)
            {
                Agv.HandshakeStatusText = "AGV牙叉伸出中";
                LOG.TRACE($"FORK ARM Extend Out");
                var _arm_move_result = await ForkLifter.ForkExtendOutAsync();
                arm_move_Done = _arm_move_result.confirm;
                if (!arm_move_Done)
                {
                    LOG.TRACE($"FORK ARM Extend Action Done. {_arm_move_result.Item2}");
                    return (false, _arm_move_result.Item2);
                }
                else if (ForkLifter.CurrentForkARMLocation != FORK_ARM_LOCATIONS.END)
                {
                    return (false, AlarmCodes.Fork_Arm_Pose_Error);
                }
                LOG.INFO($"FORK ARM POSITION = {ForkLifter.CurrentForkARMLocation}");
                await Task.Delay(1000);
                //check arm position 
                (bool success, AlarmCodes alarm_code) fork_height_change_result = await ChangeForkPositionInWorkStation();
                if (!fork_height_change_result.success)
                    return (false, fork_height_change_result.alarm_code);

                //檢查在席
                (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckAfterEQActionFinishInEQ();
                if (!CstExistCheckResult.confirm)
                    return (false, CstExistCheckResult.alarmCode);
                await Task.Delay(700);
                var FormArmShortenTask = Task.Run(async () =>
                {
                    Agv.HandshakeStatusText = "AGV牙叉縮回中";
                    var arm_move_result = await ForkLifter.ForkShortenInAsync();
                    CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    while (ForkLifter.CurrentForkARMLocation != FORK_ARM_LOCATIONS.HOME)
                    {
                        Thread.Sleep(1);
                        if (cts.IsCancellationRequested)
                        {
                            return (false, AlarmCodes.Fork_Arm_Pose_Error);
                        }
                    }

                    if (!arm_move_result.confirm)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                    if (ForkLifter.CurrentForkARMLocation != FORK_ARM_LOCATIONS.HOME)
                    {
                        return (false, AlarmCodes.Fork_Arm_Pose_Error);
                    }
                    return (true, AlarmCodes.None);
                });

                if (!Agv.Parameters.ForkAGV.NoWaitForkArmFinishAndMoveOutInWorkStation)
                {
                    await FormArmShortenTask;
                    await Task.Delay(1000);
                }
                return (true, AlarmCodes.None);
            }
            else
            {
                return (true, AlarmCodes.None);
            }
        }

        private void HasCargoIOSimulation()
        {
            if (Agv.Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            {
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_1, false);
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_2, false);
            }
            else if (Agv.Parameters.AgvType == AGV_TYPE.FORK)
            {
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor, false);
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor, false);
            }
        }
        private void NoCargoIOSimulation()
        {
            if (Agv.Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            {
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_1, true);
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_2, true);
            }
            else if (Agv.Parameters.AgvType == AGV_TYPE.FORK)
            {
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor, true);
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor, true);
            }
        }


        private AlarmCodes CheckAGVStatus(bool check_park_position = true, bool check_cargo_exist_state = false)
        {
            LOG.INFO($"Check AGV Status--({Agv.BarcodeReader.CurrentTag}/{RunningTaskData.Destination})");

            //檢查在席
            if (check_cargo_exist_state)
            {

                (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckAfterEQActionFinishInEQ();
                if (!CstExistCheckResult.confirm)
                {
                    return CstExistCheckResult.alarmCode;
                }
            }

            AlarmCodes alarm_code = AlarmCodes.None;
            if (Agv.Sub_Status == SUB_STATUS.DOWN)
                alarm_code = AlarmCodes.AGV_State_Cant_Move;

            else if (check_park_position && Agv.BarcodeReader.CurrentTag != RunningTaskData.Destination)
                alarm_code = AlarmCodes.AGV_BarcodeReader_Not_Match_Tag_of_Destination;
            else if (check_park_position && Agv.BarcodeReader.DistanceToTagCenter > Agv.Parameters.TagParkingTolerance)
                alarm_code = AlarmCodes.AGV_Park_Position_Too_Far_From_Tag_Of_Destination;
            else
                alarm_code = AlarmCodes.None;

            if (alarm_code != AlarmCodes.None)
                LOG.WARN($"車載狀態錯誤({alarm_code}):{Agv.Sub_Status}-Barcode讀值:{Agv.BarcodeReader.CurrentTag},AGVC Last Visited Tag={Agv.Navigation.LastVisitedTag},距離Tag中心:{Agv.BarcodeReader.DistanceToTagCenter} mm | 終點Tag={RunningTaskData.Destination}");

            return alarm_code;
        }

        private async Task<(bool confirm, AlarmCodes alarmCode)> AGVCOMPTHandshake()
        {
            if (_eqHandshakeMode != WORKSTATION_HS_METHOD.HS)
                return (true, AlarmCodes.None);
            (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyOFF(action);
            if (!HSResult.eqready)
            {
                Agv.Sub_Status = SUB_STATUS.DOWN;
                return (false, HSResult.alarmCode);
            }
            else
                return (true, AlarmCodes.None);
        }
        private async Task WaitCSTIDReported()
        {
            LOG.TRACE($"Start Wait CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS");
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (Agv.AGVS.UseWebAPI)
                while (Agv.AGVS.previousRunningStatusReport_via_WEBAPI.CSTID.First() != Agv.CSTReader.ValidCSTID)
                {
                    await Task.Delay(1);
                    if (cts.IsCancellationRequested)
                    {
                        LOG.Critical($"Wait CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS Timeout!");
                        return;
                    }
                }
            else
            {
                while (Agv.AGVS.previousRunningStatusReport_via_TCPIP.CSTID.First() != Agv.CSTReader.ValidCSTID)
                {
                    await Task.Delay(1);
                    if (cts.IsCancellationRequested)
                    {
                        LOG.Critical($"Wait CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS Timeout!");
                        return;
                    }
                }
            }
            LOG.TRACE($"CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS SUCCESS");
        }

        protected async virtual Task<(bool success, AlarmCodes alarm_code)> ChangeForkPositionInWorkStation()
        {

            CancellationTokenSource _wait_fork_reach_position_cst = new CancellationTokenSource();
            _ = Task.Factory.StartNew(() =>
            {
                while (!_wait_fork_reach_position_cst.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                    Agv.HandshakeStatusText = $"AGV牙叉下降至放貨高度..({ForkLifter.CurrentHeightPosition} cm)";
                }
            });
            var result = await ForkLifter.ForkGoTeachedPoseAsync(destineTag, this.RunningTaskData.Height, FORK_HEIGHT_POSITION.DOWN_, 0.5);
            _wait_fork_reach_position_cst.Cancel();
            return result;

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
            if (!result.request_success || !result.action_done)
            {
                return (false, AlarmCodes.Read_Cst_ID_Fail);
            }
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(3000);
            while (Agv.CSTReader.Data.data == "")
            {
                await Task.Delay(1);
                if (cts.IsCancellationRequested)
                {
                    return (false, AlarmCodes.Read_Cst_ID_Fail_Service_Done_But_Topic_No_CSTID);
                }
            }
            var cst_id_expect = RunningTaskData.CST.First().CST_ID.Trim();

            if (cst_id_expect == "")
                return (true, AlarmCodes.None);

            var reader_valid_id = Agv.CSTReader.ValidCSTID.Trim();
            var reader_actual_read_id = Agv.CSTReader.Data.data.Trim().ToUpper();


            if (Agv.Parameters.CSTIDReadNotMatchSimulation)
            {
                LOG.ERROR($"[ID NOT MATCH SIMULATION] AGVS CST Download: {cst_id_expect}, CST READER : {reader_valid_id}");
                return (false, AlarmCodes.Cst_ID_Not_Match);
            }


            if (reader_valid_id == "ERROR" || reader_actual_read_id == "ERROR" || reader_actual_read_id == "ERROR")
            {
                LOG.ERROR($"CST Reader Action done and CSTID get(From /module_information), CST READER : {reader_actual_read_id}");
                return (false, AlarmCodes.Read_Cst_ID_Fail);
            }
            if (reader_valid_id != cst_id_expect)
            {
                LOG.ERROR($"AGVS CST Download: {cst_id_expect}, CST READER : {reader_valid_id}");
                return (false, AlarmCodes.Cst_ID_Not_Match);
            }
            Agv.CSTReader.ValidCSTID = reader_valid_id;
            return (true, AlarmCodes.None);
        }



        /// <summary>
        /// Load作業(放貨)=>車上應該有貨
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected virtual (bool confirm, AlarmCodes alarmCode) CstExistCheckBeforeHSStartInFrontOfEQ()
        {
            if (!Agv.Parameters.CST_EXIST_DETECTION.Before_In)
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
        protected virtual (bool confirm, AlarmCodes alarmCode) CstExistCheckAfterEQActionFinishInEQ()
        {
            Agv.HandshakeStatusText = "檢查在席狀態.(車上應無物料)";
            if (!StaStored.CurrentVechicle.Parameters.CST_EXIST_DETECTION.After_EQ_Busy_Off)
                return (true, AlarmCodes.None);

            if (Agv.CargoStatus != Vehicle.CARGO_STATUS.NO_CARGO) //不該有料卻有料
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
