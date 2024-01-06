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
            TA5_Wait_L_U_REQ_OFF
        }
        private Dictionary<HANDSHAKE_EQ_TIMEOUT, Stopwatch> EQHSTimersStopwatches = new Dictionary<HANDSHAKE_EQ_TIMEOUT, Stopwatch>() {
            {  HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON , new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON , new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON , new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF ,new Stopwatch()},
            {  HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF , new Stopwatch()}
        };
        public Dictionary<HANDSHAKE_EQ_TIMEOUT, double> EQHSTimers = new Dictionary<HANDSHAKE_EQ_TIMEOUT, double>() {
            {  HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF , 0},
            {  HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF , 0}
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
            public clsHandshakeSignalState(EQ_HSSIGNAL signal_name)
            {
                this.signal_name = signal_name;
            }
            public bool _state = false;
            public bool lastNewInput = false;
            private Stopwatch stopwatch = new Stopwatch();
            private readonly EQ_HSSIGNAL signal_name;
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
                                await Task.Delay(300, cts.Token);
                                bool is_changed = _state != lastNewInput;
                                _state = lastNewInput;
                                if (is_changed)
                                    LOG.INFO($"EQ 交握訊號-{signal_name} Change to {(_state ? 1 : 0)}");
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
        }

        private bool IsHandShakeBypass => Parameters.EQHandshakeBypass;

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
        private int EqModbusTcpPort
        {
            get
            {
                if (WorkStations.Stations.TryGetValue(ExecutingTaskModel.destineTag, out var data))
                {
                    return data.ModbusTcpPort;
                }
                else
                {
                    return -1;
                }
            }
        }

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
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                if (Parameters.EQHandshakeSimulationAutoRun)
                {
                    _ = Task.Factory.StartNew(async () =>
                    {
                        await Task.Delay(20);
                        await WagoDO.SetState(DO_ITEM.EMU_EQ_GO, true);
                    });
                }
            }
            return EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].State;
        }
        private bool IsEQBusyOn()
        {
            return EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].State;

        }

        #region 交握

        private async void SetAGVBUSY(bool value, bool isInEQ = true)
        {
            await WagoDO.SetState(DO_ITEM.AGV_BUSY, value);
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
        private async void SetAGVREADY(bool value)
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
        private async void SetAGVVALID(bool value)
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
        internal async void SetAGV_TR_REQ(bool value)
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
        private async void SetAGV_COMPT(bool value)
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

        internal void ResetHSTimers()
        {
            foreach (var timer_key in EQHSTimers.Keys)
            {
                EQHSTimers[timer_key] = 0;
                EQHSTimersStopwatches[timer_key].Reset();
            }
        }
        /// <summary>
        /// 開始與EQ進行交握;
        /// - AGV VALID ON => 等待L/U_REQ ON => TR_REQ ON => 等待EQ READY ON
        /// </summary>
        internal async Task<(bool eqready, AlarmCodes alarmCode)> WaitEQReadyON(ACTION_TYPE action)
        {
            if (Parameters.LDULD_Task_No_Entry)
                return (true, AlarmCodes.None);

            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.PIO)
                WatchE84EQGOSignalWhenHSStart();

            CancellationTokenSource waitEQSignalCST = new CancellationTokenSource();
            CancellationTokenSource waitEQReadyOnCST = new CancellationTokenSource();
            bool IsEQDown = false;
            Task wait_eq_UL_req_ON = new Task(() =>
            {
                while (!IsULReqOn(action) && IsEQGOOn())
                {
                    if (waitEQSignalCST.IsCancellationRequested | Sub_Status == SUB_STATUS.DOWN)
                        throw new OperationCanceledException();
                    Thread.Sleep(1);
                }
            });
            var wait_eq_ready = new Task(() =>
            {
                while (!IsEQReadyOn() && IsEQGOOn())
                {
                    Thread.Sleep(1);
                    if (waitEQReadyOnCST.IsCancellationRequested | Sub_Status == SUB_STATUS.DOWN)
                        throw new OperationCanceledException();

                    if (!IsULReqOn(action))
                    {
                        IsEQDown = true;
                        throw new OperationCanceledException();
                    }
                }
            });

            SetAGVVALID(true);
            try
            {
                LOG.Critical("[EQ Handshake] 等待EQ LU_REQ ON");
                HandshakeStatusText = "等待EQ 取放請求訊號ON";
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON);
                waitEQSignalCST.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON]));
                wait_eq_UL_req_ON.Start();
                wait_eq_UL_req_ON.Wait(waitEQSignalCST.Token);
                if (!IsEQGOOn())
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                StopAllHandshakeTimer();
                if (Sub_Status == SUB_STATUS.DOWN)
                    return (false, AlarmCodes.Handshake_Fail_AGV_DOWN);
                if (!IsEQGOOn())
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                return (false, action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_TA1_EQ_L_REQ : AlarmCodes.Handshake_Fail_TA1_EQ_U_REQ);
            }
            SetAGV_TR_REQ(true);
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON);
                waitEQReadyOnCST.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON]));
                LOG.Critical("[EQ Handshake] 等待EQ Ready ON...");
                HandshakeStatusText = "等待EQ Ready..";
                wait_eq_ready.Start();
                wait_eq_ready.Wait(waitEQReadyOnCST.Token);
                if (!IsEQGOOn())
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                SetAGVBUSY(true);
                WatchE84AlarmWhenAGVBUSY();
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON);
                HandshakeStatusText = "AGV動作中..";
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                StopAllHandshakeTimer();
                if (Sub_Status == SUB_STATUS.DOWN)
                    return (false, AlarmCodes.Handshake_Fail_AGV_DOWN);
                if (!IsEQGOOn())
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                if (IsEQDown)
                    return (false, AlarmCodes.Handshake_Fail_EQ_LU_REQ_OFF_WHEN_WAIT_READY);
                return (false, AlarmCodes.Handshake_Fail_TA2_EQ_READY);
            }

        }
        internal bool EQAlarmWhenEQBusyFlag = false;
        internal bool AGVAlarmWhenEQBusyFlag = false;


        internal bool EQAlarmWhenAGVBusyFlag = false;
        internal bool AGVAlarmWhenAGVBusyFlag = false;

        internal AlarmCodes AlarmCodeWhenHandshaking = AlarmCodes.None;
        /// <summary>
        /// 等待EQ交握訊號 BUSY OFF＝＞表示ＡＧＶ可以退出了(模擬模式下:用CST在席有無表示是否BUSY結束 LOAD=>貨被拿走. Unload=>貨被放上來)
        /// </summary>
        internal async Task<(bool eq_busy_off, AlarmCodes alarmCode)> WaitEQBusyOFF(ACTION_TYPE action)
        {
            if (Parameters.LDULD_Task_No_Entry)
                return (true, AlarmCodes.None);
            DirectionLighter.WaitPassLights();
            CancellationTokenSource waitEQ_BUSY_ON_CTS = new CancellationTokenSource();
            CancellationTokenSource waitEQ_BUSY_OFF_CTS = new CancellationTokenSource();

            SetAGVBUSY(false, true);
            await Task.Delay(300);
            SetAGVREADY(true);
            AlarmCodes alarm_code = AlarmCodes.None;
            Task wait_eq_busy_ON = new Task(() =>
            {
                LOG.Critical("[EQ Handshake] 等待EQ BUSY ON ");
                while (!IsEQBusyOn() && IsEQGOOn() && IsEQReadyOn())
                {
                    if (Sub_Status == SUB_STATUS.DOWN)
                    {
                        alarm_code = AlarmCodes.Handshake_Fail_AGV_DOWN;
                        throw new OperationCanceledException();
                    }
                    if (waitEQ_BUSY_ON_CTS.IsCancellationRequested)
                    {
                        alarm_code = AlarmCodes.Handshake_Fail_EQ_BUSY_NOT_ON;
                        throw new OperationCanceledException();
                    }
                    Thread.Sleep(1);
                }
            });
            Task wait_eq_busy_OFF = new Task(() =>
            {
                LOG.Critical("[EQ Handshake] 等待EQ BUSY OFF");
                if (waitEQ_BUSY_OFF_CTS.IsCancellationRequested)
                {
                    alarm_code = AlarmCodes.Handshake_Fail_EQ_BUSY_NOT_OFF;
                    throw new OperationCanceledException();
                }
                bool IsEQBusyOFF = !IsEQBusyOn();
                while (!IsEQBusyOFF && IsEQGOOn())
                {
                    IsEQBusyOFF = !IsEQBusyOn();

                    if (IsEQBusyOFF && !waitEQ_BUSY_OFF_CTS.IsCancellationRequested) //偵測到OFF 訊號後，再等1秒再檢查一次
                    {
                        Thread.Sleep(1000);
                        IsEQBusyOFF = !IsEQBusyOn() && IsEQGOOn();
                        if (!IsEQBusyOFF)
                        {
                            LOG.Critical($"EQ Busy Signal Flick!!!!!!!!!!");
                        }
                    }
                    Thread.Sleep(1);
                }
            });
            try
            {
                HandshakeStatusText = "等待EQ開始動作..";
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON);
                waitEQ_BUSY_ON_CTS.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON]));
                wait_eq_busy_ON.Start();
                wait_eq_busy_ON.Wait(waitEQ_BUSY_ON_CTS.Token);
                if (!IsEQGOOn())
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON);
                HandshakeStatusText = "等待EQ完成動作..";

            }
            catch (Exception ex)
            {
                if (!IsEQGOOn())
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                LOG.ERROR($"[HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON] {ex.Message}-EQAlarmWhenEQBusyFlag={EQAlarmWhenEQBusyFlag},AGVAlarmWhenEQBusyFlag={AGVAlarmWhenEQBusyFlag}", ex);
                LOG.Critical(ex);
                StopAllHandshakeTimer();
                var _alarm = AlarmCodes.None;
                if (!IsEQGOOn())
                    _alarm = AlarmCodes.Handshake_Fail_EQ_GO;
                else if (!IsEQReadyOn())
                    _alarm = AlarmCodes.Handshake_Fail_EQ_READY_OFF;
                else if (Sub_Status == SUB_STATUS.DOWN)
                    _alarm = AlarmCodes.Handshake_Fail_AGV_DOWN;
                else
                    _alarm = AlarmCodes.Handshake_Fail_TA3_EQ_BUSY_ON;
                AlarmCodeWhenHandshaking = _alarm;
                return (false, _alarm);
            }
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF);
                waitEQ_BUSY_OFF_CTS.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF]));
                WatchE84EQAlarmWhenEQBUSY(waitEQ_BUSY_OFF_CTS);
                wait_eq_busy_OFF.Start();
                wait_eq_busy_OFF.Wait(waitEQ_BUSY_OFF_CTS.Token);
                if (!IsEQGOOn())
                {
                    AlarmCodeWhenHandshaking = AlarmCodes.Handshake_Fail_EQ_GO;
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                }
                if (!IsEQReadyOn())
                {
                    AlarmCodeWhenHandshaking = AlarmCodes.Handshake_Fail_EQ_READY_OFF;
                    return (false, AlarmCodes.Handshake_Fail_EQ_READY_OFF);
                }

                HandshakeStatusText = "EQ完成動作";
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF);
                SetAGVREADY(false); //AGV BUSY 開始退出
                SetAGVBUSY(true);

                HandshakeStatusText = "AGV動作中..";
                WatchE84AlarmWhenAGVBUSY();
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                StopAllHandshakeTimer();
                bool IsEQOrAGVAlarmWhenEQBUSY = EQAlarmWhenEQBusyFlag | AGVAlarmWhenEQBusyFlag;
                LOG.ERROR($"{ex.Message}-EQAlarmWhenEQBusyFlag={EQAlarmWhenEQBusyFlag},AGVAlarmWhenEQBusyFlag={AGVAlarmWhenEQBusyFlag}", ex);
                LOG.Critical(ex);
                AlarmCodes _alarm = AlarmCodes.None;
                if (!IsEQGOOn())
                    _alarm = AlarmCodes.Handshake_Fail_EQ_GO;
                else if (!IsEQReadyOn())
                    _alarm = AlarmCodes.Handshake_Fail_EQ_READY_OFF;
                else if (!IsEQOrAGVAlarmWhenEQBUSY)
                    _alarm = AlarmCodes.Handshake_Fail_TA4_EQ_BUSY_OFF;
                else if (Sub_Status == SUB_STATUS.DOWN)
                    _alarm = AlarmCodes.Handshake_Fail_AGV_DOWN;
                else
                    _alarm = AlarmCodes.None;
                AlarmCodeWhenHandshaking = _alarm;
                return (_alarm == AlarmCodes.None, _alarm);
            }

        }

        /// <summary>
        /// 結束EQ交握_等待EQ READY OFF
        /// </summary>
        internal async Task<(bool eqready_off, AlarmCodes alarmCode)> WaitEQReadyOFF(ACTION_TYPE action)
        {

            if (Parameters.LDULD_Task_No_Entry)
                return (true, AlarmCodes.None);
            CancellationTokenSource wait_eq_l_u_req_off_cts = new CancellationTokenSource();
            Task wait_eq_UL_req_OFF = new Task(() =>
            {
                while (IsULReqOn(action) && IsEQGOOn())
                {
                    if (wait_eq_l_u_req_off_cts.IsCancellationRequested)
                        return;
                    Thread.Sleep(1);
                }
            });
            Task wait_eq_ready_off = new Task(() =>
            {
                LOG.WARN("[EQ Handshake] 等待 EQ READY OFF");
                while (IsEQReadyOn() && IsEQGOOn())
                {
                    if (wait_eq_l_u_req_off_cts.IsCancellationRequested)
                        return;
                    Thread.Sleep(1);
                }
            });

            SetAGVBUSY(false, false);
            await Task.Delay(200);
            SetAGV_COMPT(true);
            HandshakeStatusText = "等待EQ取放請求訊號OFF..";
            LOG.WARN("[EQ Handshake] 等待取放請求訊號OFF");
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
                wait_eq_l_u_req_off_cts.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF]));
                wait_eq_UL_req_OFF.Start();
                wait_eq_UL_req_OFF.Wait(wait_eq_l_u_req_off_cts.Token);
                if (!IsEQGOOn())
                {
                    AlarmCodeWhenHandshaking = AlarmCodes.Handshake_Fail_EQ_GO;
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                }

                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                StopAllHandshakeTimer();
                AlarmCodes _alarm = AlarmCodes.None;
                if (!IsEQGOOn())
                    _alarm = AlarmCodes.Handshake_Fail_EQ_GO;
                else if (Sub_Status == SUB_STATUS.DOWN)
                    _alarm = AlarmCodes.Handshake_Fail_AGV_DOWN;
                else
                {
                    _alarm = action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_TA5_EQ_L_REQ : AlarmCodes.Handshake_Fail_TA5_EQ_U_REQ;
                }
                AlarmCodeWhenHandshaking = _alarm;
                return (false, _alarm);
            }
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
                wait_eq_l_u_req_off_cts = new CancellationTokenSource();
                wait_eq_l_u_req_off_cts.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF]));
                wait_eq_ready_off.Start();
                wait_eq_ready_off.Wait(wait_eq_l_u_req_off_cts.Token);
                await Task.Delay(300);
                if (!IsEQGOOn())
                {
                    AlarmCodeWhenHandshaking = AlarmCodes.Handshake_Fail_EQ_GO;
                    return (false, AlarmCodes.Handshake_Fail_EQ_GO);
                }
                SetAGV_COMPT(false);
                SetAGV_TR_REQ(false);
                SetAGVVALID(false);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
                LOG.INFO("[EQ Handshake] EQ READY OFF=>Handshake Done");
                _ = Task.Run(async () =>
                {
                    HandshakeStatusText = "交握結束";
                    await Task.Delay(1000);
                });
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                StopAllHandshakeTimer();

                AlarmCodes _alarm = AlarmCodes.None;
                if (!IsEQGOOn())
                    _alarm = AlarmCodes.Handshake_Fail_EQ_GO;
                else if (Sub_Status == SUB_STATUS.DOWN)
                    _alarm = AlarmCodes.Handshake_Fail_AGV_DOWN;
                else
                    _alarm = AlarmCodes.Handshake_Fail_TA5_EQ_READY_NOT_OFF;
                AlarmCodeWhenHandshaking = _alarm;
                return (false, _alarm);
            }

        }

        #endregion
        private async Task WatchE84EQGOSignalWhenHSStart()
        {
            _ = Task.Run(async () =>
            {
                LOG.WARN("Start Watch EQ_GO State..");
                while (true)
                {
                    try
                    {
                        Thread.Sleep(10);
                        if (ExecutingTaskModel == null)
                            break;
                        if (ExecutingTaskModel != null && ExecutingTaskModel.action != ACTION_TYPE.Load && ExecutingTaskModel.action != ACTION_TYPE.Unload)
                            break;

                        if (!IsEQGOOn())
                        {
                            LOG.ERROR("EQ_GO State OFF!");
                            SetAGV_TR_REQ(false);
                            if (Sub_Status == SUB_STATUS.DOWN)
                                break;
                            if (Sub_Status == SUB_STATUS.RUN)
                            {
                                AGVC.AbortTask();
                                Sub_Status = SUB_STATUS.DOWN;
                                AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_EQ_GO, false);
                                StopAllHandshakeTimer();
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LOG.ERROR($"WatchE84EQGOSignalWhenHS Process end[{ex.Message}]");
                        break;
                    }
                }
                LOG.TRACE($"WatchE84EQGOSignalWhenHS Process end");
            });
        }

        private async Task WatchE84AlarmWhenAGVBUSY()
        {
            await Task.Delay(200);
            AGVAlarmWhenAGVBusyFlag = false;
            EQAlarmWhenAGVBusyFlag = false;
            await Task.Delay(1);
            _ = Task.Run(async () =>
            {
                while (AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY] && AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ])
                {
                    Thread.Sleep(10);
                    bool isEQReadyOff = !IsEQReadyOn();
                    bool isEQBusyOn = IsEQBusyOn();
                    ////AGV作動中發生AGV異常
                    if (Sub_Status == SUB_STATUS.DOWN && AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY])
                    {
                        if (isEQReadyOff | isEQBusyOn | !IsEQGOOn())
                            return;
                        RaiseAGVAlarmWhenHS();
                        return;
                    }

                    if ((isEQReadyOff | isEQBusyOn) && AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY] && !AGVHsSignalStates[AGV_HSSIGNAL.AGV_READY])//AGV作動中發生EQ異常
                    {
                        EQAlarmWhenAGVBusyFlag = true;
                        AGVC.AbortTask();
                        Sub_Status = SUB_STATUS.DOWN;
                        await Task.Delay(100);
                        var alarm_code = !IsEQGOOn() ? AlarmCodes.Handshake_Fail_EQ_GO : (isEQReadyOff ? AlarmCodes.Handshake_Fail_EQ_READY_OFF : AlarmCodes.Handshake_Fail_EQ_Busy_ON_When_AGV_BUSY);
                        AlarmManager.AddAlarm(alarm_code, false);
                        LOG.TRACE($"WatchE84AlarmWhenAGVBUSY Process end.[isEQReadyOff/isEQBusyOn]");
                        return;
                    }
                } ////AGV作動中發生AGV異常
                if (Sub_Status == SUB_STATUS.DOWN && IsEQGOOn())
                {
                    RaiseAGVAlarmWhenHS();
                }
                LOG.TRACE($"WatchE84AlarmWhenAGVBUSY Process end[AGV_BUSY OFF/AGV_TR_REQ OFF]");
            });
        }

        private void RaiseAGVAlarmWhenHS()
        {
            AGVAlarmWhenAGVBusyFlag = true;
            AGVC.AbortTask();
            SetAGV_TR_REQ(false);
            LOG.Critical($"AGV作動中發生AGV異常，須將AGV移動至安全位置後進行賦歸方可將Busy 訊號 OFF.");

            if (AlarmCodeWhenHandshaking != AlarmCodes.Has_Job_Without_Cst & AlarmCodeWhenHandshaking != AlarmCodes.Has_Cst_Without_Job)
                AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_AGV_DOWN, false);
            LOG.TRACE($"WatchE84AlarmWhenAGVBUSY Process end.[Handshake_Fail_AGV_DOWN]");
        }

        private async Task WatchE84EQAlarmWhenEQBUSY(CancellationTokenSource waitEQSignalCST)
        {
            await Task.Delay(200);
            AGVAlarmWhenEQBusyFlag = false;
            EQAlarmWhenEQBusyFlag = false;
            await Task.Delay(1);
            _ = Task.Run(async () =>
            {
                while (IsEQBusyOn())
                {
                    Thread.Sleep(10);
                    var isEQReadyOFF = !IsEQReadyOn();
                    if (Sub_Status == SUB_STATUS.DOWN)
                    {
                        AGVAlarmWhenEQBusyFlag = true;
                        waitEQSignalCST.Cancel();
                        SetAGV_TR_REQ(false);
                        if (!isEQReadyOFF & IsEQGOOn() & AlarmCodeWhenHandshaking == AlarmCodes.None)
                        {
                            AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_AGV_DOWN, false);
                        }
                        LOG.TRACE($"WatchE84EQAlarmWhenEQBUSY Process end[AGVSub_Status DOWN]");
                        return;
                    }

                    if (isEQReadyOFF && Sub_Status == SUB_STATUS.RUN)//異常發生
                    {
                        EQAlarmWhenEQBusyFlag = true;
                        waitEQSignalCST.Cancel();
                        AGVC.AbortTask();
                        Sub_Status = SUB_STATUS.DOWN;
                        AlarmManager.AddAlarm(!IsEQGOOn() ? AlarmCodes.Handshake_Fail_EQ_GO : AlarmCodes.Handshake_Fail_EQ_READY_OFF, false);
                        LOG.TRACE($"WatchE84EQAlarmWhenEQBUSY Process end[EQ_READY OFF]");
                        return;
                    }
                }
                LOG.TRACE($"WatchE84EQAlarmWhenEQBUSY Process end[EQ_BUSY OFF]");
            });
        }
        internal void StopAllHandshakeTimer()
        {
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF);
            EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
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

    }
}
