//#define YM_4FAOI
using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.CargoStates;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Models.WorkStation;
using Microsoft.AspNetCore.SignalR;
using NLog;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static AGVSystemCommonNet6.MAP.MapPoint;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsForkLifter;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params.clsManualCheckCargoStatusParams;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    /// <summary>
    /// 放貨任務
    /// </summary>
    public class LoadTask : TaskBase
    {
        private ManualResetEvent _WaitBackToHomeDonePause = new ManualResetEvent(false);
        private ManualResetEvent _waitMoveToPortDonePause = new ManualResetEvent(false);
        private ActionStatus _BackHomeActionDoneStatus = ActionStatus.PENDING;

        public delegate bool CheckCargotatusDelegate(CheckPointModel checkPointData);
        public static event CheckCargotatusDelegate OnManualCheckCargoStatusTrigger;
        public override int MoveActionTimeout => Agv.Parameters.LDULDParams.MoveActionTimeoutInSec * 1000;
        public enum CST_ID_NO_MATCH_ACTION
        {
            REPORT_READER_RESULT,
            QUERY_VIRTUAL_ID
        }
        public enum CST_ID_READ_FAIL_ACTION
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

        internal clsCST CstInformation = new clsCST() { CST_Type = CST_TYPE.None };

        protected override int ExitPointTag => RunningTaskData.Homing_Trajectory.First().Point_ID;
        public LoadTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            height = Agv.Parameters.VMSParam.Protocol == Vehicle.VMS_PROTOCOL.GPM_VMS ? taskDownloadData.Height : taskDownloadData.Height - 1;
            DetermineHandShakeSetting();
            HandleCSTType();
        }

        private void HandleCSTType()
        {
            clsCST[] cst_info = RunningTaskData.CST;
            if (cst_info == null || cst_info.Length == 0)
            {
                CstInformation.CST_Type = CST_TYPE.None;
                return;
            }
            CstInformation = cst_info.First();
        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                //啟用前後雷射偵測 + Loading 組數
                await Agv.Laser.SideLasersEnable(false);
                await Agv.Laser.FrontBackLasersEnable(!Agv.Parameters.LDULDParams.BypassFrontLaserWhenEntryEQ, false);

                await Task.Delay(200);

                return await Agv.Laser.ModeSwitch(LASER_MODE.Secondary);

            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return false;
            }
        }

        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            if (Agv.Parameters.LDULD_Task_No_Entry && !IsDestineStationBuffer)
            {
                return await base.BeforeTaskExecuteActions();
            }
            if (Agv.Parameters.CheckEQDOStatusWorkStationTags.Contains(RunningTaskData.Destination))
                CheckEQDIOStates();

            (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckBeforeHSStartInFrontOfEQ();
            if (!CstExistCheckResult.confirm)
                return (false, CstExistCheckResult.alarmCode);

            RecordLDULDStateToDB();

            await ManualCheckCargoStatusPrcessBeforeAction();

            if (IsNeedHandshake)
            {
                bool _is_modbus_hs = HandshakeProtocol == Vehicle.EQ_HS_METHOD.MODBUS;
                bool isEmulationHs = HandshakeProtocol == EQ_HS_METHOD.EMULATION;
                Agv.HandshakeStatusText = "AGV交握訊號重置...";
                await Agv.ResetHandshakeSignals();
                Agv.ResetHSTimersAndEvents();
                await Task.Delay(400);
                Agv.HandshakeStatusText = _is_modbus_hs ? "建立Modbus連線..." : "確認光IO EQ GO訊號...";
                if (_is_modbus_hs)
                {
                    clsEQHandshakeModbusTcp modbusTcp = new clsEQHandshakeModbusTcp(Agv.Parameters.ModbusIO, destineTag, ModBusTcpPort);
                    if (!modbusTcp.Start(Agv.AGVS, Agv.AGVHsSignalStates, Agv.EQHsSignalStates))
                        return (false, AlarmCodes.Waiting_EQ_Handshake);
                }
                if (!Agv.Parameters.LDULD_Task_No_Entry)
                {
                    if (isEmulationHs)
                    {
                        await Agv.WagoDO.SetState(DO_ITEM.EMU_EQ_GO, true);
                        await Task.Delay(400);
                    }
                    if (!_is_modbus_hs && !Agv.IsEQGOOn())
                    {
                        await Task.Delay(200);
                        if (!Agv.IsEQGOOn())
                            return (false, AlarmCodes.Precheck_IO_Fail_EQ_GO);
                    }

                    if (!Agv.IsEQHsSignalInitialState(out AlarmCodes alarmCode))
                        return (false, alarmCode);
                }

                (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyON(action, watchEQGO: !_is_modbus_hs);
                await Task.Delay(1000);
                if (!HSResult.eqready)
                {
                    return (false, HSResult.alarmCode);
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

            var lsrDetectionResult = await TryDetectObstacleInEQPort();
            if (lsrDetectionResult.alarm_code != AlarmCodes.None)
            {
                if (Agv.Parameters.LDULDParams.LsrObsDetectedAlarmLevel == ALARM_LEVEL.ALARM)
                {
                    logger.Error($"EQ (TAG-{destineTag}) [偵測到設備Port內有障礙物],Alarm等級=>不允許AGV侵入!");
                    return (false, AlarmCodes.EQP_PORT_HAS_OBSTACLE_BY_LSR);
                }
                else
                {
                    logger.Warn($"EQ (TAG-{destineTag}) [偵測到設備Port內有障礙物],警示等級=>允許侵入");
                    AlarmManager.AddWarning(AlarmCodes.EQP_PORT_HAS_OBSTACLE_BY_LSR);
                }
            }
            else
            {
                logger.Trace($"EQ (TAG-{destineTag}) [設備Port內無障礙物] 允許侵入");
                _waitMoveToPortDonePause.Reset();
            }

            BuzzerPlayer.Action();
            return await base.BeforeTaskExecuteActions();
        }


        protected virtual async Task ManualCheckCargoStatusPrcessBeforeAction()
        {
            clsManualCheckCargoStatusParams manualCheckSettings = Agv.Parameters.ManualCheckCargoStatus;
            if (!manualCheckSettings.Enabled)
                return;

            bool modelExist = TryGetCheckPointModelByTag(Agv.Navigation.LastVisitedTag, ACTION_TYPE.Load, out CheckPointModel checkPointModel);
            if (!modelExist || !checkPointModel.Enabled || checkPointModel.TriggerMoment != CHECK_MOMENT.BEFORE_LOAD)
                return;
            InvokeCargoManualCheckNotify(checkPointModel);
            BuzzerPlayer.Action();
        }

        protected virtual async Task ManualCheckCargoStatusPrcessAfterAction()
        {
            //Do nothing
            return;
        }

        protected bool TryGetCheckPointModelByTag(int tag, ACTION_TYPE action, out CheckPointModel checkPointModel)
        {
            checkPointModel = null;
            if (Agv.Parameters.ManualCheckCargoStatus.CheckPoints == null || Agv.Parameters.ManualCheckCargoStatus.CheckPoints.Count == 0)
                return false;

            checkPointModel = Agv.Parameters.ManualCheckCargoStatus.CheckPoints.FirstOrDefault(x => x.CheckPointTag == tag &&
                                                                                x.TriggerMoment == (action == ACTION_TYPE.Load ? CHECK_MOMENT.BEFORE_LOAD : CHECK_MOMENT.AFTER_UNLOAD));
            return checkPointModel != null;
        }

        protected void InvokeCargoManualCheckNotify(CheckPointModel checkPointModel)
        {
            bool? _checked = OnManualCheckCargoStatusTrigger?.Invoke(checkPointModel); //if _checked is false=> Timeout. Nobody check the cargo status.
            logger.Trace($"Manual Check Cargo Status Triggered by CheckPoint Tag {checkPointModel.CheckPointTag} => Result:{_checked}");
        }

        private async Task<(bool confirm, AlarmCodes alarm_code)> TryDetectObstacleInEQPort()
        {
            if (Agv.Parameters.LDULDParams.LsrObstacleDetectionEnable)
            {
                Agv.IsHandshaking = true;
                Agv.HandshakeStatusText = "設備內障礙物檢查..";
                logger.Trace($"EQ (TAG-{destineTag}) [Port雷射偵測障礙物]啟動");
                bool _HasObstacle = await CheckPortObstacleViaLaser();

                return (!_HasObstacle, _HasObstacle ? AlarmCodes.EQP_PORT_HAS_OBSTACLE_BY_LSR : AlarmCodes.None);
            }
            else
            {
                return (true, AlarmCodes.None);
            }
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

            Agv.DirectionLighter.Flash(new DO_ITEM[2] { DO_ITEM.AGV_DiractionLight_Right, DO_ITEM.AGV_DiractionLight_Left }, 200);

            bool _front_area_1_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_1);
            bool _front_area_2_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2);
            bool _front_area_3_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_3);
            bool _front_area_4_obs = !Agv.WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_4);
            await Agv.Laser.ModeSwitch(LASER_MODE.Secondary);
            await Agv.Laser.FrontBackLasersEnable(true, false);

            Agv.DirectionLighter.AbortFlash();

            return _front_area_1_obs || _front_area_2_obs || _front_area_3_obs || _front_area_4_obs;
        }

        private void RecordLDULDStateToDB()
        {
            lduld_record.StartTime = DateTime.Now;
            lduld_record.Action = action;
            lduld_record.WorkStationTag = destineTag;
            DBhelper.AddUDULDRecord(lduld_record);
        }
