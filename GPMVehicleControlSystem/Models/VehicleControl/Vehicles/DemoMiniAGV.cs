
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Emulators;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using System.Net.Sockets;
using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public class DemoMiniAGV : TsmcMiniAGV
    {
        public DemoMiniAGV() : base()
        {
            LOG.INFO("Demo Mini AGV Created.");
        }
        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();
        }
        protected override async Task DOSignalDefaultSetting()
        {
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_G, false);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_B, false);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            await WagoDO.SetState(DO_ITEM.Back_LsrBypass, false);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, false);
            await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Front_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_1, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_2, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_3, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_4, true);
            await WagoDO.SetState(DO_ITEM.Instrument_Servo_On, true);
            await WagoDO.SetState(DO_ITEM.Battery_1_Electricity_Interrupt, false);
            await WagoDO.SetState(DO_ITEM.Battery_2_Electricity_Interrupt, false);
            await Laser.ModeSwitch(0);
        }
        public override async Task<bool> ResetMotor(bool bypass_when_motor_busy_on = true)
        {
            try
            {
                await WagoDO.ResetSaftyRelay();

                bool anyDriverAlarm = WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2) ||
                    WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_3) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_4);
                StaEmuManager.agvRosEmu?.ClearDriversErrorCodes();
                if (bypass_when_motor_busy_on & !anyDriverAlarm)
                    return true;
                //await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
                //await Task.Delay(200);
                //await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                //await Task.Delay(200);
                //await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);
                //await Task.Delay(200);
                //await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
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
    }
}
