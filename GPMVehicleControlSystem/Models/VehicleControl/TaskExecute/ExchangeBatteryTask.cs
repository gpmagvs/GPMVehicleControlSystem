using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsBattery;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class ExchangeBatteryTask : TaskBase
    {
        private readonly TsmcMiniAGV TsmcMiniAGV;
        private bool IsBat1Unlock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Unlock_Sensor);
        private bool IsBat2Unlock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Unlock_Sensor);
        private bool IsBat1Lock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Lock_Sensor);
        private bool IsBat2Lock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Lock_Sensor);


        public enum EXCHANGE_BAT_ACTION
        {
            REMOVE_BATTERY,
            RELOAD_BATTERY,
        }

        public ExchangeBatteryTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            TsmcMiniAGV = Agv as TsmcMiniAGV;
        }

        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.ExchangeBattery;

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Flash(new DO_ITEM[4] {
                 DO_ITEM.AGV_DiractionLight_Right,
                 DO_ITEM.AGV_DiractionLight_Right_2,
                 DO_ITEM.AGV_DiractionLight_Left,
                 DO_ITEM.AGV_DiractionLight_Left_2,
            }, 800);
        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            return await Agv.Laser.ModeSwitch(VehicleComponent.clsLaser.LASER_MODE.Bypass);
        }
        private class clsBatInfo
        {
            public clsBatInfo(TsmcMiniAGV agv, int bat_no)
            {
                this.agv = agv;
                this.bat_no = bat_no;
            }
            public BATTERY_LOCATION location;
            public bool IsExist
            {
                get
                {
                    return location == BATTERY_LOCATION.RIGHT ? agv.IsBattery1Exist : agv.IsBattery2Exist;
                }
            }
            public TsmcMiniAGV agv { get; }
            public int bat_no { get; }
            public byte level { get; internal set; }
        }
        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            BuzzerPlayer.ExchangeBattery();
            await TsmcMiniAGV.Battery1UnLock();
            if (!IsBat1Unlock)
                return (false, AlarmCodes.Battery1_Not_UnLock);
            await TsmcMiniAGV.Battery2UnLock();
            if (!IsBat2Unlock)
                return (false, AlarmCodes.Battery2_Not_UnLock);

            try
            {
                //先換低電量的

                clsBatInfo[] batInfos = new clsBatInfo[2]
                {
                    new clsBatInfo(TsmcMiniAGV,1)
                    {
                         location = BATTERY_LOCATION.RIGHT,
                         level = TsmcMiniAGV.Batteries[1].Data.batteryLevel,
                    },
                    new clsBatInfo(TsmcMiniAGV,2){
                         location = BATTERY_LOCATION.LEFT,
                         level = TsmcMiniAGV.Batteries[2].Data.batteryLevel,
                    }
                };

                batInfos = batInfos.OrderBy(bat => bat.level).ToList().FindAll(bat => bat.level <= Agv.Parameters.InspectionAGV.ExchangeBatLevelThresholdVal).ToArray();

                foreach (var bat in batInfos)
                {
                    if (bat.IsExist)
                        await HandshakeWithExchanger(bat.location, EXCHANGE_BAT_ACTION.REMOVE_BATTERY);
                    await HandshakeWithExchanger(bat.location, EXCHANGE_BAT_ACTION.RELOAD_BATTERY);
                    LOG.INFO($"電池-{bat.bat_no} 交換完成");
                }

            }
            catch (HandshakeException ex)
            {
                return (false, ex.alarm_code);
            }
            catch (HSTimeoutException ex)
            {

                return (false, ex.alarm_code);
            }

            await TsmcMiniAGV.Battery1Lock();
            if (!IsBat1Lock)
                return (false, AlarmCodes.Battery1_Not_Lock);
            await TsmcMiniAGV.Battery2Lock();
            if (!IsBat2Lock)
                return (false, AlarmCodes.Battery2_Not_Lock);

            //退至二次定位點
            BuzzerPlayer.Action();
            AGVCActionStatusChaged += OnAGVCBackToEntryPoint;
            var gotoEntryPointTask = RunningTaskData.CreateGoHomeTaskDownloadData();
            AGVControl.CarController.SendActionCheckResult result = Agv.AGVC.ExecuteTaskDownloaded(gotoEntryPointTask, Agv.Parameters.ActionTimeout).Result;

            return (result.Accept, result.Accept? AlarmCodes.None : AlarmCodes.Can_not_Pass_Task_to_Motion_Control);
        }

        private async Task HandshakeWithExchanger(BATTERY_LOCATION batNo, EXCHANGE_BAT_ACTION action)
        {
            DO_ITEM BES = batNo == BATTERY_LOCATION.RIGHT ? DO_ITEM.AGV_CS_0 : DO_ITEM.AGV_CS_1;
            DO_ITEM LDUDLREQ = action == EXCHANGE_BAT_ACTION.REMOVE_BATTERY ? DO_ITEM.AGV_L_REQ : DO_ITEM.AGV_U_REQ;

            await WaitEQSignal(DI_ITEM.EQ_VALID, true, 3);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_VALID, true);
            await TsmcMiniAGV.WagoDO.SetState(BES, true);
            await TsmcMiniAGV.WagoDO.SetState(LDUDLREQ, true);
            await WaitEQSignal(DI_ITEM.EQ_TR_REQ, true, 60);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_READY, true);
            await WaitEQSignal(DI_ITEM.EQ_BUSY, true, 10);

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(30)); //TP3
            while (TsmcMiniAGV.WagoDI.GetState(DI_ITEM.EQ_BUSY))
            {
                await Task.Delay(10);
                if (cts.IsCancellationRequested)
                {
                    throw new HSTimeoutException(AlarmCodes.Handshake_Fail_BAT_Remove_Timeout);
                    return;
                }
                bool IsBatteryOutOfAGV()
                {
                    if (batNo == BATTERY_LOCATION.RIGHT)
                    {
                        bool bat1_exist1 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Exist_1);
                        bool bat1_exist2 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Exist_2);
                        return !bat1_exist1 && !bat1_exist2;
                    }
                    else
                    {
                        bool bat2_exist1 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Exist_1);
                        bool bat2_exist2 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Exist_2);
                        return !bat2_exist1 && !bat2_exist2;
                    }
                }
                if (IsBatteryOutOfAGV())
                {
                    await TsmcMiniAGV.WagoDO.SetState(LDUDLREQ, false);
                    break;
                }
            }

            await WaitEQSignal(DI_ITEM.EQ_BUSY, false, 30);
            await WaitEQSignal(DI_ITEM.EQ_COMPT, true, 3);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_READY, false);
            await TsmcMiniAGV.WagoDO.SetState(BES, false);
            await WaitEQSignal(DI_ITEM.EQ_COMPT, false, 3);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_VALID, false);

        }
        private async Task<bool> WaitEQSignal(DI_ITEM input, bool expect_state, int timeout_sec)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(timeout_sec));
            while (Agv.WagoDI.GetState(input) != expect_state)
            {
                await Task.Delay(10);
                if (cts.IsCancellationRequested)
                {
                    AlarmCodes alarm_code = AlarmCodes.Handshake_Fail;
                    if (input == DI_ITEM.EQ_VALID)
                        alarm_code = expect_state ? AlarmCodes.Handshake_Fail_BAT_EXG_EQ_VALID_NOT_ON : AlarmCodes.Handshake_Fail_BAT_EXG_EQ_VALID_NOT_OFF;
                    if (input == DI_ITEM.EQ_TR_REQ)
                        alarm_code = expect_state ? AlarmCodes.Handshake_Fail_BAT_EXG_EQ_TRREQ_NOT_ON : AlarmCodes.Handshake_Fail_BAT_EXG_EQ_TRREQ_NOT_OFF;
                    if (input == DI_ITEM.EQ_BUSY)
                        alarm_code = expect_state ? AlarmCodes.Handshake_Fail_EQ_BUSY_NOT_ON : AlarmCodes.Handshake_Fail_EQ_BUSY_NOT_OFF;
                    if (input == DI_ITEM.EQ_COMPT)
                        alarm_code = expect_state ? AlarmCodes.Handshake_Fail_BAT_EXG_EQ_COMPT_NOT_ON : AlarmCodes.Handshake_Fail_BAT_EXG_EQ_COMPT_NOT_OFF;

                    throw new HSTimeoutException(alarm_code);
                }

            }

            return true;

        }
        private void OnAGVCBackToEntryPoint(ActionStatus status)
        {
            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                AGVCActionStatusChaged -= OnAGVCBackToEntryPoint;
                return;
            }
            LOG.WARN($"[ {RunningTaskData.Task_Simplex} -{action}-Back To Entry Point of Bat-Exanger] AGVC Action Status Changed: {status}.");

            if (status == ActionStatus.SUCCEEDED)
            {
                AGVCActionStatusChaged = null;
                LOG.INFO($"電池交換完成");
                base.HandleAGVCActionSucceess();
            }
        }

    }
    public class HandshakeException : Exception
    {
        public HandshakeException(AlarmCodes alarm)
        {
            this.alarm_code = alarm;
        }

        public AlarmCodes alarm_code { get; }
    }
    public class HSTimeoutException : TimeoutException
    {
        public readonly AlarmCodes alarm_code;

        public HSTimeoutException(AlarmCodes alarm)
        {
            this.alarm_code = alarm;
        }
    }
}
