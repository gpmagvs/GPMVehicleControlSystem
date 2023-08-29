using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsDriver : CarComponent
    {
        public enum DRIVER_LOCATION
        {
            LEFT,
            RIGHT,
            LEFT_FORWARD,
            RIGHT_FORWARD,
            LEFT_BACKWARD,
            RIGHT_BACKWARD,
            FORK
        }
        public override COMPOENT_NAME component_name => COMPOENT_NAME.DRIVER;
        public DRIVER_LOCATION location = DRIVER_LOCATION.RIGHT;
        public new DriverState Data => StateData == null ? new DriverState() : (DriverState)StateData;

        public double CurrentPosition { get => Data.position; }

        public override string alarm_locate_in_name => component_name.ToString();

        public override void CheckStateDataContent()
        {
            DriverState _driverState = (DriverState)StateData;
            var code = _driverState.errorCode;
            if (code != 0)
            {
                if (code == 1)
                    Current_Alarm_Code = AlarmCodes.Over_current_protection;
                else if (code == 2)
                    Current_Alarm_Code = AlarmCodes.Over_load_protection;
                else if (code == 3)
                    Current_Alarm_Code = AlarmCodes.Motor_Feedback_Signal_Error;
                else if (code == 4)
                    Current_Alarm_Code = AlarmCodes.Over_voltage_protection;
                else if (code == 5)
                    Current_Alarm_Code = AlarmCodes.Under_voltage_protection;
                else if (code == 6)
                    Current_Alarm_Code = AlarmCodes.Motor_Driver_Over_Heat_Error;
                else if (code == 7)
                    Current_Alarm_Code = AlarmCodes.Motor_Active_Error;
                else if (code == 8)
                    Current_Alarm_Code = AlarmCodes.Over_speed_protection;
                else if (code == 10)
                    Current_Alarm_Code = AlarmCodes.Over_heat_protection;
                else if (code == 12)
                    Current_Alarm_Code = AlarmCodes.Motor_Over_Speed_Error;
                else if (code == 13)
                    Current_Alarm_Code = AlarmCodes.Motor_Encoder_Error;
                else if (code == 14)
                    Current_Alarm_Code = AlarmCodes.Motor_Run_Forbid;
                else if (code == 15)
                    Current_Alarm_Code = AlarmCodes.Motor_Extern_Stop;
                else if (code == 20)
                    Current_Alarm_Code = AlarmCodes.Motor_Hall_Sequence_Error;
                else if (code == 21)
                    Current_Alarm_Code = AlarmCodes.Command_error;
                else if (code == 22)
                    Current_Alarm_Code = AlarmCodes.Motor_Parameters_Error;
                else 
                    Current_Alarm_Code = AlarmCodes.Other_error;
            }
            else
                Current_Alarm_Code = AlarmCodes.None;

        }
    }
}
