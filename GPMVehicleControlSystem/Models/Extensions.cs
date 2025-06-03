using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;

namespace GPMVehicleControlSystem.Models
{
    public static class Extensions
    {
        public static int ToInt(this bool value)
        {
            return value ? 1 : 0;
        }
        public static string ToSymbol(this bool value, string symbol_true, string sybol_false)
        {
            return value ? symbol_true : sybol_false;
        }

        public static bool IsMotorAlarm(this clsAlarmCode alarm)
        {
            if (alarm == null || alarm.EAlarmCode == AlarmCodes.Unknown)
                return false;
            return alarm.EAlarmCode.IsMotorAlarm();
        }

        public static bool IsMotorAlarm(this AlarmCodes alarmCode)
        {
            AlarmCodes[] motorAlarms = new AlarmCodes[]
            {
                 AlarmCodes.Motor_Active_Error,
                 AlarmCodes.Motor_Driver_Over_Heat_Error,
                 AlarmCodes.Motor_Over_Speed_Error,
                 AlarmCodes.Motor_Encoder_Error,
                 AlarmCodes.Motor_Run_Forbid,
                 AlarmCodes.Motor_Extern_Stop,
                 AlarmCodes.Motor_Hall_Sequence_Error,
                 AlarmCodes.Motor_Parameters_Error,
                 AlarmCodes.Under_Voltage,
                 AlarmCodes.Over_current_protection,

                 AlarmCodes.Over_load_protection,
                 AlarmCodes.Motor_Feedback_Signal_Error,
                 AlarmCodes.Over_voltage_protection,
                 AlarmCodes.Under_voltage_protection,
                 AlarmCodes.Motor_Driver_Over_Heat_Error,
                 AlarmCodes.Motor_Active_Error,
                 AlarmCodes.Over_speed_protection,
                 AlarmCodes.Over_heat_protection,
                 AlarmCodes.Motor_Over_Speed_Error,
                 AlarmCodes.Motor_Encoder_Error,
                 AlarmCodes.Motor_Run_Forbid,
                 AlarmCodes.Motor_Extern_Stop,
                 AlarmCodes.Motor_Hall_Sequence_Error,

                 AlarmCodes.Vertical_Motor_IO_Error,
                 AlarmCodes.Wheel_Motor_IO_Error,
                 AlarmCodes.Wheel_Motor_IO_Error_Left,
                 AlarmCodes.Wheel_Motor_IO_Error_Right,
                 AlarmCodes.Wheel_Motor_IO_Error_Left_Front,
                 AlarmCodes.Wheel_Motor_IO_Error_Left_Rear,
                 AlarmCodes.Wheel_Motor_IO_Error_Right_Front,
                 AlarmCodes.Wheel_Motor_IO_Error_Right_Rear,
            };
            return motorAlarms.Contains(alarmCode);
        }
    }
}
