using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsBattery : CarComponent
    {
        public bool IsCharging => Data.dischargeCurrent == 0 && Data.chargeCurrent != 0;

        public clsStateCheckSpec ChargingCheckSpec = new clsStateCheckSpec { };

        public clsStateCheckSpec DischargeCheckSpec = new clsStateCheckSpec
        {
            MaxCurrentAllow = 5000
        };
        public new BatteryState Data => StateData == null ? new BatteryState() : (BatteryState)StateData;

        public override COMPOENT_NAME component_name => COMPOENT_NAME.BATTERY;

        public override void CheckStateDataContent()
        {
            var error_code = Data.errorCode;
            if (error_code != 0)
            {
                if (error_code == 1)
                    current_alarm_code = AlarmCodes.Over_Voltage;
                else if (error_code == 2)
                    current_alarm_code = AlarmCodes.Under_Voltage;
                else if (error_code == 4)
                    current_alarm_code = AlarmCodes.Over_Current_Charge;
                else if (error_code == 8)
                    current_alarm_code = AlarmCodes.Over_Current_Discharge;
                else if (error_code == 16)
                    current_alarm_code = AlarmCodes.Under_Current_Charge;
                else if (error_code == 32)
                    current_alarm_code = AlarmCodes.Over_Temperature;
                else if (error_code == 64)
                    current_alarm_code = AlarmCodes.Under_Temperature;
            }
            else
            {
                current_alarm_code = AlarmCodes.None;
            }
        }
    }

    public class clsStateCheckSpec
    {
        public double MinCurrentAllow { get; set; } = 80;
        public double MaxCurrentAllow { get; set; } = 4000;
        public double MinVoltageAllow { get; set; } = 20.0;
        public double MaxVoltageAllow { get; set; } = 20.0;
    }

}
