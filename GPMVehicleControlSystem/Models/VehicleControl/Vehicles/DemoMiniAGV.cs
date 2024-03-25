
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using System.Net.Sockets;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;

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

        public DemoMiniAGV() : base()
        {
            LOG.INFO("Demo Mini AGV Created.");
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
        public override async Task<bool> ResetMotor(bool triggerByResetButtonPush, bool bypass_when_motor_busy_on = true)
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

                if (bypass_when_motor_busy_on && !anyDriverAlarm)
                    return true;

                return true;
            }
            catch (SocketException ex)
            {
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
            catch (Exception ex)
            {
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
                LOG.Critical(rejectReason);
                return false;
            }
            LOG.TRACE("Demo Room Mini AGV- Try Lock Battery No.1 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(1, BAT_LOCK_ACTION.LOCK);
            return WaitBatteryLocked(1);
        }
        public override async Task<bool> Battery2Lock()
        {
            if (!IsLockActionAllow(2, out string rejectReason))
            {
                LOG.Critical(rejectReason);
                return false;
            }
            LOG.TRACE("Demo Room Mini AGV- Try Lock Battery No.2 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(2, BAT_LOCK_ACTION.LOCK);
            return WaitBatteryLocked(2);
        }

        public override async Task<bool> Battery1UnLock()
        {
            if (!IsUnlockActionAllow(2, out string rejectReason))
            {
                LOG.Critical(rejectReason);
                return false;
            }
            await BatteryUnLockSemaphoreSlim.WaitAsync();
            try
            {

                LOG.TRACE("Demo Room Mini AGV- Try Unlock Battery No.1 [call service]");
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
                LOG.Critical(rejectReason);
                return false;
            }
            await BatteryUnLockSemaphoreSlim.WaitAsync();
            try
            {
                LOG.TRACE("Demo Room Mini AGV- Try Unlock Battery No.2 [call service]");
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

            bool _isOnlyOneBatteryDocked = (bat1_exist3_docked && !bat2_exist3_docked) || !bat1_exist3_docked && bat2_exist3_docked;

            if (_isOnlyOneBatteryDocked)
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
    }
}
