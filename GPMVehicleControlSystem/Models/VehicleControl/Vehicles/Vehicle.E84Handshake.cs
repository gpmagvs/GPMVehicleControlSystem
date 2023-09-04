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
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        public enum EQ_HSSIGNAL
        {
            EQ_READY,
            EQ_BUSY,
            EQ_L_REQ,
            EQ_U_REQ,
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
            { EQ_HSSIGNAL.EQ_READY, false },
            { EQ_HSSIGNAL.EQ_BUSY, false },
            { EQ_HSSIGNAL.EQ_L_REQ, false },
            { EQ_HSSIGNAL.EQ_U_REQ, false },
        };
        public Dictionary<AGV_HSSIGNAL, bool> AGVHsSignalStates = new Dictionary<AGV_HSSIGNAL, bool>()
        {
            { AGV_HSSIGNAL.AGV_VALID, false },
            { AGV_HSSIGNAL.AGV_READY, false },
            { AGV_HSSIGNAL.AGV_TR_REQ, false },
            { AGV_HSSIGNAL.AGV_BUSY, false },
            { AGV_HSSIGNAL.AGV_COMPT, false },
        };
        private int EqModbusTcpPort
        {
            get
            {
                if (WorkStations.Stations.TryGetValue(ExecutingTask.destineTag, out var data))
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
        private async void SetAGV_TR_REQ(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_TR_REQ, value);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ] = value;
        }
        private async void SetAGV_COMPT(bool value)
        {
            await WagoDO.SetState(DO_ITEM.AGV_COMPT, value);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT] = value;
        }

        /// <summary>
        /// 開始與EQ進行交握;
        /// - AGV VALID ON => 等待L/U_REQ ON => TR_REQ ON => 等待EQ READY ON
        /// </summary>
        internal async Task<(bool eqready, AlarmCodes alarmCode)> WaitEQReadyON(ACTION_TYPE action)
        {
            LOG.Critical("[EQ Handshake] 等待EQ Ready ON...");

            CancellationTokenSource waitEQSignalCST = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task wait_eq_UL_req_ON = new Task(() =>
            {
                while (!IsULReqOn(action))
                {
                    if (waitEQSignalCST.IsCancellationRequested)
                        throw new OperationCanceledException();
                    Thread.Sleep(1);
                }
            });
            Task wait_eq_ready = new Task(() =>
            {
                while (!IsEQReadyOn())
                {
                    if (waitEQSignalCST.IsCancellationRequested)
                        throw new OperationCanceledException();
                    Thread.Sleep(1);
                }
            });

            SetAGVVALID(true);
            waitEQSignalCST = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                wait_eq_UL_req_ON.Start();
                wait_eq_UL_req_ON.Wait(waitEQSignalCST.Token);
            }
            catch (OperationCanceledException ex)
            {
                return (false, action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_EQ_L_REQ : AlarmCodes.Handshake_Fail_EQ_U_REQ);
            }
            waitEQSignalCST = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            SetAGV_TR_REQ(true);
            try
            {
                wait_eq_ready.Start();
                wait_eq_ready.Wait(waitEQSignalCST.Token);
                SetAGVBUSY(true);
                WatchE84AlarmWhenAGVBUSY();
                return (true, AlarmCodes.None);
            }
            catch (OperationCanceledException ex)
            {
                return (false, AlarmCodes.Handshake_Fail_EQ_READY);
            }

        }
        internal bool EQAlarmWhenEQBusyFlag = false;
        internal bool AGVAlarmWhenEQBusyFlag = false;

        public clsDynamicTrafficState DynamicTrafficState { get; internal set; } = new clsDynamicTrafficState();

        /// <summary>
        /// 結束EQ交握_等待EQ READY OFF
        /// </summary>
        internal async Task<(bool eqready_off, AlarmCodes alarmCode)> WaitEQReadyOFF(ACTION_TYPE action)
        {

            LOG.Critical("[EQ Handshake] 等待EQ READY OFF");
            CancellationTokenSource waitEQSignalCST = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task wait_eq_UL_req_OFF = new Task(() =>
            {
                while (IsULReqOn(action))
                {
                    if (waitEQSignalCST.IsCancellationRequested)
                        return;
                    Thread.Sleep(1);
                }
            });
            Task wait_eq_ready_off = new Task(() =>
            {
                while (IsEQReadyOn())
                {
                    if (waitEQSignalCST.IsCancellationRequested)
                        return;
                    Thread.Sleep(1);
                }
            });

            SetAGVBUSY(false);
            SetAGV_COMPT(true);


            waitEQSignalCST = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                wait_eq_UL_req_OFF.Start();
                wait_eq_UL_req_OFF.Wait(waitEQSignalCST.Token);
            }
            catch (OperationCanceledException ex)
            {
                return (false, action == ACTION_TYPE.Load ? AlarmCodes.Handshake_Fail_EQ_L_REQ : AlarmCodes.Handshake_Fail_EQ_U_REQ);
            }
            try
            {
                wait_eq_ready_off.Start();
                wait_eq_ready_off.Wait(waitEQSignalCST.Token);
                SetAGV_COMPT(false);
                SetAGV_TR_REQ(false);
                SetAGVVALID(false);

                LOG.Critical("[EQ Handshake] EQ READY OFF=>Handshake Done");
                return (true, AlarmCodes.None);
            }
            catch (OperationCanceledException ex)
            {
                return (false, AlarmCodes.Handshake_Fail_EQ_READY);

            }

        }


        /// <summary>
        /// 等待EQ交握訊號 BUSY OFF＝＞表示ＡＧＶ可以退出了(模擬模式下:用CST在席有無表示是否BUSY結束 LOAD=>貨被拿走. Unload=>貨被放上來)
        /// </summary>
        internal async Task<(bool eq_busy_off, AlarmCodes alarmCode)> WaitEQBusyOFF(ACTION_TYPE action)
        {

            LOG.Critical("[EQ Handshake] 等待EQ BUSY OFF");
            DirectionLighter.WaitPassLights();
            CancellationTokenSource waitEQSignalCST = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            SetAGVBUSY(false);
            SetAGVREADY(true);

            Task wait_eq_busy_ON = new Task(() =>
            {
                while (!IsEQBusyOn())
                {
                    if (waitEQSignalCST.IsCancellationRequested)
                        throw new OperationCanceledException();
                    Thread.Sleep(1);
                }
            });
            Task wait_eq_busy_OFF = new Task(() =>
            {
                while (IsEQBusyOn())
                {
                    if (waitEQSignalCST.IsCancellationRequested)
                        throw new OperationCanceledException();
                    Thread.Sleep(1);
                }
            });

            try
            {
                wait_eq_busy_ON.Start();
                wait_eq_busy_ON.Wait(waitEQSignalCST.Token);
            }
            catch (OperationCanceledException)
            {
                return (false, AlarmCodes.Handshake_Fail_EQ_BUSY_ON);
            }
            waitEQSignalCST = new CancellationTokenSource(TimeSpan.FromSeconds(Debugger.IsAttached ? 15 : 90));
            WatchE84EQAlarmWhenEQBUSY(waitEQSignalCST);
            try
            {
                wait_eq_busy_OFF.Start();
                wait_eq_busy_OFF.Wait(waitEQSignalCST.Token);
                SetAGVREADY(false); //AGV BUSY 開始退出
                SetAGVBUSY(true);
                WatchE84AlarmWhenAGVBUSY();
                return (true, AlarmCodes.None);
            }
            catch (OperationCanceledException ex)
            {
                if (!EQAlarmWhenEQBusyFlag)
                    return (false, AlarmCodes.Handshake_Fail_EQ_BUSY_OFF);
                else
                    return (false, AlarmCodes.Handshake_Fail);
            }
        }

        #endregion

        private async Task WatchE84AlarmWhenAGVBUSY()
        {
            AGVAlarmWhenEQBusyFlag = false;
            EQAlarmWhenEQBusyFlag = false;
            await Task.Delay(1);
            _ = Task.Factory.StartNew(async () =>
            {
                while (AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY])
                {
                    await Task.Delay(1);
                    if (Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN | Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.ALARM)
                    {
                        SetAGV_TR_REQ(false);
                        AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_AGV_DOWN, false);
                        throw new Exception("AGV Abnormal When AGV BUSY");
                    }
                    bool isEQReadyOff = !IsEQReadyOn();
                    bool isEQBusyOn = IsEQBusyOn();
                    if (isEQReadyOff | isEQBusyOn)//異常發生
                    {
                        EQAlarmWhenEQBusyFlag = true;
                        AGVC.AbortTask(RESET_MODE.ABORT);
                        Sub_Status = AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN;
                        AlarmManager.AddAlarm(isEQReadyOff ? AlarmCodes.Handshake_Fail_EQ_READY_OFF_When_AGV_BUSY : AlarmCodes.Handshake_Fail_EQ_Busy_ON_When_AGV_BUSY, false);
                        await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                        throw new Exception("EQ Abnormal When AGV BUSY");
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
                    if (Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN | Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.ALARM)
                    {
                        AGVAlarmWhenEQBusyFlag = true;
                        SetAGV_TR_REQ(false);
                        AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_AGV_DOWN, false);

                        while (IsEQBusyOn())
                        {
                            await Task.Delay(1);
                        }
                        SetAGVREADY(false);
                        SetAGVVALID(false);
                        break;
                    }
                    if (!IsEQReadyOn() && Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN)//異常發生
                    {
                        EQAlarmWhenEQBusyFlag = true;
                        waitEQSignalCST.Cancel();
                        AGVC.AbortTask(RESET_MODE.ABORT);
                        Sub_Status = AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN;
                        AlarmManager.AddAlarm(AlarmCodes.Handshake_Fail_Inside_EQ_EQ_GO, false);
                        await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                        while (IsEQBusyOn())
                        {
                            await Task.Delay(1);
                        }
                        SetAGVREADY(false);
                        SetAGV_TR_REQ(false);
                        SetAGVVALID(false);
                    }

                }
            });
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
    }
}
