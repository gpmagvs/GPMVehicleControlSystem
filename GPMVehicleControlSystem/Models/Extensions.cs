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
            };
            return motorAlarms.Contains(alarm.EAlarmCode);
        }

    }
}