#if YM_4FAOI
        private void DetermineHandShakeSetting()
        {
            IsNeedHandshake = true;
            eqHandshakeMode = WORKSTATION_HS_METHOD.HS;
        }
#else
        private void DetermineHandShakeSetting()
        {
            bool existHandshakeModeSetting = Agv.WorkStations.Stations.TryGetValue(destineTag, out clsWorkStationData? data);

            HandshakeProtocol = existHandshakeModeSetting && data != null ? data.HandShakeConnectionMode : EQ_HS_METHOD.PIO;

            if (IsJustAGVPickAndPlaceAtWIPPort || existHandshakeModeSetting && data.CargoTransferMode == CARGO_TRANSFER_MODE.ONLY_FIRST_SLOT_EQ_Pick_and_Place && height > 0)
            {
                IsNeedHandshake = false;
                eqHandshakeMode = WORKSTATION_HS_METHOD.NO_HS;
                logger.Info($"[{action}] Tag_{destineTag} is WIP and NOT FIRST Layer (Height={height}): Handshake Mode:{eqHandshakeMode}");
                return;
            }

            if (existHandshakeModeSetting)
            {
                WORKSTATION_HS_METHOD mode = data.HandShakeModeHandShakeMode;
                eqHandshakeMode = mode;
                IsNeedHandshake = mode == WORKSTATION_HS_METHOD.HS;
                logger.Warn($"[{action}] Tag_{destineTag} Handshake Mode:{mode}({(int)mode})");
            }
            else
            {
                logger.Warn($"[{action}] Tag_{destineTag} Handshake Mode Not Defined! Forcing Handsake to Safty Protection. ");
                eqHandshakeMode = WORKSTATION_HS_METHOD.HS;
                IsNeedHandshake = true;
            }
        }
