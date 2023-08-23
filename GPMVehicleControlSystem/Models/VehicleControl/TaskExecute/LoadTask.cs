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
                return AppSettingsHelper.GetValue<bool>("VCS:CST_READER_TRIGGER");
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
                    LOG.WARN($"[{action}] Tag_{destineTag} Handshake Mode Not Defined!");
                    return WORKSTATION_HS_METHOD.NO_HS;
                }
            }
        }

        internal bool back_to_secondary_flag = false;
        private WORKSTATION_HS_METHOD _eqHandshakeMode;

        public LoadTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_START);

            (bool confirm, AlarmCodes alarmCode) CstExistCheckResult = CstExistCheckBeforeHSStartInFrontOfEQ();
            if (!CstExistCheckResult.confirm)
                return (false, CstExistCheckResult.alarmCode);

            (bool confirm, AlarmCodes alarmCode) CstBarcodeCheckResult = await CSTBarcodeReadBeforeAction();

            if (!CstBarcodeCheckResult.confirm)
                return (false, CstBarcodeCheckResult.alarmCode);


            if (eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
            {
                if (Agv.EQ_HS_Method == Vehicle.EQ_HS_METHOD.MODBUS)
                {
                    var eqModbusConn = await Agv.ModbusTcpConnect();
                    if (!eqModbusConn)
                    {
                        return (false, AlarmCodes.Waiting_EQ_Handshake);
                    }
                }

                (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyON(action);
                if (!HSResult.eqready)
                {
                    return (false, HSResult.alarmCode);
                }
            }
            return await base.BeforeTaskExecuteActions();
        }


        protected override async Task<(bool, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            Agv.DirectionLighter.CloseAll();
            (bool hs_success, AlarmCodes alarmCode) HSResult = new(false, AlarmCodes.None);
            _eqHandshakeMode = eqHandshakeMode;
            if (_eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
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
                bool armMoveing = false;

                var isNeedArmExtend = Agv.WorkStations.Stations[destineTag].ForkArmExtend;

                if (isNeedArmExtend)
                {
                    armMoveing = await ForkLifter.ForkExtendOutAsync();
                    arm_move_Done = WaitForkArmMoveDone(FORK_ARM_LOCATIONS.END);
                }
                else
                {
                    arm_move_Done = true;
                }
                if (arm_move_Done)
                {
                    await ChangeForkPositionInWorkStation();
                    if (isNeedArmExtend)
                    {
                        armMoveing = await ForkLifter.ForkShortenInAsync();
                        arm_move_Done = WaitForkArmMoveDone(FORK_ARM_LOCATIONS.HOME);
                        if (!arm_move_Done)
                            return (false, AlarmCodes.Fork_Arm_Pose_Error);
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
                Agv.DirectionLighter.Backward(delay: 800);
                RunningTaskData = RunningTaskData.TurnToBackTaskData();
                Agv.ExecutingTask.RunningTaskData = RunningTaskData;

                if(AGVCActionStatusChaged!=null)
                    AGVCActionStatusChaged=null;
                AGVCActionStatusChaged += WaitAGVStatusChangeToSucces;

                await Agv.AGVC.AGVSTaskDownloadHandler(RunningTaskData);
            });


            return (true, AlarmCodes.None);
        }

        private async void WaitAGVStatusChangeToSucces(ActionStatus status)
        {
            if (status == ActionStatus.SUCCEEDED)
            {
                    if(Agv.Sub_Status == SUB_STATUS.DOWN){
return;
                    }
                Agv.AGVC.OnAGVCActionChanged -= WaitAGVStatusChangeToSucces;
                back_to_secondary_flag = true;
                if (_eqHandshakeMode == WORKSTATION_HS_METHOD.HS)
                {
                    (bool eqready, AlarmCodes alarmCode) HSResult = await Agv.WaitEQReadyOFF(action);
                    if (!HSResult.eqready)
                    {
                        AlarmManager.AddAlarm(HSResult.alarmCode, false);
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                        Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);

                    }
                }

                if (ForkLifter != null)
                {
                    await ForkLifter.ForkGoHome();
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

        protected async virtual Task ChangeForkPositionInWorkStation()
        {
            await ForkLifter.ForkGoTeachedPoseAsync(destineTag, 0, FORK_HEIGHT_POSITION.DOWN_, 0.3);

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
        public override async void LaserSettingBeforeTaskExecute()
        {
            Agv.Laser.AllLaserDisable();
            await Agv.Laser.ModeSwitch(LASER_MODE.Loading);
        }

        protected virtual async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeReadBeforeAction()
        {
            if (!CSTTrigger)
                return (true, AlarmCodes.None);
            return await CSTBarcodeRead();
        }

        protected virtual async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeReadAfterAction()
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
            if (!AppSettingsHelper.GetValue<bool>("VCS:CST_EXIST_DETECTION:Before_In"))
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
            if (!AppSettingsHelper.GetValue<bool>("VCS:CST_EXIST_DETECTION:After_EQ_Busy_Off"))
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
