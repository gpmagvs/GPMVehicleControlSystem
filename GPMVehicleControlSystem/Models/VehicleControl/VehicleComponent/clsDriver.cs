using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;

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

        public override void CheckStateDataContent()
        {

            DriverState _driverState = (DriverState)StateData;
            if (_driverState.errorCode != 0)
            {
                var code = _driverState.errorCode;
                if (code == 1)
                    current_alarm_code = AlarmCodes.Under_voltage_protection;
                if (code == 2)
                    current_alarm_code = AlarmCodes.Under_current_protection;
                if (code == 3)
                    current_alarm_code = AlarmCodes.Over_voltage_protection;
                if (code == 4)
                    current_alarm_code = AlarmCodes.Over_current_protection;
                if (code == 5)
                    current_alarm_code = AlarmCodes.Over_heat_protection;
                if (code == 6)
                    current_alarm_code = AlarmCodes.Over_load_protection;
                if (code == 7)
                    current_alarm_code = AlarmCodes.Over_regeneration_load_protection;
                if (code == 8)
                    current_alarm_code = AlarmCodes.Over_speed_protection;
                if (code == 9)
                    current_alarm_code = AlarmCodes.Deviation_excess_protection;
                if (code == 10)
                    current_alarm_code = AlarmCodes.AConnection_error_protection;
                if (code == 11)
                    current_alarm_code = AlarmCodes.Status_Error;
                if (code == 12)
                    current_alarm_code = AlarmCodes.Communication_error;
                if (code == 13)
                    current_alarm_code = AlarmCodes.Alarm_input_protection;
                if (code == 14)
                    current_alarm_code = AlarmCodes.Command_error;
                if (code == 15)
                    current_alarm_code = AlarmCodes.Overtorque;
                if (code == 16)
                    current_alarm_code = AlarmCodes.Other_error;
            }
            else
                current_alarm_code = AlarmCodes.None;

        }
    }
}
