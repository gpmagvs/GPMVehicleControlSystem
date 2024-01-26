
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Emulators;
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
        public DemoMiniAGV() : base()
        {
            LOG.INFO("Demo Mini AGV Created.");
        }
        protected override async Task DOSignalDefaultSetting()
        {
            WagoDO.AllOFF();
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Front_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Back_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_1, new bool[] { true, true, false, true });
            await WagoDO.SetState(DO_ITEM.Instrument_Servo_On, true);
            await Laser.ModeSwitch(0);
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
        public override async Task<bool> ResetMotor(bool bypass_when_motor_busy_on = true)
        {
            try
            {
                await WagoDO.ResetSaftyRelay();
                if (Parameters.WagoSimulation)
                {
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Safty_PLC_Output, true);
                }
                bool anyDriverAlarm = WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2) ||
                    WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_3) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_4);
                StaEmuManager.agvRosEmu?.ClearDriversErrorCodes();
                if (bypass_when_motor_busy_on & !anyDriverAlarm)
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
            if (Parameters.WagoSimulation)
            {
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Safty_PLC_Output, false);
            }
        }

        protected override void EMOButtonPressedHandler(object? sender, EventArgs e)
        {
            base.EMOButtonPressedHandler(sender, e);
            if (Parameters.WagoSimulation)
            {
                StaEmuManager.wagoEmu.SetState(DI_ITEM.Safty_PLC_Output, false);
            }
        }

        public override async Task<bool> Battery1Lock()
        {
            LOG.TRACE("Demo Room Mini AGV- Try Lock Battery No.1 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(1, BAT_LOCK_ACTION.LOCK);
            return WaitBatteryLocked(1);
        }
        public override async Task<bool> Battery2Lock()
        {
            LOG.TRACE("Demo Room Mini AGV- Try Lock Battery No.2 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(2, BAT_LOCK_ACTION.LOCK);
            return WaitBatteryLocked(2);
        }

        public override async Task<bool> Battery1UnLock()
        {
            LOG.TRACE("Demo Room Mini AGV- Try Unlock Battery No.1 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(1, BAT_LOCK_ACTION.UNLOCK);
            return WaitBatteryUnLocked(1);
        }
        public override async Task<bool> Battery2UnLock()
        {
            LOG.TRACE("Demo Room Mini AGV- Try Unlock Battery No.2 [call service]");
            DemoMiniAGVControl.BatteryLockControlService(2, BAT_LOCK_ACTION.UNLOCK);
            return WaitBatteryUnLocked(2);
        }

    }
}
