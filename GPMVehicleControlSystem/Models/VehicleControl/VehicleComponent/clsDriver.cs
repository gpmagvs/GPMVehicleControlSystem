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

        public override string alarm_locate_in_name => component_name.ToString();

        public override void CheckStateDataContent()
        {

            DriverState _driverState = (DriverState)StateData;
            if (_driverState.errorCode != 0)
            {
                var code = _driverState.errorCode;
                if (code == 1)
                    Current_Warning_Code = AlarmCodes.Under_voltage_protection;
                if (code == 2)
                    Current_Warning_Code = AlarmCodes.Under_current_protection;
                if (code == 3)
                    Current_Warning_Code = AlarmCodes.Over_voltage_protection;
                if (code == 4)
                    Current_Warning_Code = AlarmCodes.Over_current_protection;
                if (code == 5)
                    Current_Warning_Code = AlarmCodes.Over_heat_protection;
                if (code == 6)
                    Current_Warning_Code = AlarmCodes.Over_load_protection;
                if (code == 7)
                    Current_Warning_Code = AlarmCodes.Over_regeneration_load_protection;
                if (code == 8)
                    Current_Warning_Code = AlarmCodes.Over_speed_protection;
                if (code == 9)
                    Current_Warning_Code = AlarmCodes.Deviation_excess_protection;
                if (code == 10)
                    Current_Warning_Code = AlarmCodes.AConnection_error_protection;
                if (code == 11)
                    Current_Warning_Code = AlarmCodes.Status_Error;
                if (code == 12)
                    Current_Warning_Code = AlarmCodes.Communication_error;
                if (code == 13)
                    Current_Warning_Code = AlarmCodes.Alarm_input_protection;
                if (code == 14)
                    Current_Warning_Code = AlarmCodes.Command_error;
                if (code == 15)
                    Current_Warning_Code = AlarmCodes.Overtorque;
                if (code == 16)
                    Current_Warning_Code = AlarmCodes.Other_error;
            }
            else
                Current_Warning_Code = AlarmCodes.None;

        }
    }
}
