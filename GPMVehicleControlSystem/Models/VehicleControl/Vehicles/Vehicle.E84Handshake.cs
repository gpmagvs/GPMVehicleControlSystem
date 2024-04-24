using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Modbus.Device;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        public enum HANDSHAKE_AGV_TIMEOUT
        {
            Normal,
            T1_TR_REQ_ON,
            T2_AGV_BUSY_ON,
            T3_AGV_BUSY_OFF,
            T4_AGV_READY_OFF,
            T5_AGV_BUSY_2,
            T6_AGV_COMPT_OFF
        }
        public enum HANDSHAKE_EQ_TIMEOUT
        {
            TA1_Wait_L_U_REQ_ON,
            TA2_Wait_EQ_READY_ON,
            TA3_Wait_EQ_BUSY_ON,
            TA4_Wait_EQ_BUSY_OFF,
            TA5_Wait_L_U_REQ_OFF,
            TP_3_Wait_AGV_BUSY_OFF,
            TP_5_Wait_AGV_BUSY_OFF,
        }
        private Dictionary<HANDSHAKE_EQ_TIMEOUT, Stopwatch> EQHSTimersStopwatches = new Dictionary<HANDSHAKE_EQ_TIMEOUT, Stopwatch>() {
            {  HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON , new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON , new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON , new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF ,new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF , new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TP_3_Wait_AGV_BUSY_OFF, new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TP_5_Wait_AGV_BUSY_OFF , new Stopwatch()}
        };
        public Dictionary<HANDSHAKE_EQ_TIMEOUT, double> EQHSTimers = new Dictionary<HANDSHAKE_EQ_TIMEOUT, double>() {
            {  HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TP_3_Wait_AGV_BUSY_OFF, 0},
            {  HANDSHAKE_EQ_TIMEOUT.TP_5_Wait_AGV_BUSY_OFF , 0}
        };

        public enum EQ_HSSIGNAL
        {
            EQ_READY,
            EQ_BUSY,
            EQ_L_REQ,
            EQ_U_REQ,
            EQ_GO
        }
        public enum AGV_HSSIGNAL
        {
            AGV_VALID,
            AGV_READY,
            AGV_TR_REQ,
            AGV_BUSY,
            AGV_COMPT,
        }

        public class clsHandshakeSignalState
        {
            public static int FlickDelayTime => StaStored.CurrentVechicle.Parameters.HandshakeIOFlickDelayTime;
            public clsHandshakeSignalState(EQ_HSSIGNAL signal_name)
            {
                this.signal_name = signal_name;
            }
            public bool _state = false;
            public bool lastNewInput = false;
            private Stopwatch stopwatch = new Stopwatch();
            private readonly EQ_HSSIGNAL signal_name;
            public event EventHandler OnSignalOFF;
            public event EventHandler OnSignalON;
            CancellationTokenSource cts = null;
            Task _change_wait_task = null;

            public bool State
            {
                get => _state;
                set
                {
                    if (lastNewInput != value)
                    {
                        if (_change_wait_task != null)
                        {
                            cts.Cancel();
                        }
                        _change_wait_task = Task.Run(async () =>
                        {
                            //開始計時 300 毫秒,如果沒有被中斷 表示訊號有變化 
                            try
                            {
                                cts = new CancellationTokenSource();
                                await Task.Delay(FlickDelayTime, cts.Token);
                                bool is_changed = _state != lastNewInput;
                                _state = lastNewInput;
                                if (is_changed)
                                {
                                    LOG.TRACE($"[EQ交握訊號監視] {signal_name} Change to {(_state ? 1 : 0)}");
                                    if (!_state)
                                        OnSignalOFF?.Invoke(this, EventArgs.Empty);
                                    else
                                        OnSignalON?.Invoke(this, EventArgs.Empty);
                                }
                                _change_wait_task = null;
                            }
                            catch (Exception)
                            {
                                LOG.WARN($"{signal_name}-Signal Flick! (Change to {(lastNewInput ? 1 : 0)} in short time(300ms))");

                            }
                        });

                        lastNewInput = value;

                    }
                }
            }

            public void ClearEventRegist()
            {
                OnSignalOFF = OnSignalON = null;
            }
        }

        private bool IsHandShakeBypass => Parameters.EQHandshakeBypass;

        internal AlarmCodes AlarmCodeWhenHandshaking = AlarmCodes.None;

        public Dictionary<EQ_HSSIGNAL, clsHandshakeSignalState> EQHsSignalStates = new Dictionary<EQ_HSSIGNAL, clsHandshakeSignalState>()
        {
            { EQ_HSSIGNAL.EQ_L_REQ, new clsHandshakeSignalState(EQ_HSSIGNAL.EQ_L_REQ)},
            { EQ_HSSIGNAL.EQ_U_REQ,  new clsHandshakeSignalState(EQ_HSSIGNAL.EQ_U_REQ) },
            { EQ_HSSIGNAL.EQ_READY,  new clsHandshakeSignalState(EQ_HSSIGNAL.EQ_READY) },
            { EQ_HSSIGNAL.EQ_BUSY,  new clsHandshakeSignalState(EQ_HSSIGNAL.EQ_BUSY) },
            { EQ_HSSIGNAL.EQ_GO,  new clsHandshakeSignalState(EQ_HSSIGNAL.EQ_GO) },
        };
        public Dictionary<AGV_HSSIGNAL, bool> AGVHsSignalStates = new Dictionary<AGV_HSSIGNAL, bool>()
        {
            { AGV_HSSIGNAL.AGV_VALID, false },
            { AGV_HSSIGNAL.AGV_TR_REQ, false },
            { AGV_HSSIGNAL.AGV_BUSY, false },
            { AGV_HSSIGNAL.AGV_READY, false },
            { AGV_HSSIGNAL.AGV_COMPT, false },
        };

        private CancellationTokenSource hs_abnormal_happen_cts = new CancellationTokenSource();

        private bool _IsEQGoOFF_When_Handshaking = false;
        private bool _IsAGVAbnormal_when_handshaking = false;
        private bool _IsEQAbnormal_when_handshaking = false;
        private bool _IsEQBusy_when_AGV_Busy = false;
        private bool _IsEQREQOFF_when_wait_EQREADY_when_handshaking = false;
        private bool _IsEQ_READYOFF_when_handshaking = false;

        private bool IsEQGoOFF_When_Handshaking { get => _IsEQGoOFF_When_Handshaking; set { _IsEQGoOFF_When_Handshaking = value; IsAGVAbnormal_when_handshaking = false; } }
        private bool IsAGVAbnormal_when_handshaking { get => _IsAGVAbnormal_when_handshaking; set { _IsAGVAbnormal_when_handshaking = value; } }
        private bool IsEQAbnormal_when_handshaking { get => _IsEQAbnormal_when_handshaking; set { _IsEQAbnormal_when_handshaking = value; IsAGVAbnormal_when_handshaking = false; } }
        private bool IsEQBusy_when_AGV_Busy { get => _IsEQBusy_when_AGV_Busy; set { _IsEQBusy_when_AGV_Busy = value; IsAGVAbnormal_when_handshaking = false; } }
        private bool IsEQREQOFF_when_wait_EQREADY_when_handshaking { get => _IsEQREQOFF_when_wait_EQREADY_when_handshaking; set { _IsEQREQOFF_when_wait_EQREADY_when_handshaking = value; IsAGVAbnormal_when_handshaking = false; } }
        private bool IsEQ_READYOFF_when_handshaking { get => _IsEQ_READYOFF_when_handshaking; set { _IsEQ_READYOFF_when_handshaking = value; IsAGVAbnormal_when_handshaking = false; } }

        private bool IsULReqOn(ACTION_TYPE action)
        {
            if (action == ACTION_TYPE.Load)
                return EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ].State;
            else
                return EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ].State;
        }

        private bool IsEQReadyOn()
        {
            return EQHsSignalStates[EQ_HSSIGNAL.EQ_READY].State;
        }
        internal bool IsEQGOOn()
        {
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.MODBUS)
                return true;

            bool _simulate_eq_go_on = Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION && Parameters.EQHandshakeSimulationAutoRun;
            if (_simulate_eq_go_on)
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(20);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_GO, true);
                });
            }
            return EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].State;
        }
        private bool IsEQBusyOn()
        {
            return EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].State;

        }

        #region 交握

        private async Task SetAGVBUSY(bool value, bool isInEQ = true)
        {
            await WagoDO.SetState(DO_ITEM.AGV_BUSY, value);
            HandshakeStatusText = value ? "AGV動作中" : "AGV動作完成";
            if (value)
                EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].OnSignalON += HandleEQBusyONAfterAGVBUSY;
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION && Parameters.EQHandshakeSimulationAutoRun && !value) //AGV_BUSY OFF
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    if (isInEQ)
                    {
                        await WagoDO.SetState(DO_ITEM.EMU_EQ_BUSY, true);
                        await Task.Delay(1000);
                        await WagoDO.SetState(DO_ITEM.EMU_EQ_BUSY, false);
                    }
                    else
                    {
                        //模擬EQ未等COMPT OFF就提前OFF
                        await WagoDO.SetState(DO_ITEM.EMU_EQ_READY, false);
                        await WagoDO.SetState(DO_ITEM.EMU_EQ_L_REQ, false);
                        await WagoDO.SetState(DO_ITEM.EMU_EQ_U_REQ, false);
                    }
                });
            }
        }
        private async Task SetAGVREADY(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_READY, value);

            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION && Parameters.EQHandshakeSimulationAutoRun && value)
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_BUSY, true);
                });
            }

        }
        private async Task SetAGVVALID(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_VALID, value);
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION && Parameters.EQHandshakeSimulationAutoRun && value)
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_L_REQ, true);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_U_REQ, true);
                });
            }

        }
        internal async Task SetAGV_TR_REQ(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_TR_REQ, value);
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION && Parameters.EQHandshakeSimulationAutoRun && value)
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_READY, true);
                });
            }


        }
        private async Task SetAGV_COMPT(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_COMPT, value);
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION && Parameters.EQHandshakeSimulationAutoRun && value)
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_READY, false);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_L_REQ, false);
                    await WagoDO.SetState(DO_ITEM.EMU_EQ_U_REQ, false);
                });
            }
        }
        internal bool IsEQHsSignalInitialState()
        {
            return !IsEQBusyOn() && !IsEQReadyOn() && !IsULReqOn(ACTION_TYPE.Unload) && !IsULReqOn(ACTION_TYPE.Load);
        }
        public clsDynamicTrafficState DynamicTrafficState { get; internal set; } = new clsDynamicTrafficState();

        internal void ResetHSTimersAndEvents()
        {
            foreach (var timer_key in EQHSTimers.Keys)
            {
                EQHSTimers[timer_key] = 0;
                EQHSTimersStopwatches[timer_key].Reset();
            }

            foreach (var item in EQHsSignalStates.Values)
            {
                item.ClearEventRegist();
            }
        }
        internal async Task<(bool done, AlarmCodes alarmCode)> Handshake_AGV_BUSY_ON(bool isBackToHome)
        {
            if (isBackToHome)
            {
                await SetAGVREADY(false);
            }
            await SetAGVBUSY(true);
            return (true, AlarmCodes.None);
        }

        private void HandleEQBusyONAfterAGVBUSY(object? sender, EventArgs e)
        {
            EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].OnSignalON -= HandleEQBusyONAfterAGVBUSY;
            if (AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY])
            {
                LOG.Critical("EQ Busy ON when AGV Busy!!");
                IsEQBusy_when_AGV_Busy = true;
                hs_abnormal_happen_cts.Cancel();
                ExecutingTaskEntity.Abort(AlarmCodes.Handshake_Fail_EQ_Busy_ON_When_AGV_BUSY);
            }
        }

        /// <summary>
        /// 開始與EQ進行交握;
        /// - AGV VALID ON => 等待L/U_REQ ON => TR_REQ ON => 等待EQ READY ON
        /// </summary>
        internal async Task<(bool eqready, AlarmCodes alarmCode)> WaitEQReadyON(ACTION_TYPE action)
        {
            hs_abnormal_happen_cts = new CancellationTokenSource();
            IsEQGoOFF_When_Handshaking = IsEQREQOFF_when_wait_EQREADY_when_handshaking = IsAGVAbnormal_when_handshaking = IsEQAbnormal_when_handshaking = IsEQBusy_when_AGV_Busy = false;

            if (Parameters.LDULD_Task_No_Entry)
                return (true, AlarmCodes.None);

            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.PIO)
                WatchE84EQGOSignalWhenHSStart();

            EQ_HSSIGNAL _LU_SIGNAL = action == ACTION_TYPE.Load ? EQ_HSSIGNAL.EQ_L_REQ : EQ_HSSIGNAL.EQ_U_REQ;
            await SetAGVVALID(true);
            StartWatchAGVStatusAsync();
            try
            {
                (bool success, AlarmCodes alarm_code) _result = await HandshakeWith(_LU_SIGNAL, HS_SIGNAL_STATE.ON, HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON, action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_TA1_EQ_L_REQ : AlarmCodes.Handshake_Fail_TA1_EQ_U_REQ);
                if (!_result.success)
                    return _result;
                await SetAGV_TR_REQ(true);
                EQHsSignalStates[_LU_SIGNAL].OnSignalOFF += HandleEQ_LUREQ_OFF;
                _result = await HandshakeWith(EQ_HSSIGNAL.EQ_READY, HS_SIGNAL_STATE.ON, HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON, AlarmCodes.Handshake_Fail_TA2_EQ_READY);
                EQHsSignalStates[_LU_SIGNAL].OnSignalOFF -= HandleEQ_LUREQ_OFF;
                if (!_result.success)
                    return _result;
                EQHsSignalStates[EQ_HSSIGNAL.EQ_READY].OnSignalOFF += HandleEQReadOFF;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                return (false, AlarmCodes.Handshake_Fail);
            }
            return (true, AlarmCodes.None);
        }

        private async void StartWatchAGVStatusAsync()
        {
            LOG.TRACE($"Start Watch AGV Status");
            await Task.Run(() =>
            {
                while (AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID])
                {
                    Thread.Sleep(1);
                    if (GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        if (!IsEQAbnormal_when_handshaking && !IsEQBusy_when_AGV_Busy && !IsEQGoOFF_When_Handshaking && !IsEQREQOFF_when_wait_EQREADY_when_handshaking)
                        {
                            IsAGVAbnormal_when_handshaking = true;
                            hs_abnormal_happen_cts.Cancel();
                            LOG.TRACE($"Watch AGV Status Finish(AGV DOWN)");
                        }
                        return;
                    }
                }
                LOG.TRACE($"Watch AGV Status Finish(Handshake Finish)");
            });
        }

        private void HandleEQReadOFF(object? sender, EventArgs e)
        {
            EQHsSignalStates[EQ_HSSIGNAL.EQ_READY].OnSignalOFF -= HandleEQReadOFF;
            if (AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT])
            {
                //normal
                LOG.TRACE($"EQ READY Normal OFF When AGV_COMPT ON");
            }
            else if (EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].State || Parameters.EQHandshakeMethod == EQ_HS_METHOD.MODBUS) //EQ GO ON著/若沒有光IO則不管
            {
                LOG.WARN($"EQ READY OFF When Handshaking running");
                IsEQAbnormal_when_handshaking = true;
                hs_abnormal_happen_cts.Cancel();
                ExecutingTaskEntity.Abort(AlarmCodes.Handshake_Fail_EQ_READY_OFF);
            }
        }

        private void HandleEQ_LUREQ_OFF(object sender, EventArgs e)
        {
            EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ].OnSignalOFF -= HandleEQ_LUREQ_OFF;
            EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ].OnSignalOFF -= HandleEQ_LUREQ_OFF;
            IsEQREQOFF_when_wait_EQREADY_when_handshaking = true;
            hs_abnormal_happen_cts.Cancel();
        }

        /// <summary>
        /// 等待EQ交握訊號 BUSY OFF＝＞表示ＡＧＶ可以退出了(模擬模式下:用CST在席有無表示是否BUSY結束 LOAD=>貨被拿走. Unload=>貨被放上來)
        /// </summary>
        internal async Task<(bool eq_busy_off, AlarmCodes alarmCode)> WaitEQBusyOnAndOFF(ACTION_TYPE action)
        {
            if (Parameters.LDULD_Task_No_Entry)
                return (true, AlarmCodes.None);
            DirectionLighter.WaitPassLights();
            await SetAGVBUSY(false, true);
            await SetAGVREADY(true);
            AlarmCodes alarm_code = AlarmCodes.None;

            try
            {
                (bool success, AlarmCodes alarm_code) _result = await HandshakeWith(EQ_HSSIGNAL.EQ_BUSY, HS_SIGNAL_STATE.ON, HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON, AlarmCodes.Handshake_Fail_TA3_EQ_BUSY_ON);
                if (!_result.success)
                    return _result;

                _result = await HandshakeWith(EQ_HSSIGNAL.EQ_BUSY, HS_SIGNAL_STATE.OFF, HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF, AlarmCodes.Handshake_Fail_TA4_EQ_BUSY_OFF);
                if (!_result.success)
                    return _result;

            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                return (false, AlarmCodes.Handshake_Fail);
            }
            return (true, AlarmCodes.None);
        }

        /// <summary>
        /// 結束EQ交握_等待EQ READY OFF
        /// </summary>
        internal async Task<(bool eqready_off, AlarmCodes alarmCode)> WaitEQReadyOFF(ACTION_TYPE action)
        {

            if (Parameters.LDULD_Task_No_Entry)
                return (true, AlarmCodes.None);
            EQ_HSSIGNAL _LU_SIGNAL = action == ACTION_TYPE.Load ? EQ_HSSIGNAL.EQ_L_REQ : EQ_HSSIGNAL.EQ_U_REQ;
            AlarmCodes _TimeoutAlarmCode = _LU_SIGNAL == EQ_HSSIGNAL.EQ_L_REQ ? AlarmCodes.Handshake_Fail_TA5_EQ_L_REQ : AlarmCodes.Handshake_Fail_TA5_EQ_U_REQ;
            await SetAGVBUSY(false, false);
            await Task.Delay(200);
            await SetAGV_COMPT(true);

            try
            {
                (bool success, AlarmCodes alarm_code) _result = await HandshakeWith(_LU_SIGNAL, HS_SIGNAL_STATE.OFF, HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF, _TimeoutAlarmCode);
                if (!_result.success)
                    return _result;

                _result = await HandshakeWith(EQ_HSSIGNAL.EQ_READY, HS_SIGNAL_STATE.OFF, HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF, AlarmCodes.Handshake_Fail_TA5_EQ_READY_NOT_OFF);
                if (!_result.success)
                    return _result;

                await SetAGV_COMPT(false);
                await SetAGV_TR_REQ(false);
                await SetAGVVALID(false);
                LOG.INFO("[EQ Handshake] EQ READY OFF=>Handshake Done");
                _ = Task.Run(async () =>
                {
                    HandshakeStatusText = "Finish!";
                    await Task.Delay(1000);
                    HandshakeStatusText = "";
                });
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                return (false, AlarmCodes.Handshake_Fail);
            }
            return (true, AlarmCodes.None);


        }

        #endregion
        private async Task WatchE84EQGOSignalWhenHSStart()
        {
            EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].OnSignalOFF += HandleEQGOOff;
        }

        private void HandleEQGOOff(object? sender, EventArgs e)
        {
            EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].OnSignalOFF -= HandleEQGOOff;
            if (AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID])
            {
                IsEQGoOFF_When_Handshaking = true;
                hs_abnormal_happen_cts.Cancel();
                ExecutingTaskEntity.Abort(AlarmCodes.Handshake_Fail_EQ_GO);
            }
        }

        internal void StopAllHandshakeTimer()
        {
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TP_3_Wait_AGV_BUSY_OFF);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TP_5_Wait_AGV_BUSY_OFF);

        }



        private void StartTimer(HANDSHAKE_EQ_TIMEOUT timer)
        {
            EQHSTimers[timer] = 0;
            var _stopwatch = EQHSTimersStopwatches[timer];
            _stopwatch.Start();
            Task.Factory.StartNew(async () =>
            {
                while (_stopwatch.IsRunning)
                {
                    await Task.Delay(100);
                    EQHSTimers[timer] = double.Parse(_stopwatch.ElapsedMilliseconds / 1000.0 + "");
                }
            });

        }

        private void EndTimer(HANDSHAKE_EQ_TIMEOUT timer)
        {
            EQHSTimersStopwatches[timer].Stop();
        }



        public async Task EQTimeoutDetectionTest(HANDSHAKE_AGV_TIMEOUT test_item)
        {
            await Task.Delay(1).ContinueWith((t) =>
            {
                SetAGVVALID(true);

            });
        }
        private enum HS_SIGNAL_STATE
        {
            ON, OFF
        }


        private async Task<(bool success, AlarmCodes alarm_code)> HandshakeWith(Enum Signal, HS_SIGNAL_STATE EXPECTED_State, HANDSHAKE_EQ_TIMEOUT Timer, AlarmCodes alarm_code_timeout)
        {
            int timeout = Parameters.EQHSTimeouts[Timer];
            LOG.TRACE($"[EQ Handshake] 等待 {Signal} {EXPECTED_State}-(Timeout_{timeout}) sec");
            if (Signal.GetType().Name == "EQ_HSSIGNAL")
                HandshakeStatusText = CreateHandshakeStatusDisplayText(Signal, EXPECTED_State);
            StartTimer(Timer);
            bool changed_done = WaitHSSignalStateChanged(Signal, EXPECTED_State, timeout, hs_abnormal_happen_cts, HandshakeStatusText);
            LOG.WARN($"[EQ Handshake] {Signal} changed to {EXPECTED_State}, {(changed_done ? "success" : "fail")}");
            EndTimer(Timer);
            AlarmCodes _alarmcode = AlarmCodes.None;
            if (!changed_done || GetSub_Status() == SUB_STATUS.DOWN)
            {
                if (IsAGVAbnormal_when_handshaking && GetSub_Status() == SUB_STATUS.DOWN)
                    _alarmcode = AlarmCodes.Handshake_Fail_AGV_DOWN;
                else if (IsEQGoOFF_When_Handshaking)
                    _alarmcode = AlarmCodes.Handshake_Fail_EQ_GO;
                else if (IsEQREQOFF_when_wait_EQREADY_when_handshaking)
                    _alarmcode = AlarmCodes.Handshake_Fail_EQ_LU_REQ_OFF_WHEN_WAIT_READY;
                else if (IsEQBusy_when_AGV_Busy)
                    _alarmcode = AlarmCodes.Handshake_Fail_EQ_Busy_ON_When_AGV_BUSY;
                else if (IsEQAbnormal_when_handshaking)
                    _alarmcode = AlarmCodes.Handshake_Fail_EQ_READY_OFF;
                else if (IsEQ_READYOFF_when_handshaking)
                    _alarmcode = AlarmCodes.Handshake_Fail_EQ_READY_OFF;
                else
                    _alarmcode = alarm_code_timeout;
                ExecutingTaskEntity.Abort(_alarmcode);
            }
            return (_alarmcode == AlarmCodes.None, _alarmcode);
        }

        private string CreateHandshakeStatusDisplayText(Enum signal, HS_SIGNAL_STATE eXPECTED_State)
        {
            EQ_HSSIGNAL eq_hs = (EQ_HSSIGNAL)signal;
            if (eq_hs == EQ_HSSIGNAL.EQ_L_REQ)
            {
                if (eXPECTED_State == HS_SIGNAL_STATE.ON)
                    return "等待設備[載入]需求訊號開啟";
                else
                    return "等待設備[載入]需求訊號關閉";
            }
            else if (eq_hs == EQ_HSSIGNAL.EQ_U_REQ)
            {
                if (eXPECTED_State == HS_SIGNAL_STATE.ON)
                    return "等待設備[載出]需求訊號開啟";
                else
                    return "等待設備[載出]需求訊號關閉";
            }
            else if (eq_hs == EQ_HSSIGNAL.EQ_READY)
            {
                if (eXPECTED_State == HS_SIGNAL_STATE.ON)
                    return "等待設備READY";
                else
                    return "等待設備READY OFF";
            }
            else if (eq_hs == EQ_HSSIGNAL.EQ_BUSY)
            {
                if (eXPECTED_State == HS_SIGNAL_STATE.ON)
                    return "等待設備開始動作";
                else
                    return "等待設備完成動作";
            }
            else
                return $"等待 {signal} 訊號 {eXPECTED_State}";
        }

        private bool WaitHSSignalStateChanged(Enum Signal, HS_SIGNAL_STATE EXPECTED_State, int timeout = 50, CancellationTokenSource cancellation = null, string handshakeText = "")
        {
            // LOG.TRACE($"[交握訊號變化等待] Wait {Signal} change to {EXPECTED_State}...");
            CancellationTokenSource _cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (_GetState(Signal) != (EXPECTED_State == HS_SIGNAL_STATE.ON) ? true : false)
            {
                Thread.Sleep(1);
                _HandshakeStatusText = handshakeText + $"-{stopwatch.Elapsed}";
                if (cancellation != null && cancellation.IsCancellationRequested)
                {
                    //LOG.Critical($"[交握訊號變化等待] {Signal} Wait changed to {EXPECTED_State} Interupt!!!!!");
                    stopwatch.Stop();
                    return false;
                }
                if (_cts.IsCancellationRequested)
                {
                    //LOG.Critical($"[交握訊號變化等待] {Signal} Wait changed to {EXPECTED_State} Timeout!!!!!");
                    stopwatch.Stop();
                    return false;
                }
            }
            //LOG.TRACE($"[交握訊號變化等待] {Signal} changed to {EXPECTED_State}!");
            stopwatch.Stop();
            return true;

            ///private local function
            bool _GetState(Enum _Signal)
            {
                if (_Signal.GetType().Name == "EQ_HSSIGNAL")
                {
                    return EQHsSignalStates[(EQ_HSSIGNAL)Signal].State;
                }
                else if (_Signal.GetType().Name == "AGV_HSSIGNAL")
                {
                    return AGVHsSignalStates[(AGV_HSSIGNAL)Signal];
                }
                else
                    return false;
            }
        }
    }
}
