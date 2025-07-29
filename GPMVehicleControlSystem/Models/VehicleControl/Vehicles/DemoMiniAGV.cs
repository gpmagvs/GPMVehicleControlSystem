
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Service;
using Microsoft.AspNetCore.SignalR;
using System.Net.Sockets;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public class DemoMiniAGV : TsmcMiniAGV
    {
        public InspectorAGVCarController? DemoMiniAGVControl => base.MiniAgvAGVC;

        private SemaphoreSlim BatteryUnLockSemaphoreSlim = new SemaphoreSlim(1, 1);
        public override bool IsBattery1Exist
        {
            get
            {
                bool _IsHeadSideSensorOn = WagoDI.GetState(DI_ITEM.Battery_1_Exist_1);
                bool _IsTailSideSensorON = WagoDI.GetState(DI_ITEM.Battery_1_Exist_2);
                return _IsTailSideSensorON || _IsHeadSideSensorOn;
            }
        }

        public override bool IsBattery2Exist
        {
            get
            {
                bool _IsHeadSideSensorOn = WagoDI.GetState(DI_ITEM.Battery_2_Exist_1);
                bool _IsTailSideSensorON = WagoDI.GetState(DI_ITEM.Battery_2_Exist_2);
                return _IsTailSideSensorON || _IsHeadSideSensorOn;
            }
        }

        public DemoMiniAGV(clsVehicelParam param, VehicleServiceAggregator vehicleServiceAggregator) : base(param, vehicleServiceAggregator)
        {
        }
        protected override async Task DOSignalDefaultSetting()
        {
            //WagoDO.AllOFF();
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Front_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Back_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_1, new bool[] { true, true, false, true });
            await WagoDO.SetState(DO_ITEM.Instrument_Servo_On, true);
            await Laser.ModeSwitch(1);
        }
        protected override void SyncHandshakeSignalStates()
        {
            //Do NOthing
        }


        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();
            WagoDI.SubsSignalStateChange(DI_ITEM.Front_Right_Ultrasound_Sensor, (obj, state) =>
            {
                if (state)
                    AlarmManager.AddWarning(AlarmCodes.Ultrasonic_Sensor_Error_Right);
                else
                    AlarmManager.ClearAlarm(AlarmCodes.Ultrasonic_Sensor_Error_Right);
            });
            WagoDI.SubsSignalStateChange(DI_ITEM.Back_Left_Ultrasound_Sensor, (obj, state) =>
            {
                if (state)
                    AlarmManager.AddWarning(AlarmCodes.Ultrasonic_Sensor_Error_Left);
                else
                    AlarmManager.ClearAlarm(AlarmCodes.Ultrasonic_Sensor_Error_Left);
            });

        }

        internal override async Task ResetAlarmsAsync(bool IsTriggerByButton)
        {
            await base.ResetAlarmsAsync(IsTriggerByButton);
            if (GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN)
            {
                await AGVC.EmergencyStop(bypass_stopped_check: true);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Brake, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Brake, false);

            }
            BuzzerPlayer.SoundPlaying = SOUNDS.Stop;

        }

        public override async Task<bool> ResetMotor(bool triggerByResetButtonPush)
        {
            try
            {

                //await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
                //await WagoDO.SetState(DO_ITEM.Left_LsrBypass, true);
                //await WagoDO.SetState(DO_ITEM.Front_LsrBypass, true);
                //await WagoDO.SetState(DO_ITEM.Back_LsrBypass, true);

                await WagoDO.ResetSaftyRelay();

                bool anyDriverAlarm = WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2) ||
                    WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_3) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_4);

                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
                return true;
            }
            catch (SocketException ex)
            {
                logger.LogError(ex, "SocketException occurred");
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while resetting motor");
                AlarmManager.AddAlarm(AlarmCodes.Code_Error_In_System, false);
                return false;
            }
        }
        protected override bool CheckEMOButtonNoRelease()
        {
            bool button1NoRelease = base.CheckEMOButtonNoRelease();
            if (button1NoRelease)
                return true;
            return WagoDI.GetState(DI_ITEM.EMO_Button_2);
        }

        protected override void WagoDI_OnBumpSensorPressed(object? sender, EventArgs e)
        {
            base.WagoDI_OnBumpSensorPressed(sender, e);
        }

        protected override void EMOButtonPressedHandler(object? sender, EventArgs e)
        {
            base.EMOButtonPressedHandler(sender, e);
        }

        public override async Task<bool> Battery1Lock()
        {
            if (!IsLockActionAllow(1, out string rejectReason))
            {
                logger.LogError(rejectReason);
                return false;
            }
            logger.LogTrace("Demo Room Mini AGV- Try Lock Battery No.1 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(1, BAT_LOCK_ACTION.LOCK);
            return WaitBatteryLocked(1);
        }
        public override async Task<bool> Battery2Lock()
        {
            if (!IsLockActionAllow(2, out string rejectReason))
            {
                logger.LogError(rejectReason);
                return false;
            }
            logger.LogTrace("Demo Room Mini AGV- Try Lock Battery No.2 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(2, BAT_LOCK_ACTION.LOCK);
            return WaitBatteryLocked(2);
        }

        public override async Task<bool> Battery1UnLock()
        {
            if (!IsUnlockActionAllow(1, out string rejectReason))
            {
                logger.LogError(rejectReason);
                return false;
            }


            await BatteryUnLockSemaphoreSlim.WaitAsync();
            try
            {

                logger.LogTrace("Demo Room Mini AGV- Try Unlock Battery No.1 [call service]");
                DemoMiniAGVControl.BatteryLockControlService(1, BAT_LOCK_ACTION.UNLOCK);
                return WaitBatteryUnLocked(1);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                BatteryUnLockSemaphoreSlim.Release();
            }
        }
        public override async Task<bool> Battery2UnLock()
        {
            if (!IsUnlockActionAllow(2, out string rejectReason))
            {
                logger.LogError(rejectReason);
                return false;
            }
            await BatteryUnLockSemaphoreSlim.WaitAsync();
            try
            {
                logger.LogTrace("Demo Room Mini AGV- Try Unlock Battery No.2 [call service]");
                DemoMiniAGVControl.BatteryLockControlService(2, BAT_LOCK_ACTION.UNLOCK);
                return WaitBatteryUnLocked(2);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                BatteryUnLockSemaphoreSlim.Release();
            }
        }

        internal void BatteryLockActionStop()
        {
            DemoMiniAGVControl.BatteryLockControlService(0, BAT_LOCK_ACTION.Stop);
        }
        /// <summary>
        /// 判斷當前是否可以操作電池解鎖
        /// </summary>
        /// <param name="toUnlockBatNumber"></param>
        /// <param name="rejectReason"></param>
        /// <returns></returns>
        protected override bool IsUnlockActionAllow(int toUnlockBatNumber, out string rejectReason)
        {


            //不可解鎖的情況: 1.僅有一顆電池正在服役中
            rejectReason = "";
            //GetExistSensorState(toUnlockBatNumber, out bool exist1_front, out bool exist2_back, out bool exist3_docked);
            GetExistSensorState(1, out bool bat1_exist1_front, out bool bat1_exist2_back, out bool bat1_exist3_docked);
            GetExistSensorState(2, out bool bat2_exist1_front, out bool bat2_exist2_back, out bool bat2_exist3_docked);


            bool _isToUnlockBatIsNotExist = toUnlockBatNumber == 1 ? !bat1_exist3_docked : !bat2_exist3_docked;

            if (_isToUnlockBatIsNotExist)
                return true;//請求解鎖的那一顆電池目前不存在

            bool _isOnlyOneBatteryDocked = (bat1_exist3_docked && !bat2_exist3_docked) || !bat1_exist3_docked && bat2_exist3_docked;

            if (_isOnlyOneBatteryDocked && !_isToUnlockBatIsNotExist)
            {
                rejectReason = "當前只有一顆電池";
                return false;
            }
            else
            {
                return true;
            }
        }
        /// <summary>
        /// 判斷當前是否可以操作電池鎖定
        /// </summary>
        /// <param name="toLockBatNumber"></param>
        /// <param name="rejectReason"></param>
        /// <returns></returns>
        protected override bool IsLockActionAllow(int toLockBatNumber, out string rejectReason)
        {
            rejectReason = "";

            GetExistSensorState(toLockBatNumber, out bool exist1_front, out bool exist2_back, out bool exist3_docked);

            if (exist1_front && !exist3_docked)
            {
                rejectReason = $"電池-{toLockBatNumber}電池目前的位置-不可進行鎖定動作";
                return false;
            }

            return true;
        }

        void GetExistSensorState(int batNumber, out bool exist1_front, out bool exist2_back, out bool exist3_docked)
        {
            if (batNumber == 1)
            {
                exist1_front = WagoDI.GetState(DI_ITEM.Battery_1_Exist_1);
                exist2_back = WagoDI.GetState(DI_ITEM.Battery_1_Exist_2);
                exist3_docked = WagoDI.GetState(DI_ITEM.Battery_1_Exist_3);
            }
            else
            {

                exist1_front = WagoDI.GetState(DI_ITEM.Battery_2_Exist_1);
                exist2_back = WagoDI.GetState(DI_ITEM.Battery_2_Exist_2);
                exist3_docked = WagoDI.GetState(DI_ITEM.Battery_2_Exist_3);
            }
        }

        public override async void StartPublishIOListsMsg()
        {
            await Task.Delay(10);
            _ = Task.Run(async () =>
            {
                logger.LogTrace($"Start publish IOLists! [Use {IOlistsMsg_KGS.RosMessageName}]");

                IOlistsMsg_KGS payload = new IOlistsMsg_KGS();

                IOlistMsg_KGSBase[] lastInputsIOTable = GetCurrentInputIOTable();
                IOlistMsg_KGSBase[] lastOutputsIOTable = GetCurrentInputIOTable();

                PublishIOListsMsg(lastInputsIOTable);
                PublishIOListsMsg(lastOutputsIOTable);

                while (true)
                {
                    await Task.Delay(1);

                    IOlistMsg_KGSBase[] _currentInputsIOTable = GetCurrentInputIOTable();
                    IOlistMsg_KGSBase[] _currentOutputsIOTable = GetCurrentOutputIOTable();

                    bool _isInputsChanged = IsIOChanged(_currentInputsIOTable, lastInputsIOTable);
                    bool _isOutputsChanged = IsIOChanged(_currentOutputsIOTable, lastOutputsIOTable);

                    if (_isInputsChanged)
                        PublishIOListsMsg(_currentInputsIOTable);
                    if (_isOutputsChanged)
                        PublishIOListsMsg(_currentOutputsIOTable);

                    lastInputsIOTable = _currentInputsIOTable;
                    lastOutputsIOTable = _currentOutputsIOTable;

                }
                IOlistMsg_KGSBase[] GetCurrentInputIOTable()
                {
                    return WagoDI.VCSInputs.Select(signal => new IOlistMsg_KGSBase("X", signal.State ? 1 : 0, WagoDI.VCSInputs.IndexOf(signal))).ToArray();
                }
                IOlistMsg_KGSBase[] GetCurrentOutputIOTable()
                {
                    return WagoDO.VCSOutputs.Select(signal => new IOlistMsg_KGSBase("Y", signal.State ? 1 : 0, WagoDO.VCSOutputs.IndexOf(signal))).ToArray();
                }
                void PublishIOListsMsg(IOlistMsg_KGSBase[] IOTable)
                {
                    payload.IOtable = IOTable;
                    AGVC?.IOListMsgPublisher(payload);
                }
                bool IsIOChanged(IOlistMsg_KGSBase[] table1, IOlistMsg[] table2)
                {
                    return !table1.Select(io => io.Coil).SequenceEqual(table2.Select(io => io.Coil));
                }
            });
        }
    }
}
