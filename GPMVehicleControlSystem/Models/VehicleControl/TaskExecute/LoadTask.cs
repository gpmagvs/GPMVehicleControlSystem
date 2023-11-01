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
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using AGVSystemCommonNet6.Tools.Database;
using GPMVehicleControlSystem.Models.Emulators;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    /// <summary>
    /// 放貨任務
    /// </summary>
    public class LoadTask : TaskBase
    {
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
        internal WORKSTATION_HS_METHOD _eqHandshakeMode;

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
                await Task.Delay(700);
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
            IsNeedQueryVirutalStation = Agv.Parameters.StationNeedQueryVirtualID.Contains(destineTag);
            LOG.TRACE($"TAG= {destineTag} is need query virtual station? {IsNeedQueryVirutalStation}");

            if (_eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
            {
                AlarmCodes checkstatus_alarm_code = AlarmCodes.None;
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                while ((checkstatus_alarm_code = CheckAGVStatus()) != AlarmCodes.None)
                {
                    await Task.Delay(100);
                    if (cts.IsCancellationRequested)
                    {
                        Agv.SetAGV_TR_REQ(false);
                        return (false, checkstatus_alarm_code);
                    }
                }
                HSResult = await Agv.WaitEQBusyOFF(action);
                if (!HSResult.hs_success)
                {
                    Agv.DirectionLighter.CloseAll();
                    return (false, HSResult.alarmCode);
                }
                else
                {
                    //放貨完成->清除CST帳籍
                    if (action == ACTION_TYPE.Load)
                        Agv.CSTReader.ValidCSTID = "";
                }

            }

            Agv.DirectionLighter.CloseAll();
            back_to_secondary_flag = false;
            await Task.Delay(1000);

            if (ForkLifter != null && !Agv.Parameters.LDULD_Task_No_Entry)
            {
                bool arm_move_Done = false;
                (bool confirm, string message) armMoveing = (false, "等待DO輸出");
                var isNeedArmExtend = Agv.WorkStations.Stations[destineTag].ForkArmExtend;

                if (isNeedArmExtend)
                {
                    LOG.INFO($"FORK ARM Extend Out");
                    var _arm_move_result = await ForkLifter.ForkExtendOutAsync();
                    arm_move_Done = _arm_move_result.confirm;
                    if (!arm_move_Done)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                    if (ForkLifter.CurrentForkARMLocation != FORK_ARM_LOCATIONS.END)
                    {
                        return (false, AlarmCodes.Fork_Arm_Pose_Error);
                    }
                    LOG.INFO($"FORK ARM POSITION = {ForkLifter.CurrentForkARMLocation}");
                    await Task.Delay(1000);
                }
                else
                {
                    arm_move_Done = true;
                }
                if (arm_move_Done)
                {
                    //check arm position 
                    (bool success, AlarmCodes alarm_code) fork_height_change_result = await ChangeForkPositionInWorkStation();
                    if (!fork_height_change_result.success)
                        return (false, fork_height_change_result.alarm_code);

                    await Task.Delay(1000);
                    if (isNeedArmExtend)
                    {
                        var FormArmShortenTask = Task.Run(async () =>
                        {
                            var arm_move_result = await ForkLifter.ForkShortenInAsync();
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
                    }
                }

            }

            //檢查在席
            (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckAfterEQBusyOff();
            if (!CstExistCheckResult.confirm)
                return (false, CstExistCheckResult.alarmCode);
            if (TaskCancelCTS.IsCancellationRequested)
                return (false, AlarmCodes.None);

            if (action == ACTION_TYPE.Unload && Agv.HasAnyCargoOnAGV())
            {
                Agv.CSTReader.ValidCSTID = "TrayUnknow";
            }
            else
                Agv.CSTReader.ValidCSTID = "";



            if (Agv.Parameters.SimulationMode)
            {
                if (action == ACTION_TYPE.Unload)
                    HasCargoIOSimulation();
                else
                    NoCargoIOSimulation();
            }


            //下Homing Trajectory 任務讓AGV退出

            try
            {
                AlarmCodes checkstatus_alarm_code = AlarmCodes.None;
                if ((checkstatus_alarm_code = CheckAGVStatus(false)) != AlarmCodes.None)
                {
                    Agv.SetAGV_TR_REQ(false);
                    return (false, checkstatus_alarm_code);
                }
                if (Agv.Parameters.LDULD_Task_No_Entry)
                {
                    HandleBackToHomeActionStatusChanged(ActionStatus.SUCCEEDED);
                    return (true, AlarmCodes.None);
                }
                else
                {
                    Agv.DirectionLighter.Backward(delay: 800);
                    RunningTaskData = RunningTaskData.CreateGoHomeTaskDownloadData();

                    await Agv.Laser.AllLaserDisable();
                    await Agv.Laser.ModeSwitch(LASER_MODE.Loading);
                    await Agv.Laser.FrontBackLasersEnable(false, true);

                    (bool agvc_executing, string message) agvc_response = await TransferTaskToAGVC();
                    if (!agvc_response.agvc_executing)
                    {
                        LOG.ERROR(agvc_response.message);
                        return (false, AlarmCodes.Can_not_Pass_Task_to_Motion_Control);
                    }
                    else
                    {
                        await Task.Delay(500);
                        if (Agv.AGVC.ActionStatus == ActionStatus.SUCCEEDED)
                        {
                            if (Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary)
                                ForkHomeProcess();
                            HandleBackToHomeActionStatusChanged(ActionStatus.SUCCEEDED);
                        }
                        else if (Agv.AGVC.ActionStatus == ActionStatus.ACTIVE | Agv.AGVC.ActionStatus == ActionStatus.PENDING)
                        {
                            if (Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary)
                                ForkHomeProcess();
                            AGVCActionStatusChaged += HandleBackToHomeActionStatusChanged;
                            await Agv.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                        }
                        return (true, AlarmCodes.None);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                throw;
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


        private AlarmCodes CheckAGVStatus(bool check_park_position = true)
        {

            LOG.INFO($"Check AGV Status--({Agv.BarcodeReader.CurrentTag}/{RunningTaskData.Destination})");
            if (Agv.Parameters.LDULD_Task_No_Entry)
                return AlarmCodes.None;

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
        bool IsNeedWaitForkHome = false;
        Task forkGoHomeTask = null;
        private async void HandleBackToHomeActionStatusChanged(ActionStatus status)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                if (IsAGVCActionNoOperate(status, HandleBackToHomeActionStatusChanged))
                    return;

                if (Agv.Sub_Status == SUB_STATUS.DOWN)
                {
                    AGVCActionStatusChaged = null;
                    return;
                }


                LOG.WARN($"[AGVC Action Status Changed-ON-Action Actived][{RunningTaskData.Task_Simplex} -{action}-Back To Secondary Point of WorkStation] AGVC Action Status Changed: {status}.");


                if (status == ActionStatus.SUCCEEDED)
                {
                    if (Agv.lastVisitedMapPoint.StationType != STATION_TYPE.Normal)
                    {
                        AlarmManager.AddAlarm(AlarmCodes.AGV_Location_Not_Secondary, false);
                        return;
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
                        LOG.TRACE($"[Async Action] Fork is Home Now");
                    }

                    var HSResult = await AGVCOMPTHandshake();

                    if (!HSResult.confirm)
                    {
                        AlarmManager.AddAlarm(HSResult.alarmCode, false);
                        await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: HSResult.alarmCode);
                        return;
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
                        if (action == ACTION_TYPE.Unload)
                            await WaitCSTIDReported();
                        await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: cst_read_fail_alarm);
                    }
                    else
                    {
                        if (action == ACTION_TYPE.Unload)
                            await WaitCSTIDReported();
                        await base.HandleAGVCActionSucceess();
                    }
                }

            });
        }

        private async Task ForkHomeProcess()
        {
            if (ForkLifter != null && !Agv.Parameters.LDULD_Task_No_Entry)
            {
                IsNeedWaitForkHome = true;
                forkGoHomeTask = await Task.Factory.StartNew(async () =>
                {
                    LOG.TRACE($"Wait Reach Tag {RunningTaskData.Destination}, Fork Will Start Go Home.");
                    while (Agv.BarcodeReader.CurrentTag != RunningTaskData.Destination)
                    {
                        await Task.Delay(1);
                        if (Agv.Sub_Status == SUB_STATUS.DOWN)
                        {
                            IsNeedWaitForkHome = false;
                            return;
                        }
                    }
                    LOG.TRACE($"Reach Tag {RunningTaskData.Destination}!, Fork Start Go Home NOW!!!");
                    (bool confirm, AlarmCodes alarm_code) ForkGoHomeActionResult = (false, AlarmCodes.None);
                    await Agv.Laser.SideLasersEnable(true);
                    await RegisterSideLaserTriggerEvent();
                    while (Agv.ForkLifter.CurrentForkLocation != FORK_LOCATIONS.HOME)
                    {
                        await Task.Delay(1);
                        if (Agv.Sub_Status == SUB_STATUS.DOWN)
                        {
                            IsNeedWaitForkHome = false;
                            return;
                        }
                        ForkGoHomeActionResult = await ForkLifter.ForkGoHome();
                        LOG.TRACE($"[Fork Home Process At Secondary]ForkHome Confirm= {ForkGoHomeActionResult.confirm}/Z-Axis Position={Agv.ForkLifter.CurrentForkLocation}");
                        if (ForkGoHomeActionResult.confirm && Agv.ForkLifter.CurrentForkLocation != FORK_LOCATIONS.HOME)
                        {
                            AlarmManager.AddWarning(AlarmCodes.Fork_Go_Home_But_Home_Sensor_Signal_Error);
                            break;
                        }
                    }
                    await UnRegisterSideLaserTriggerEvent();
                    await Task.Delay(500);
                    await Agv.Laser.SideLasersEnable(false);
                    if (!ForkGoHomeActionResult.confirm)
                    {
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                        AlarmManager.AddAlarm(ForkGoHomeActionResult.alarm_code, false);
                    }
                });

            }
            else
                IsNeedWaitForkHome = false;
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
            var result = await ForkLifter.ForkGoTeachedPoseAsync(destineTag, 0, FORK_HEIGHT_POSITION.DOWN_, 0.5);
            return result;

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
            var reader_valid_id = Agv.CSTReader.ValidCSTID.Trim();
            var reader_actual_read_id = Agv.CSTReader.Data.data.Trim().ToUpper();


            if (Agv.Parameters.CSTIDReadNotMatchSimulation)
            {
                LOG.ERROR($"[ID NOT MATCH SIMULATION] AGVS CST Download: {cst_id_expect}, CST READER : {reader_valid_id}");
                return (false, AlarmCodes.Cst_ID_Not_Match);
            }


            if (reader_valid_id == "ERROR" | reader_actual_read_id == "ERROR" | reader_valid_id == "TrayUnknow")
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
