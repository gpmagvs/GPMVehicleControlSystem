
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
        public override async Task<bool> ResetMotor(bool bypass_when_motor_busy_on = true)
        {
            try
            {
                //await WagoDO.ResetSaftyRelay();

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
