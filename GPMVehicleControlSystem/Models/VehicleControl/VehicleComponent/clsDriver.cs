using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;

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
        }
        public override COMPOENT_NAME component_name => COMPOENT_NAME.DRIVER;
        public DRIVER_LOCATION location = DRIVER_LOCATION.RIGHT;
        public new DriverState Data => StateData == null ? new DriverState() : (DriverState)StateData;
        public override STATE CheckStateDataContent()
        {

            STATE _state = STATE.NORMAL;
            DriverState _driverState = (DriverState)StateData;
            if (_driverState.errorCode != 0)
            {
                var code = _driverState.errorCode;
                AlarmCodes alarm_code = AlarmCodes.Wheel_Motor_Alarm;
                if (code == 1)
                    alarm_code = AlarmCodes.Under_voltage_protection;
                if (code == 2)
                    alarm_code = AlarmCodes.Under_current_protection;
                if (code == 3)
                    alarm_code = AlarmCodes.Over_voltage_protection;
                if (code == 4)
                    alarm_code = AlarmCodes.Over_current_protection;
                if (code == 5)
                    alarm_code = AlarmCodes.Over_heat_protection;
                if (code == 6)
                    alarm_code = AlarmCodes.Over_load_protection;
                if (code == 7)
                    alarm_code = AlarmCodes.Over_regeneration_load_protection;
                if (code == 8)
                    alarm_code = AlarmCodes.Over_speed_protection;
                if (code == 9)
                    alarm_code = AlarmCodes.Deviation_excess_protection;
                if (code == 10)
                    alarm_code = AlarmCodes.AConnection_error_protection;
                if (code == 11)
                    alarm_code = AlarmCodes.Status_Error;
                if (code == 12)
                    alarm_code = AlarmCodes.Communication_error;
                if (code == 13)
                    alarm_code = AlarmCodes.Alarm_input_protection;
                if (code == 14)
                    alarm_code = AlarmCodes.Command_error;
                if (code == 15)
                    alarm_code = AlarmCodes.Overtorque;
                if (code == 16)
                    alarm_code = AlarmCodes.Other_error;
                AddAlarm(alarm_code);
            }
            return _state;

        }
    }
}