#endif

        private void CheckEQDIOStates()
        {
            if (Agv.EQDIOStates == null)
            {
                logger.Warn($"無法取得EQ DIO狀態!");
                return;
            }
            if (Agv.EQDIOStates.TryGetValue(RunningTaskData.Destination, out var dio_status))
            {
                if (!dio_status.EQ_Status_Run)
                {
                    logger.Warn($"EQ DO : EQ_STATUS_RUN Not ON");
                    return;
                }

                if (dio_status.Up_Pose == false && dio_status.Down_Pose == false)
                {
                    logger.Warn($"EQ DO : EQ LD IN UNKNOWN POSITION.");
                    return;
                }

                if (action == ACTION_TYPE.Load)//放貨
                {
                    if (dio_status.PortExist)
                    {
                        logger.Warn($"EQ DO : EQ PORT HAS CARGO!");
                        return;
                    }
                    if (dio_status.Up_Pose == true && dio_status.Down_Pose == false)
                    {
                        logger.Warn($"EQ DO : EQ LD NOT DOWN_POSE");
                        return;
                    }
                    logger.Info($"EQ DO Status Check [Load] => OK");
                }
                else if (action == ACTION_TYPE.Unload)
                {
                    if (!dio_status.PortExist)
                    {
                        logger.Warn($"EQ DO : EQ PORT NO CARGO!!");
                        return;
                    }
                    if (dio_status.Up_Pose == false && dio_status.Down_Pose == true)
                    {
                        logger.Warn($"EQ DO : EQ LD NOT  UP_POSE");
                        return;
                    }
                    logger.Info($"EQ DO Status Check [Unload] => OK");
                }

            }
            else
                logger.Warn($"無法取得站點 Tag {RunningTaskData.Destination} 的DIO狀態.(Key Not Found..)");
        }

        internal override void Abort(AlarmCodes alarm_code = AlarmCodes.None)
        {
            task_abort_alarmcode = alarm_code;
            Agv.AGVC.EmergencyStop(true);
            base.Abort(alarm_code);
            _waitMoveToPortDonePause.Set();
            _WaitBackToHomeDonePause.Set();
            logger.Warn($"[Abort Task] {action} Task Abort, Alarm Code = {alarm_code}");
        }

        internal void ResetActionTimeoutManualResetSignals()
        {
            _waitMoveToPortDonePause.Set();
            _WaitBackToHomeDonePause.Set();
            logger.Trace("ResetActionTimeoutManualResetSignals");
        }

        protected override async Task WaitTaskDoneAsync()
        {
            Stopwatch _stopWatch = Stopwatch.StartNew();
            logger.Trace($"等待AGV完成 [{action}] 任務");
            logger.Info($"等待AGV完成 [{action}] -移動至設備 (Timeout : {MoveActionTimeout})");
            bool inTime = _waitMoveToPortDonePause.WaitOne(MoveActionTimeout);
            _stopWatch.Stop();
            if (task_abort_alarmcode != AlarmCodes.None)
            {
                logger.Info($"AGV [{action}] 動作中止-task_abort_alarmcode={task_abort_alarmcode}");
                return;
            }
            if (inTime)
            {
                task_abort_alarmcode = AlarmCodes.None;
                logger.Info($"AGV已完成 [{action}] -移動至設備任務(Time Spend: {_stopWatch.Elapsed})");
                _wait_agvc_action_done_pause.WaitOne();
                logger.Trace($"AGV完成 [{action}] 任務 ,Alarm Code:=>{task_abort_alarmcode}.]");
            }
            else
            {
                task_abort_alarmcode = AlarmCodes.Action_Timeout;
                logger.Error($"等待AGV完成 [{action}] -移動至設備任務 逾時!(Time Spend: {_stopWatch.Elapsed}) Alarm Code:=>{task_abort_alarmcode}.]");
            }
        }
        /// <summary>
        /// AGV停車停好在設備後的動作
        /// </summary>
        /// <returns></returns>
        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            try
            {
                _waitMoveToPortDonePause.Set();
                logger.Trace($"Load/Unload Action after AGVC Move done in port.");
#if !YM_4FAOI
                Agv.FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
#endif
                Agv.DirectionLighter.CloseAll();
                RecordParkLoction();
                (bool hs_success, AlarmCodes alarmCode) HSResult = new(false, AlarmCodes.None);
                _eqHandshakeMode = eqHandshakeMode;
                //IsNeedQueryVirutalStation = Agv.Parameters.StationNeedQueryVirtualID.Contains(destineTag);
                // logger.Trace($"Cargo Transfer Mode of TAG-{destineTag} ==> {CargoTransferMode}|_eqHandshakeMode:{_eqHandshakeMode}|isNeedArmExtend:{isNeedArmExtend}");
                logger.Trace($"Load/Unload Action after AGVC Move done in port. CargoTransferMode : {CargoTransferMode} |  _eqHandshakeMode:{_eqHandshakeMode} | isNeedArmExtend:{isNeedArmExtend}");
                if (_eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
                {
                    if (CargoTransferMode == CARGO_TRANSFER_MODE.EQ_Pick_and_Place && ForkLifter != null && isNeedArmExtend)
                    {
                        logger.Trace($"Cargo Transfer - ForkExtendOutAsync");
                        var _arm_move_result = await ForkLifter.ForkExtendOutAsync();
                    }

                    logger.Trace($"Cargo Transfer TryCheckAGVStatus");
                    var checkStatusResult = await TryCheckAGVStatus();
                    if (!checkStatusResult.success)
                        return (false, checkStatusResult.alarmCode);

                    if (CargoTransferMode == CARGO_TRANSFER_MODE.EQ_Pick_and_Place)
                    {
                        logger.Trace($"Cargo Transfer WaitEQBusyOnAndOFF");
                        HSResult = await Agv.WaitEQBusyOnAndOFF(action);
                        if (HSResult.hs_success)
                        {
                            _ = Agv.Handshake_AGV_BUSY_ON(isBackToHome: true);
                            lduld_record.EQActionFinishTime = DateTime.Now;
                            DBhelper.ModifyUDLUDRecord(lduld_record);
                        }
                        else
                        {

                            logger.Error($"Wait EQ Busy On/Off result Fail({HSResult.alarmCode})");
                            return (false, HSResult.alarmCode);
                        }
                    }
                    else
                    {
                        logger.Trace($"AGV Pick and Place not need Wait EQ Busy ON/OFF.");
                    }
                    //放貨完成->清除CST帳籍
                    if (action == ACTION_TYPE.Load)
                        Agv.CSTReader.ValidCSTID = "";

                }

                Agv.DirectionLighter.CloseAll();
                back_to_secondary_flag = false;
                await Task.Delay(1000);

                (bool success, AlarmCodes alarmcode) forkActionResult = await ForkActionsInWorkStation();
                if (!forkActionResult.success)
                    return forkActionResult;

                //檢查在席
                (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckAfterEQActionFinishInEQ();
                if (!CstExistCheckResult.confirm)
                    return (false, CstExistCheckResult.alarmCode);

                if (ForkLifter != null && isNeedArmExtend)
                    ForkLifter.ForkShortenInAsync();

                return await StartBackToHome();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return (false, AlarmCodes.Code_Error_In_System);
            }

        }

        private async Task<(bool success, AlarmCodes alarmCode)> TryCheckAGVStatus()
        {
            var alarmCode = AlarmCodes.None;
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while ((alarmCode = CheckAGVStatus()) != AlarmCodes.None)
            {
                await Task.Delay(1000);
                if (cts.IsCancellationRequested)
                {
                    break;
                }
            }
            if (alarmCode != AlarmCodes.None)
                logger.Warn($"車載狀態錯誤({alarmCode}):{Agv.GetSub_Status()}-Barcode讀值:{Agv.BarcodeReader.CurrentTag},AGVC Last Visited Tag={Agv.Navigation.LastVisitedTag}| 終點Tag={RunningTaskData.Destination}");

            return (alarmCode == AlarmCodes.None, alarmCode);
        }

        private async Task<(bool success, AlarmCodes alarmCode)> StartBackToHome()
        {

            try
            {
                IsBackToSecondaryPt = true;
                AlarmCodes checkstatus_alarm_code = AlarmCodes.None;
                if ((checkstatus_alarm_code = CheckAGVStatus(check_park_position: false, check_cargo_exist_state: true)) != AlarmCodes.None)
                {
                    logger.Warn($"車載狀態錯誤({checkstatus_alarm_code}):{Agv.GetSub_Status()}-Barcode讀值:{Agv.BarcodeReader.CurrentTag},AGVC Last Visited Tag={Agv.Navigation.LastVisitedTag}| 終點Tag={RunningTaskData.Destination}");
                    return (false, checkstatus_alarm_code);
                }
                RecordExistSensorState();
                (bool exitPortConfirmed, AlarmCodes alarmCode) = await ExitPortRequest();
                if (!exitPortConfirmed)
                {
                    return (false, alarmCode);
                }
                if (Agv.Parameters.SoundsParams.BackToSecondaryPtPlayAudio)
                    BuzzerPlayer.PlayInBackground(SOUNDS.Backward);

                Agv.DirectionLighter.Backward(delay: 800);
                RunningTaskData = RunningTaskData.CreateGoHomeTaskDownloadData();

                await Agv.Laser.ModeSwitch(LASER_MODE.Secondary);
                await Agv.Laser.FrontBackLasersEnable(false, true);
                SendActionCheckResult send_task_result = await TransferTaskToAGVC();
                if (!send_task_result.Accept)
                {
                    logger.Error($"{send_task_result.ToJson()}");
                    Agv.SetSub_Status(SUB_STATUS.DOWN);
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
                    else if (Agv.AGVC.IsRunning)
                    {
                        if (Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary)
                            ForkHomeProcess();


                        _WaitBackToHomeDonePause.Reset();

                        AGVCActionStatusChaged += BackToHomeActionDoneCallback;

                        ExistSensorStatusDetectionWhenBackToEntryPointAsync();

                        await Agv.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.BACK_TO_SECONDARY_POINT, false);

                        logger.Trace($"等待二次定位回HOME位置任務完成...(Timeout:{MoveActionTimeout}) ms");

                        (bool _inTime, long _timeSpend) = await WaitVehicleArriveEntryPoint(MoveActionTimeout);

                        AGVCActionStatusChaged -= BackToHomeActionDoneCallback;

                        if (!_inTime)
                            task_abort_alarmcode = AlarmCodes.Action_Timeout;

                        if (task_abort_alarmcode != AlarmCodes.None)
                        {
                            logger.Info($"AGV [{action}] -退出至二次定位點任務已終止!(task_abort_alarmcode:{task_abort_alarmcode},Time Spend: {_timeSpend} ms)");
                            Agv.SetSub_Status(SUB_STATUS.DOWN);
                            return (false, task_abort_alarmcode);
                        }
                        logger.Info($"AGV已完成 [{action}] -退出至二次定位點任務(Time Spend: {_timeSpend} ms)");
                        //logger.Trace("車控回HOME位置任務完成");
                        _alarmcode = await AfterBackHomeActions(_BackHomeActionDoneStatus);

                    }
                    Agv.SetSub_Status(_alarmcode == AlarmCodes.None ? SUB_STATUS.IDLE : SUB_STATUS.DOWN);
                    return (_alarmcode == AlarmCodes.None, _alarmcode);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                throw;
            }
            finally
            {
                _WaitBackToHomeDonePause.Set();
            }
        }

        protected virtual async Task ExistSensorStatusDetectionWhenBackToEntryPointAsync()
        {
            //放貨 檢查是否還有貨
            await Task.Factory.StartNew(async () =>
            {
                while (Agv.AGVC.IsRunning)
                {
                    if (Agv.CargoStateStorer.GetCargoStatus(false) != CARGO_STATUS.NO_CARGO)
                    {
                        Agv.SoftwareEMO(AlarmCodes.Has_Cst_Without_Job);
                        return;
                    }
                    await Task.Delay(1000);
                }
            });
        }

        private async Task<(bool _inTime, long _timeSpend)> WaitVehicleArriveEntryPoint(int moveActionTimeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool _cancelWaitFlag = false;
            GPMVehicleControlSystem.VehicleControl.DIOModule.clsIOSignal? backLaserArea3 = Agv.WagoDI.VCSInputs.FirstOrDefault(o => o.Input == DI_ITEM.BackProtection_Area_Sensor_3);

            try
            {
                if (backLaserArea3 != null)
                {
                    backLaserArea3.OnSignalOFF += HandleBackLaserArea3SignalOFF;
                    backLaserArea3.OnSignalON += HandleBackLaserArea3SignalON;
                }

                _ = Task.Factory.StartNew(async () =>
                {
                    _WaitBackToHomeDonePause.WaitOne();
                    _cancelWaitFlag = true;
                });
                while (stopwatch.ElapsedMilliseconds < moveActionTimeout)
                {
                    await Task.Delay(100);
                    if (_cancelWaitFlag)
                    {
                        stopwatch.Stop();
                        return (true, stopwatch.ElapsedMilliseconds);
                    }
                }
                stopwatch.Stop();
                return (false, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (backLaserArea3 != null)
                {
                    backLaserArea3.OnSignalOFF -= HandleBackLaserArea3SignalOFF;
                    backLaserArea3.OnSignalON -= HandleBackLaserArea3SignalON;
                }
            }
            void HandleBackLaserArea3SignalOFF(object obj, EventArgs e)
            {
                _PauseTimer($"後方雷射觸發，退出工作站Timeout暫停監視");
            }
            void HandleBackLaserArea3SignalON(object obj, EventArgs e)
            {
                _RestartTimer("後方雷射解除觸發，退出工作站Timeout監視重新計時");
            }
            void _PauseTimer(string message)
            {
                stopwatch.Stop();
                logger.Warn(message);
                Agv.DebugMessageBrocast(message);
            }
            void _RestartTimer(string message)
            {
                stopwatch.Restart();
                Agv.DebugMessageBrocast(message);
                logger.Warn(message);
            }

        }



        private void BackToHomeActionDoneCallback(ActionStatus status)
        {
            _BackHomeActionDoneStatus = status;
            logger.Warn($"[AGVC Action Status Changed-ON-Action Actived][{RunningTaskData.Task_Simplex} -{action}-Back To Secondary Point of WorkStation] AGVC Action Status Changed: {status}.");
            _WaitBackToHomeDonePause.Set();
            _BackHomeActionDoneStatus = status;
        }
        private async Task<AlarmCodes> AfterBackHomeActions(ActionStatus status)
        {

            if (IsAGVCActionNoOperate(status) || Agv.GetSub_Status() == SUB_STATUS.DOWN)
            {
                logger.Warn($"車控/車載狀態錯誤(車控Action 狀態:{status},車載狀態 {Agv.GetSub_Status()})");
                var _task_abort_alarmcode = IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : AlarmCodes.AGV_State_Cant_do_this_Action;
                return IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : _task_abort_alarmcode;
            }

            if (status == ActionStatus.SUCCEEDED)
            {
                bool isForkReachStandyHeight = false;
                bool AsyncCSTReadSuccess = action == ACTION_TYPE.Load ? true : false;
                CancellationTokenSource asyncCSTReadCancellationTokenSource = new CancellationTokenSource();
                if (Agv.Parameters.AgvType == AGV_TYPE.FORK && action == ACTION_TYPE.Unload && Agv.Parameters.ForkAGV.TriggerCstReaderWhenUnloadBackToEntryPointAndReachTag)
                {
                    logger.Trace($"[Async Action] 邊降牙叉邊拍照 AGV Park Finish In Secondary, Trigger CST Reader When Unload Back To Entry Point And Reach Tag");
                    //邊降邊拍
                    AsyncCSTReadSuccess = false;
                    _ = Task.Run(async () =>
                    {
                        (bool success, AlarmCodes alarmCode) AsyncCstReaderTriggerResult = (false, AlarmCodes.Read_Cst_ID_Fail);
                        while (!isForkReachStandyHeight)
                        {
                            try
                            {
                                await Task.Delay(1, asyncCSTReadCancellationTokenSource.Token);
                                AsyncCstReaderTriggerResult = await CSTBarcodeReadAfterAction(asyncCSTReadCancellationTokenSource.Token);
                                AsyncCSTReadSuccess = AsyncCstReaderTriggerResult.success;
                                if (AsyncCSTReadSuccess)
                                {
                                    logger.Trace($"[Async Action] 邊降牙叉邊拍照 拍照成功!");

                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Trace($"[Async Action] 邊降牙叉邊拍照任務已被取消. ({ex.Message})");
                                AsyncCSTReadSuccess = false;
                                return;
                            }
                        }

                    });

                }

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
                    logger.Trace($"[Async Action] AGV Park Finish In Secondary, Waiting Fork Go Home Finish ");
                    Task.WaitAll(new Task[] { forkGoHomeTask });
                    logger.Trace($"[Async Action] Fork is at safe height Now");
                }
                asyncCSTReadCancellationTokenSource.Cancel();
                isForkReachStandyHeight = true;
                var HSResult = await AGVCOMPTHandshake(statusDownWhenErr: false);
                //RestoreEQHandshakeConnectionMode();
                if (!HSResult.confirm)
                {
                    logger.Warn($"設備外交握失敗(Alarm Code={HSResult.alarmCode})，新增異常但流程可繼續");
                    AlarmManager.AddAlarm(HSResult.alarmCode, true);
                    await Agv.ResetHandshakeSignals();
                }

                (bool success, AlarmCodes alarmCode) CstBarcodeCheckResult = AsyncCSTReadSuccess ? (true, AlarmCodes.None) : CSTBarcodeReadAfterAction(new CancellationToken()).Result;
                if (!CstBarcodeCheckResult.success)
                {
                    AlarmCodes cst_read_fail_alarm = CstBarcodeCheckResult.alarmCode;
                    //向派車詢問虛擬ID
                    //cst 類型
                    CST_TYPE cst_type = RunningTaskData.CST.First().CST_Type;
                    //詢問原因
                    clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE query_cause = cst_read_fail_alarm == AlarmCodes.Cst_ID_Not_Match ? clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.NOT_MATCH : clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.READ_FAIL;

                    switch (query_cause)
                    {
                        case clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.READ_FAIL:
                            if (Agv.Parameters.Cst_ID_Read_Fail_Action == CST_ID_READ_FAIL_ACTION.QUERY_VIRTUAL_ID)
                            {
                                logger.Trace($"Get Cargo From Station {destineTag} CST ID READ FAIL, query virtual id from AGVS ");
                                await Agv.QueryVirtualID(query_cause, cst_type);
                            }
                            break;
                        case clsVirtualIDQu.VIRTUAL_ID_QUERY_TYPE.NOT_MATCH:

                            if (Agv.Parameters.Cst_ID_Not_Match_Action == CST_ID_NO_MATCH_ACTION.QUERY_VIRTUAL_ID)
                            {
                                logger.Trace($"Station {destineTag} is need to query virtual id from AGVS when ID Not Match");
                                await Agv.QueryVirtualID(query_cause, cst_type);
                            }
                            break;
                        default:
                            break;
                    }
                    Agv.SetSub_Status(Agv.Parameters.CstReadFailAction == EQ_INTERACTION_FAIL_ACTION.SET_AGV_DOWN_STATUS ? SUB_STATUS.DOWN : SUB_STATUS.IDLE);
                    if (action == ACTION_TYPE.Unload && Agv.Remote_Mode == REMOTE_MODE.ONLINE)
                        await WaitCSTIDReported();
                    AlarmManager.AddAlarm(cst_read_fail_alarm, false);
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
                Agv.IsHandshaking = false;

                await ManualCheckCargoStatusPrcessAfterAction();

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
            try
            {

                lduld_record.ParkLocX = Agv.Navigation.Data.robotPose.pose.position.x;
                lduld_record.ParkLocY = Agv.Navigation.Data.robotPose.pose.position.y;
                DBhelper.ModifyUDLUDRecord(lduld_record);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void RecordExistSensorState()
        {
            if (Agv.Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            {
                lduld_record.ExistSensor1_State = Agv.WagoDI.GetState(DI_ITEM.TRAY_Exist_Sensor_1);
                lduld_record.ExistSensor2_State = Agv.WagoDI.GetState(DI_ITEM.TRAY_Exist_Sensor_2);
            }
            if (Agv.Parameters.AgvType == AGV_TYPE.FORK)
            {
                lduld_record.ExistSensor1_State = Agv.WagoDI.GetState(DI_ITEM.RACK_Exist_Sensor_2);
                lduld_record.ExistSensor2_State = Agv.WagoDI.GetState(DI_ITEM.RACK_Exist_Sensor_1);
            }

            DBhelper.ModifyUDLUDRecord(lduld_record);
        }

#if YM_4FAOI
        private bool isNeedArmExtend => false;
#else
        private bool isNeedArmExtend => Agv.WorkStations.Stations[destineTag].ForkArmExtend;
#endif

        private async Task<(bool success, AlarmCodes alarmcode)> ForkActionsInWorkStation()
        {
            if (ForkLifter == null)
                return (true, AlarmCodes.None);


            bool arm_move_Done = false;
            (bool confirm, string message) armMoveing = (false, "等待DO輸出");

            if (isNeedArmExtend)
            {
                Agv.HandshakeStatusText = "AGV牙叉伸出中";
                logger.Trace($"FORK ARM Extend Out");
                var _arm_move_result = await ForkLifter.ForkExtendOutAsync();
                arm_move_Done = _arm_move_result.confirm;
                if (!arm_move_Done)
                {
                    logger.Trace($"FORK ARM Extend Action Done. {_arm_move_result.Item2}");
                    return (false, _arm_move_result.Item2);
                }
                else if (ForkLifter.CurrentForkARMLocation != FORK_ARM_LOCATIONS.END)
                {
                    return (false, AlarmCodes.Fork_Arm_Pose_Error);
                }
                logger.Info($"FORK ARM POSITION = {ForkLifter.CurrentForkARMLocation}");
                await Task.Delay(1000);
                //check arm position 
            }

            //在RACK取放貨且是空取空放模式
            if (IsDestineStationBuffer && Agv.Parameters.LDULD_Task_No_Entry)
            {
                Agv.CargoStateStorer.simulation_cargo_status = action == ACTION_TYPE.Load ? CARGO_STATUS.NO_CARGO : CARGO_STATUS.HAS_CARGO_NORMAL;//模擬在席
                return (true, AlarmCodes.None);
            }

            (double position, bool success, AlarmCodes alarm_code) fork_height_change_result = await ChangeForkPositionInWorkStation();
            ExpectedForkPostionWhenEntryWorkStation = fork_height_change_result.position;
            if (!fork_height_change_result.success)
                return (false, fork_height_change_result.alarm_code);

            return (true, AlarmCodes.None);
        }

        private AlarmCodes CheckAGVStatus(bool check_park_position = true, bool check_cargo_exist_state = false)
        {
            logger.Info($"Check AGV Status--({Agv.BarcodeReader.CurrentTag}/{RunningTaskData.Destination})");
            string parkDirection = "";
            double parkError = 0;
            double parkTolerance = Agv.Parameters.TagParkingTolerance;
            AlarmCodes alarm_code = AlarmCodes.None;
            if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                return AlarmCodes.AGV_State_Cant_Move;

            if (check_park_position && Agv.BarcodeReader.CurrentTag != RunningTaskData.Destination)
                return AlarmCodes.AGV_BarcodeReader_Not_Match_Tag_of_Destination;
            if (check_park_position && _IsParkLocationTooFarFromTagCenter(Agv.BarcodeReader.CurrentAngle, ref parkTolerance, out parkDirection, out parkError))
            {
                logger.Error($"停車精度檢查失敗:方向= {parkDirection},誤差={parkError}/{parkTolerance}");
                return AlarmCodes.AGV_Park_Position_Too_Far_From_Tag_Of_Destination;
            }
            else
            {
                alarm_code = AlarmCodes.None;
                logger.Info($"停車精度確認OK:方向 = {parkDirection},誤差={parkError}/{parkTolerance}");
                return AlarmCodes.None;
            }

            //檢查停車精度,考慮當前航向角度決定要只用 X 或 Y方向的Tag中心誤差
            bool _IsParkLocationTooFarFromTagCenter(double currentTheta, ref double tolerance, out string direction, out double error)
            {
                direction = "unknown";
                error = 0;
                if (currentTheta >= -45 && currentTheta <= 45 || currentTheta >= 135 || currentTheta <= -135)
                {
                    direction = "X";
                    error = Agv.BarcodeReader.CurrentX;
                    return Math.Abs(Agv.BarcodeReader.CurrentX) > tolerance;
                }
                else if (currentTheta > 45 && currentTheta < 135 || currentTheta > -135 && currentTheta < -45)
                {
                    direction = "Y";
                    error = Agv.BarcodeReader.CurrentY;
                    return Math.Abs(Agv.BarcodeReader.CurrentY) > tolerance;
                }
                else
                {
                    return false;
                }


            }
        }

        private async Task<(bool confirm, AlarmCodes alarmCode)> AGVCOMPTHandshake(bool statusDownWhenErr)
        {
            if (_eqHandshakeMode != WORKSTATION_HS_METHOD.HS)
                return (true, AlarmCodes.None);
            (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyOFF(action);
            if (!HSResult.eqready)
            {
                if (statusDownWhenErr)
                    Agv.SetSub_Status(SUB_STATUS.DOWN);
                return (false, HSResult.alarmCode);
            }
            else
                return (true, AlarmCodes.None);
        }
        private async Task WaitCSTIDReported()
        {
            logger.Trace($"Start Wait CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS");
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (Agv.AGVS.UseWebAPI)
                while (Agv.AGVS.previousRunningStatusReport_via_WEBAPI.CSTID.First() != Agv.CSTReader.ValidCSTID)
                {
                    await Task.Delay(1);
                    if (cts.IsCancellationRequested)
                    {
                        logger.Error($"Wait CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS Timeout!");
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
                        logger.Error($"Wait CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS Timeout!");
                        return;
                    }
                }
            }
            logger.Trace($"CST ID =  {Agv.CSTReader.ValidCSTID} Reported TO AGVS SUCCESS");
        }

        protected async virtual Task<(double position, bool success, AlarmCodes alarm_code)> ChangeForkPositionInWorkStation()
        {

            CancellationTokenSource _wait_fork_reach_position_cst = new CancellationTokenSource();
            WaitForkReachPoseAndUpdateStatusText(_wait_fork_reach_position_cst);
            var result = await ForkLifter.ForkGoTeachedPoseAsync(destineTag, height, FORK_HEIGHT_POSITION.DOWN_, 0.5);
            _wait_fork_reach_position_cst.Cancel();
            return result;

        }

        private async Task WaitForkReachPoseAndUpdateStatusText(CancellationTokenSource _wait_fork_reach_position_cst)
        {
            while (!_wait_fork_reach_position_cst.IsCancellationRequested)
            {
                await Task.Delay(1);
                Agv.HandshakeStatusText = $"AGV牙叉下降至放貨高度..({ForkLifter.CurrentHeightPosition} cm)";
            }
        }

        internal virtual async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeReadAfterAction(CancellationToken cancellationToken)
        {

            Agv.CSTReader.ValidCSTID = "";
            //await Agv.AGVC.TriggerCSTReader();
            return (true, AlarmCodes.None);
        }

        protected async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeRead(CancellationToken cancellationToken)
        {

            var cstType = Agv.CargoStateStorer.GetCargoType();
            (bool request_success, bool action_done) result = await Agv.AGVC.TriggerCSTReader(cstType);
            if (!result.request_success || !result.action_done)
            {
                return (false, AlarmCodes.Read_Cst_ID_Fail);
            }
            try
            {
                await Agv.AGVC.CSTReadServiceSemaphoreSlim.WaitAsync();
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(3000);
                while (Agv.CSTReader.Data.data == "")
                {
                    try
                    {
                        await Task.Delay(1, cancellationToken);
                    }
                    catch (Exception)
                    {
                        return (false, AlarmCodes.Read_Cst_ID_Interupted);
                    }

                    if (cts.IsCancellationRequested)
                    {
                        return (false, AlarmCodes.Read_Cst_ID_Fail_Service_Done_But_Topic_No_CSTID);
                    }
                }

                var reader_valid_id = Agv.CSTReader.ValidCSTID.Trim();
                var reader_actual_read_id = Agv.CSTReader.realTimeCSTIDRecievedFromModuleInfo.Trim().ToUpper();

                var cst_id_expect = RunningTaskData.CST.First().CST_ID.Trim();
                if (cst_id_expect == "")
                {
                    logger.Info($"CST READ {reader_valid_id} but ID Expect Read From AGVS Task Download is empty");
                    return (true, AlarmCodes.None);
                }



                if (Agv.Parameters.CSTIDReadNotMatchSimulation)
                {
                    logger.Error($"[ID NOT MATCH SIMULATION] AGVS CST Download: {cst_id_expect}, CST READER : {reader_valid_id}");
                    return (false, AlarmCodes.Cst_ID_Not_Match);
                }


                if (reader_valid_id == "ERROR" || string.IsNullOrEmpty(reader_valid_id) || reader_actual_read_id == "ERROR" || reader_actual_read_id == "ERROR")
                {
                    logger.Error($"CST Reader Action done and CSTID get(From /module_information), CST READER : {reader_actual_read_id}");
                    return (false, AlarmCodes.Read_Cst_ID_Fail);
                }
                if (reader_valid_id != cst_id_expect)
                {
                    logger.Error($"AGVS CST Download: {cst_id_expect}, CST READER : {reader_valid_id}");
                    return (false, AlarmCodes.Cst_ID_Not_Match);
                }
                Agv.CSTReader.ValidCSTID = reader_valid_id;
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                logger.Error($"Exception Occur when CSTBarcodeRead method invoke => {ex.Message},{ex.StackTrace}");
                return (false, AlarmCodes.Read_Cst_ID_Fail);
            }
            finally
            {
                Agv.AGVC.CSTReadServiceSemaphoreSlim.Release();
            }

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

            if (!Agv.CargoStateStorer.HasAnyCargoOnAGV(Agv.Parameters.LDULD_Task_No_Entry))
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
            {
                Agv.CSTReader.ValidCSTID = "";
                return (true, AlarmCodes.None);
            }

            if (Agv.CargoStateStorer.GetCargoStatus(Agv.Parameters.LDULD_Task_No_Entry) != CARGO_STATUS.NO_CARGO) //不該有料卻有料
                return (false, AlarmCodes.Has_Cst_Without_Job);
            Agv.CSTReader.ValidCSTID = "";
            return (true, AlarmCodes.None);
        }

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Forward();
        }

        protected override void Dispose(bool disposing)
        {
            ResetActionTimeoutManualResetSignals();
            base.Dispose(disposing);
        }
    }
}
