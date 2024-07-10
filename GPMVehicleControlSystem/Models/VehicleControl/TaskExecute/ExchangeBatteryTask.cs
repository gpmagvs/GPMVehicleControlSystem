using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Newtonsoft.Json.Linq;
using RosSharp.RosBridgeClient.Actionlib;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsBattery;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class ExchangeBatteryTask : TaskBase
    {
        private readonly TsmcMiniAGV TsmcMiniAGV;
        private bool IsBat1Unlock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Unlock_Sensor);
        private bool IsBat2Unlock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Unlock_Sensor);
        private bool IsBat1Lock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Lock_Sensor);
        private bool IsBat2Lock => TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Lock_Sensor);

        public BATTERY_LOCATION Inspefic_Bat_loc = BATTERY_LOCATION.NAN;
        public EXCHANGE_BAT_ACTION Inspefic_Action = EXCHANGE_BAT_ACTION.BOTH;
        public bool Debugging = false;
        //
        public clsBatExchangeTimeout timouts => TsmcMiniAGV.Parameters.InspectionAGV.BatExchangeTimeout;

        public enum EXCHANGE_BAT_ACTION
        {
            REMOVE_BATTERY,
            RELOAD_BATTERY,
            BOTH = 404,
        }
        public ExchangeBatteryTask() : base()
        {

        }
        public ExchangeBatteryTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            TsmcMiniAGV = Agv as TsmcMiniAGV;
        }

        protected override Task<CarController.SendActionCheckResult> TransferTaskToAGVC()
        {
            return base.TransferTaskToAGVC();
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
            Agv.Laser.AllLaserDisable();
            return await Agv.Laser.ModeSwitch(VehicleComponent.clsLaser.LASER_MODE.Bypass);
        }
        internal class clsBatInfo
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
        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            TsmcMiniAGV.IsHandshaking = true;
            TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}電池交換開始";
            BuzzerPlayer.ExchangeBattery();

            #region 電池交換交握
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

                if (Inspefic_Bat_loc != BATTERY_LOCATION.NAN)
                {
                    ushort _id = (ushort)(((int)Inspefic_Bat_loc) + 1);
                    batInfos = new clsBatInfo[]
                    {
                         new clsBatInfo(TsmcMiniAGV, (int)Inspefic_Bat_loc)
                         {
                             location = Inspefic_Bat_loc,
                             level   = TsmcMiniAGV.Batteries[_id].Data.batteryLevel
                         }
                    };
                }
                else
                {
                    batInfos = batInfos.OrderBy(bat => bat.level).ToList().FindAll(bat => bat.level <= Agv.Parameters.InspectionAGV.ExchangeBatLevelThresholdVal).ToArray();
                }
                string _batNosString = string.Join("&", batInfos.Select(bat => bat.bat_no));
                TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}交換電池 : {_batNosString}";
                await Task.Delay(1000);
                if (Agv.Parameters.InspectionAGV.BatteryChangeNum < 2 && batInfos.Count() == 2)
                {
                    batInfos = new clsBatInfo[1] { batInfos[0] };
                }


                //check progress
                //await PreCheckStatus();


                //
                foreach (var bat in batInfos)
                {
                    bool _isReloadACtion = false;
                    if (Inspefic_Action == EXCHANGE_BAT_ACTION.BOTH)
                    {
                        if (bat.IsExist)
                            await HandshakeWithExchanger(bat, EXCHANGE_BAT_ACTION.REMOVE_BATTERY);
                        _isReloadACtion = true;
                        await HandshakeWithExchanger(bat, EXCHANGE_BAT_ACTION.RELOAD_BATTERY);
                    }
                    else
                    {
                        if (Inspefic_Action == EXCHANGE_BAT_ACTION.RELOAD_BATTERY)
                        {
                            _isReloadACtion = true;
                            await HandshakeWithExchanger(bat, EXCHANGE_BAT_ACTION.RELOAD_BATTERY);
                        }
                        else
                            await HandshakeWithExchanger(bat, EXCHANGE_BAT_ACTION.REMOVE_BATTERY);

                    }
                    TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}電池-{bat.bat_no} 交換完成";
                }

            }
            catch (HandshakeException ex)
            {
                BuzzerPlayer.Alarm();
                TsmcMiniAGV.IsHandshaking = false;

                if (Debugging)
                {
                    StaStored.CurrentVechicle.SetSub_Status(SUB_STATUS.DOWN);
                    AlarmManager.AddAlarm(ex.alarm_code, false);
                }
                ResetPIOSignals();
                return (false, ex.alarm_code);
            }
            catch (HSTimeoutException ex)
            {
                if (Debugging)
                {
                    StaStored.CurrentVechicle.SetSub_Status(SUB_STATUS.DOWN);
                    AlarmManager.AddAlarm(ex.alarm_code, false);
                }
                TsmcMiniAGV.IsHandshaking = false;
                ResetPIOSignals();
                return (false, ex.alarm_code);
            }
            #endregion
            TsmcMiniAGV.IsHandshaking = false;
            TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}電池交換完成，退至定位點..";
            //退至二次定位點
            return await BackwardToEntryPoint();
        }
        private async Task ResetPIOSignals()
        {
            TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_VALID, false);
            TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_L_REQ, false);
            TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_U_REQ, false);
            TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_READY, false);
            TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_CS_0, false);
            TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_CS_1, false);
        }
        private async Task PreCheckStatus()
        {
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_Check_REQ, true);
            await WaitEQSignal(DI_ITEM.EQ_Check_Result, true, 3, CancellationToken.None);
            await WaitEQSignal(DI_ITEM.EQ_Check_Ready, true, 3, CancellationToken.None);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_Check_REQ, false);
            await WaitEQSignal(DI_ITEM.EQ_Check_Result, false, 3, CancellationToken.None);
            await WaitEQSignal(DI_ITEM.EQ_Check_Ready, false, 3, CancellationToken.None);
        }

        private async Task<(bool success, AlarmCodes alarmCode)> BackwardToEntryPoint()
        {
            BuzzerPlayer.Stop();
            BuzzerPlayer.Action();

            if (Debugging)
            {
                StaStored.CurrentVechicle.SetSub_Status(AGVSystemCommonNet6.clsEnums.SUB_STATUS.IDLE);
                await Task.Delay(3000);
                BuzzerPlayer.Stop();
                return (true, AlarmCodes.None);
            }
            else
            {
                ManualResetEvent _waitReachHomeDone = new ManualResetEvent(false);
                AGVCActionStatusChaged += (status) =>
                {
                    if (status == ActionStatus.SUCCEEDED)
                        _waitReachHomeDone.Set();
                };
                var gotoEntryPointTask = RunningTaskData.CreateGoHomeTaskDownloadData();
                AGVControl.CarController.SendActionCheckResult result = Agv.AGVC.ExecuteTaskDownloaded(gotoEntryPointTask, Agv.Parameters.ActionTimeout).Result;
                if (!result.Accept)
                    return (false, AlarmCodes.Can_not_Pass_Task_to_Motion_Control);

                _waitReachHomeDone.WaitOne();
                logger.Info($"退出至定位點,電池交換完成");
                Agv.SetSub_Status(SUB_STATUS.IDLE);
                return (result.Accept, result.Accept ? AlarmCodes.None : AlarmCodes.Can_not_Pass_Task_to_Motion_Control);
            }
        }

        internal async Task WatchEQVALID(CancellationTokenSource cancellationTokenSource)
        {
            bool _eq_valid_off = false;
            logger.Trace($"[BAT EXG] EQ VALID 訊號監視中...");
            while (TsmcMiniAGV.WagoDO.GetState(DO_ITEM.AGV_VALID))
            {
                await Task.Delay(1);

                if ((_eq_valid_off = !TsmcMiniAGV.WagoDI.GetState(DI_ITEM.EQ_VALID)))
                {
                    cancellationTokenSource.Cancel();
                    break;
                }
            }
            logger.Trace($"[BAT EXG] EQ VALID 訊號監視結束.{(_eq_valid_off ? "EQ 異常" : "")}");
        }

        internal async Task<bool> HandshakeWithExchanger(clsBatInfo bat, EXCHANGE_BAT_ACTION action)
        {
            BATTERY_LOCATION batNo = bat.location;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            DO_ITEM BES = batNo == BATTERY_LOCATION.RIGHT ? DO_ITEM.AGV_CS_1 : DO_ITEM.AGV_CS_0;
            DO_ITEM LDUDLREQ = action == EXCHANGE_BAT_ACTION.REMOVE_BATTERY ? DO_ITEM.AGV_L_REQ : DO_ITEM.AGV_U_REQ;

            //start handshake
            await WaitEQSignal(DI_ITEM.EQ_VALID, true, 3, token);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_VALID, true);
            WatchEQVALID(cancellationTokenSource);

            await TsmcMiniAGV.WagoDO.SetState(BES, true);
            await TsmcMiniAGV.WagoDO.SetState(LDUDLREQ, true);
            await WaitEQSignal(DI_ITEM.EQ_TR_REQ, true, timouts.TP1, token);

            //不論是移出或移入電池，都需要在此解鎖
            (bool unlockSuccess, AlarmCodes unlockAlarmCode) = await _UnlockBattery(bat);
            if (!unlockSuccess)
                throw new HandshakeException(unlockAlarmCode);

            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_READY, true);
            await WaitEQSignal(DI_ITEM.EQ_BUSY, true, timouts.TP2, token);

            CancellationTokenSource tp3Cts = new CancellationTokenSource();
            CancellationTokenSource tp4Cts = new CancellationTokenSource();
            TimeSpan tp3TimeSpan = TimeSpan.FromSeconds(timouts.TP3);
            TimeSpan tp4TimeSpan = TimeSpan.FromSeconds(timouts.TP4);
            TimeSpan tp34TimeSpan = TimeSpan.FromSeconds(timouts.TP3 + timouts.TP4);
            tp3Cts.CancelAfter(tp3TimeSpan); //TP3
            TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}Start Wait Battery-{batNo}  [{action}]  By Exchanger ...";
            Stopwatch _stopwatch = Stopwatch.StartNew();
            bool _IsBatteryInstall_Remove_Flag = false;
            while (TsmcMiniAGV.WagoDI.GetState(DI_ITEM.EQ_BUSY))
            {
                TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}Start Wait Battery-{batNo} [{action}] By Exchanger ...{_stopwatch.Elapsed}/{tp34TimeSpan}";
                await Task.Delay(10);
                if (tp4Cts.IsCancellationRequested)
                {
                    throw new HSTimeoutException(AlarmCodes.Handshake_Fail_BAT_EXG_EQ_BUSY_OFF_TIMEOUT);
                }
                if (TsmcMiniAGV.GetSub_Status() == SUB_STATUS.DOWN)
                    return false;

                if (tp3Cts.IsCancellationRequested && !_IsBatteryInstall_Remove_Flag)
                {
                    AlarmCodes alarmCode = action == EXCHANGE_BAT_ACTION.REMOVE_BATTERY ? AlarmCodes.Handshake_Fail_BAT_Remove_Timeout : AlarmCodes.Handshake_Fail_BAT_Install_Timeout;
                    throw new HSTimeoutException(alarmCode);
                }

                if (!_IsBatteryInstall_Remove_Flag && _IsBatteryExistStateChanged())
                {
                    _IsBatteryInstall_Remove_Flag = true;
                    await TsmcMiniAGV.WagoDO.SetState(LDUDLREQ, false);
                    tp3Cts.Dispose();
                    tp4Cts.CancelAfter(tp4TimeSpan);
                }

                bool _IsBatteryExistStateChanged()
                {
                    bool bat_exist1 = false;
                    bool bat_exist2 = false;

                    if (batNo == BATTERY_LOCATION.RIGHT)
                    {
                        bat_exist1 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Exist_2);
                        bat_exist2 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_1_Exist_3);
                    }
                    else
                    {
                        bat_exist1 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Exist_2);
                        bat_exist2 = TsmcMiniAGV.WagoDI.GetState(DI_ITEM.Battery_2_Exist_3);
                    }
                    return action == EXCHANGE_BAT_ACTION.REMOVE_BATTERY ? !bat_exist1 && !bat_exist2 : bat_exist1 && bat_exist2;
                }
            }

            await WaitEQSignal(DI_ITEM.EQ_COMPT, true, Debugging ? 30 : 8, token);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_READY, false);
            await TsmcMiniAGV.WagoDO.SetState(BES, false);
            await WaitEQSignal(DI_ITEM.EQ_COMPT, false, timouts.TP5, token);
            await TsmcMiniAGV.WagoDO.SetState(DO_ITEM.AGV_VALID, false);
            TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}Battery-{batNo} {action} By Exchanger Success!";

            //如果動作是移入電池，鎖定電池
            if (action == EXCHANGE_BAT_ACTION.RELOAD_BATTERY)
            {
                (bool lockSuccess, AlarmCodes lockAlarmCode) = await _LockBattery(bat, true);
                if (!lockSuccess)
                    throw new HandshakeException(lockAlarmCode);
            }
            await Task.Delay(1000);
            return true;
            #region Local Methods
            async Task<(bool success, AlarmCodes alarmCode)> _UnlockBattery(clsBatInfo bat)
            {
                if (bat.location == BATTERY_LOCATION.RIGHT)
                {
                    TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}{bat.location}-解鎖中";
                    await TsmcMiniAGV.Battery1UnLock();
                    if (!IsBat1Unlock)
                        return (false, AlarmCodes.Battery1_Not_UnLock);
                    else
                        return (true, AlarmCodes.None);
                }
                else
                {
                    TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}{bat.location}-解鎖中";
                    await TsmcMiniAGV.Battery2UnLock();
                    if (!IsBat2Unlock)
                        return (false, AlarmCodes.Battery2_Not_UnLock);
                    else
                        return (true, AlarmCodes.None);
                }
            }

            async Task<(bool success, AlarmCodes alarmCode)> _LockBattery(clsBatInfo bat, bool _isReloadACtion)
            {
                if (bat.location == BATTERY_LOCATION.RIGHT && _isReloadACtion)
                {
                    await TsmcMiniAGV.Battery1Lock();
                    if (!IsBat1Lock)
                        return (false, AlarmCodes.Battery1_Not_Lock);
                    else
                        return (true, AlarmCodes.None);
                }
                else if (bat.location == BATTERY_LOCATION.LEFT && _isReloadACtion)
                {
                    await TsmcMiniAGV.Battery2Lock();
                    if (!IsBat2Lock)
                        return (false, AlarmCodes.Battery2_Not_Lock);
                    else
                        return (true, AlarmCodes.None);
                }
                return (true, AlarmCodes.None);
            }
            #endregion

        }
        private async Task<bool> WaitEQSignal(DI_ITEM input, bool expect_state, int timeout_sec, CancellationToken token)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(timeout_sec));
            Stopwatch _sw = Stopwatch.StartNew();

            if (TsmcMiniAGV.Parameters.InspectionAGV.BatteryExhcnageSimulation)
            {
                await Task.Delay(1000);
                return true;
            }
            while (Agv.WagoDI.GetState(input) != expect_state)
            {
                await Task.Delay(10);

                TsmcMiniAGV.HandshakeStatusText = $"{(Debugging ? "[DEBUG]" : "")}Wait Exchanger-{input}-{(expect_state ? "ON" : "OFF")}...{_sw.Elapsed}/{TimeSpan.FromSeconds(timeout_sec)}";

                if (TsmcMiniAGV.GetSub_Status() == SUB_STATUS.DOWN)
                {
                    throw new HandshakeException(AlarmCodes.Handshake_Fail_AGV_DOWN);
                }

                if (token.IsCancellationRequested)
                {
                    throw new HandshakeException(AlarmCodes.Handshake_Fail_BAT_EXG_EQ_VALID_OFF_WHEN_HANDSHAKING);
                }
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
                    if (input == DI_ITEM.EQ_Check_Result)
                        alarm_code = expect_state ? AlarmCodes.Handshake_Fail_BAT_EXG_EQ_Check_Result_NOT_ON : AlarmCodes.Handshake_Fail_BAT_EXG_EQ_Check_Result_NOT_OFF;
                    if (input == DI_ITEM.EQ_Check_Ready)
                        alarm_code = expect_state ? AlarmCodes.Handshake_Fail_BAT_EXG_EQ_Check_Ready_NOT_ON : AlarmCodes.Handshake_Fail_BAT_EXG_EQ_Check_Ready_NOT_OFF;
                    throw new HSTimeoutException(alarm_code);
                }
            }
            return true;

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
