using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Modbus.Device;
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
        private bool IsHandShakeBypass => Parameters.EQHandshakeBypass;

        public Dictionary<EQ_HSSIGNAL, bool> EQHsSignalStates = new Dictionary<EQ_HSSIGNAL, bool>()
        {
            { EQ_HSSIGNAL.EQ_L_REQ, false },
            { EQ_HSSIGNAL.EQ_U_REQ, false },
            { EQ_HSSIGNAL.EQ_READY, false },
            { EQ_HSSIGNAL.EQ_BUSY, false },
            { EQ_HSSIGNAL.EQ_GO, false },
        };
        public Dictionary<AGV_HSSIGNAL, bool> AGVHsSignalStates = new Dictionary<AGV_HSSIGNAL, bool>()
        {
            { AGV_HSSIGNAL.AGV_VALID, false },
            { AGV_HSSIGNAL.AGV_TR_REQ, false },
            { AGV_HSSIGNAL.AGV_BUSY, false },
            { AGV_HSSIGNAL.AGV_COMPT, false },
            { AGV_HSSIGNAL.AGV_READY, false },
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

            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                if (action == ACTION_TYPE.Load)
                    return WagoDO.GetState(DO_ITEM.EMU_EQ_L_REQ);
                else
                    return WagoDO.GetState(DO_ITEM.EMU_EQ_U_REQ);
            }
            else
            {
                if (action == ACTION_TYPE.Load)
                    return EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ];
                else
                    return EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ];
            }

        }

        private bool IsEQReadyOn()
        {
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                return WagoDO.GetState(DO_ITEM.EMU_EQ_READY);
            }
            else
            {
                return EQHsSignalStates[EQ_HSSIGNAL.EQ_READY];
            }
        }
        internal bool IsEQGOOn()
        {
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.MODBUS)
                return true;
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
                return WagoDO.GetState(DO_ITEM.EMU_EQ_GO);
            return EQHsSignalStates[EQ_HSSIGNAL.EQ_GO];
        }
        private bool IsEQBusyOn()
        {
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                return WagoDO.GetState(DO_ITEM.EMU_EQ_BUSY);
            }
            else
            {
                return EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY];
            }
        }

        #region 交握

        private async void SetAGVBUSY(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_BUSY, value);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY] = value;
        }
        private async void SetAGVREADY(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_READY, value);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_READY] = value;
        }
        private async void SetAGVVALID(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_VALID, value);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID] = value;
        }
        internal async void SetAGV_TR_REQ(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_TR_REQ, value);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ] = value;
        }
        private async void SetAGV_COMPT(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_COMPT, value);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT] = value;
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

            WatchE84EQGOSignalWhenHSStart();

            CancellationTokenSource waitEQSignalCST = new CancellationTokenSource();
            CancellationTokenSource waitEQReadyOnCST = new CancellationTokenSource();
            Task wait_eq_UL_req_ON = new Task(() =>
            {
                while (!IsULReqOn(action))
                {
                    if (waitEQSignalCST.IsCancellationRequested | Sub_Status == SUB_STATUS.DOWN)
                        throw new OperationCanceledException();
                    Thread.Sleep(1);
                }
            });
            Task wait_eq_ready = new Task(() =>
            {
                while (!IsEQReadyOn())
                {
                    if (waitEQReadyOnCST.IsCancellationRequested | Sub_Status == SUB_STATUS.DOWN)
                        throw new OperationCanceledException();
                    Thread.Sleep(1);
                }
            });

            SetAGVVALID(true);
            try
            {
                LOG.Critical("[EQ Handshake] 等待EQ LU_REQ ON");
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON);
                waitEQSignalCST.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON]));
                wait_eq_UL_req_ON.Start();
                wait_eq_UL_req_ON.Wait(waitEQSignalCST.Token);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA1_Wait_L_U_REQ_ON);
                return (false, action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_TA1_EQ_L_REQ : AlarmCodes.Handshake_Fail_TA1_EQ_U_REQ);
            }
            SetAGV_TR_REQ(true);
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON);
                waitEQReadyOnCST.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON]));
                LOG.Critical("[EQ Handshake] 等待EQ Ready ON...");
                wait_eq_ready.Start();
                wait_eq_ready.Wait(waitEQReadyOnCST.Token);
                SetAGVBUSY(true);
                WatchE84AlarmWhenAGVBUSY();
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON);
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA2_Wait_EQ_READY_ON);
                return (false, AlarmCodes.Handshake_Fail_TA2_EQ_READY);
            }

        }
        internal bool EQAlarmWhenEQBusyFlag = false;
        internal bool AGVAlarmWhenEQBusyFlag = false;


        internal bool EQAlarmWhenAGVBusyFlag = false;
        internal bool AGVAlarmWhenAGVBusyFlag = false;

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

            SetAGVBUSY(false);
            SetAGVREADY(true);
            AlarmCodes alarm_code = AlarmCodes.None;
            Task wait_eq_busy_ON = new Task(() =>
            {
                LOG.Critical("[EQ Handshake] 等待EQ BUSY ON ");
                while (!IsEQBusyOn())
                {
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
                while (!IsEQBusyOFF)
                {
                    IsEQBusyOFF = !IsEQBusyOn();

                    if (IsEQBusyOFF && !waitEQ_BUSY_OFF_CTS.IsCancellationRequested) //偵測到OFF 訊號後，再等1秒再檢查一次
                    {
                        Thread.Sleep(1000);
                        IsEQBusyOFF = !IsEQBusyOn();
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
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON);
                waitEQ_BUSY_ON_CTS.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON]));
                wait_eq_busy_ON.Start();
                wait_eq_busy_ON.Wait(waitEQ_BUSY_ON_CTS.Token);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON);
            }
            catch (Exception ex)
            {
                LOG.ERROR($"[HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON] {ex.Message}-EQAlarmWhenEQBusyFlag={EQAlarmWhenEQBusyFlag},AGVAlarmWhenEQBusyFlag={AGVAlarmWhenEQBusyFlag}", ex);
                LOG.Critical(ex);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA3_Wait_EQ_BUSY_ON);
                return (false, AlarmCodes.Handshake_Fail_TA3_EQ_BUSY_ON);
            }
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF);
                waitEQ_BUSY_OFF_CTS.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF]));
                WatchE84EQAlarmWhenEQBUSY(waitEQ_BUSY_OFF_CTS);
                wait_eq_busy_OFF.Start();
                wait_eq_busy_OFF.Wait(waitEQ_BUSY_OFF_CTS.Token);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF);
                SetAGVREADY(false); //AGV BUSY 開始退出
                SetAGVBUSY(true);
                WatchE84AlarmWhenAGVBUSY();
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA4_Wait_EQ_BUSY_OFF);
                bool IsEQOrAGVAlarmWhenEQBUSY = EQAlarmWhenEQBusyFlag | AGVAlarmWhenEQBusyFlag;
                LOG.ERROR($"{ex.Message}-EQAlarmWhenEQBusyFlag={EQAlarmWhenEQBusyFlag},AGVAlarmWhenEQBusyFlag={AGVAlarmWhenEQBusyFlag}", ex);
                LOG.Critical( ex);
                if (!IsEQOrAGVAlarmWhenEQBUSY)
                    return (false, AlarmCodes.Handshake_Fail_TA4_EQ_BUSY_OFF);
                else
                    return (true, AlarmCodes.None);
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
                LOG.WARN("[EQ Handshake] 等待 EQ_L_U_REQ OFF");
                while (IsULReqOn(action))
                {
                    if (wait_eq_l_u_req_off_cts.IsCancellationRequested)
                        return;
                    Thread.Sleep(1);
                }
            });
            Task wait_eq_ready_off = new Task(() =>
            {
                LOG.WARN("[EQ Handshake] 等待 EQ READY OFF");
                while (IsEQReadyOn())
                {
                    if (wait_eq_l_u_req_off_cts.IsCancellationRequested)
                        return;
                    Thread.Sleep(1);
                }
            });

            SetAGVBUSY(false);
            SetAGV_COMPT(true);
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);

                wait_eq_l_u_req_off_cts.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF]));
                wait_eq_UL_req_OFF.Start();
                wait_eq_UL_req_OFF.Wait(wait_eq_l_u_req_off_cts.Token);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
                return (false, action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_TA5_EQ_L_REQ : AlarmCodes.Handshake_Fail_TA5_EQ_U_REQ);
            }
            try
            {
                StartTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
                wait_eq_l_u_req_off_cts = new CancellationTokenSource();
                wait_eq_l_u_req_off_cts.CancelAfter(TimeSpan.FromSeconds(Parameters.EQHSTimeouts[HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF]));
                wait_eq_ready_off.Start();
                wait_eq_ready_off.Wait(wait_eq_l_u_req_off_cts.Token);
                SetAGV_COMPT(false);
                SetAGV_TR_REQ(false);
                SetAGVVALID(false);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
                LOG.INFO("[EQ Handshake] EQ READY OFF=>Handshake Done");
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                EndTimer(HANDSHAKE_EQ_TIMEOUT.TA5_Wait_L_U_REQ_OFF);
                return (false, action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_TA5_EQ_L_REQ : AlarmCodes.Handshake_Fail_TA5_EQ_U_REQ);
            }

        }

        #endregion
        private async Task WatchE84EQGOSignalWhenHSStart()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(1);
                        if (ExecutingTaskModel == null)
                            break;
                        if (ExecutingTaskModel != null && ExecutingTaskModel.action != ACTION_TYPE.Load && ExecutingTaskModel.action != ACTION_TYPE.Unload)
                            break;

                        bool isEQGoOff = !IsEQGOOn();

                        if (isEQGoOff)
                        {
                            await Task.Delay(500);
                            isEQGoOff = !IsEQGOOn();
                            if (!isEQGoOff)
                                LOG.WARN($"PID_EQ_READY Signal Flick!!!!!!!!!![WhenAGVBUSY]");
                        }
                        if (isEQGoOff)
                        {
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
                    catch (Exception)
                    {
                        break;
                    }
                }
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

        private async Task WatchE84AlarmWhenAGVBUSY()
        {
            AGVAlarmWhenAGVBusyFlag = false;
            EQAlarmWhenAGVBusyFlag = false;
            await Task.Delay(1);
            _ = Task.Factory.StartNew(async () =>
            {
                while (AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY])
                {
                    await Task.Delay(1);

                    bool isEQReadyOff = !IsEQReadyOn();
                    bool isEQBusyOn = IsEQBusyOn();

                    if (isEQReadyOff)
                    {
                        await Task.Delay(500);
                        isEQReadyOff = !IsEQReadyOn();
                        if (!isEQReadyOff)
                            LOG.WARN($"PID_EQ_READY Signal Flick!!!!!!!!!![WhenAGVBUSY]");
                    }
                    ////AGV作動中發生AGV異常
                    if (Sub_Status == SUB_STATUS.DOWN)
                    {
                        AGVAlarmWhenAGVBusyFlag = true;
                        AGVC.AbortTask();
                        SetAGV_TR_REQ(false);
                        if (isEQReadyOff | isEQBusyOn | !IsEQGOOn())
                            return;
                        LOG.Critical($"AGV作動中發生AGV異常，須將AGV移動至安全位置後進行賦歸方可將Busy 訊號 OFF.");
                        AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_AGV_DOWN, false);
                        return;
                    }


                    if (isEQReadyOff | isEQBusyOn)//AGV作動中發生EQ異常
                    {
                        EQAlarmWhenAGVBusyFlag = true;
                        AGVC.AbortTask();
                        Sub_Status = SUB_STATUS.DOWN;
                        var alarm_code = isEQReadyOff ? AlarmCodes.Handshake_Fail_EQ_READY_OFF_When_AGV_BUSY : AlarmCodes.Handshake_Fail_EQ_Busy_ON_When_AGV_BUSY;
                        AlarmManager.AddAlarm(alarm_code, false);
                        return;
                    }
                }

            });
        }
        private async Task WatchE84EQAlarmWhenEQBUSY(CancellationTokenSource waitEQSignalCST)
        {
            AGVAlarmWhenEQBusyFlag = false;
            EQAlarmWhenEQBusyFlag = false;
            await Task.Delay(1);
            _ = Task.Factory.StartNew(async () =>
            {
                while (IsEQBusyOn())
                {
                    await Task.Delay(1);
                    var isEQReadyOFF = !IsEQReadyOn();
                    if (isEQReadyOFF)
                    {
                        await Task.Delay(500);
                        isEQReadyOFF = !IsEQReadyOn();
                        if (!isEQReadyOFF)
                            LOG.WARN($"PID_EQ_READY Signal Flick!!!!!!!!!! [When EQ BUSY]");
                    }

                    if (Sub_Status == SUB_STATUS.DOWN)
                    {
                        AGVAlarmWhenEQBusyFlag = true;
                        waitEQSignalCST.Cancel();
                        SetAGV_TR_REQ(false);
                        if (!isEQReadyOFF && IsEQGOOn())
                        {
                            AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_AGV_DOWN, false);
                        }
                        break;
                    }

                    if (isEQReadyOFF && Sub_Status == SUB_STATUS.RUN)//異常發生
                    {
                        EQAlarmWhenEQBusyFlag = true;
                        waitEQSignalCST.Cancel();
                        AGVC.AbortTask();
                        Sub_Status = SUB_STATUS.DOWN;
                        AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_Inside_EQ_EQ_GO, false);
                        break;

                    }

                }
            });
        }

        private void StartTimer(HANDSHAKE_EQ_TIMEOUT timer)
        {
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

        public async Task<bool> ModbusTcpConnect(int port = -1)
        {
            try
            {
                var client = new TcpClient(AGVS.IP, port == -1 ? EqModbusTcpPort : port);
                ModbusIpMaster master = ModbusIpMaster.CreateIp(client);
                master.Transport.ReadTimeout = 5000;
                master.Transport.WriteTimeout = 5000;
                master.Transport.Retries = 10;

                _ = Task.Run(() =>
                {
                    LOG.WARN($"LDULD Handshake Use MODBUS TCP! Running...({AGVS.IP}:{(port == -1 ? EqModbusTcpPort : port)})");
                    while (_Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN)
                    {
                        Thread.Sleep(10);
                        bool[] inputs = master.ReadInputs(8, 8);

                        EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ] = inputs[0];
                        EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ] = inputs[1];
                        EQHsSignalStates[EQ_HSSIGNAL.EQ_READY] = inputs[2];
                        EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY] = inputs[3];

                        bool[] outputs = new bool[8];
                        outputs[0] = AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID];
                        outputs[3] = AGVHsSignalStates[AGV_HSSIGNAL.AGV_READY];
                        outputs[4] = AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ];
                        outputs[5] = AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY];
                        outputs[6] = AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT];
                        master.WriteMultipleCoils(8, outputs);
                    }
                    LOG.Critical($"MODBUS TCP Finish Fetch EQ Signal");
                    try
                    {
                        client.Dispose();
                        master.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LOG.ERROR(ex);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return false;
            }
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
