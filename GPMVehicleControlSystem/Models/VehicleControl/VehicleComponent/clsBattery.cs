using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsBattery : CarComponent
    {
        public clsStateCheckSpec ChargingCheckSpec = new clsStateCheckSpec { };

        public clsStateCheckSpec DischargeCheckSpec = new clsStateCheckSpec
        {
            MaxCurrentAllow = 5000
        };
        public new BatteryState Data => StateData == null ? new BatteryState() : (BatteryState)StateData;

        public override COMPOENT_NAME component_name => COMPOENT_NAME.BATTERY;

        public override string alarm_locate_in_name => component_name.ToString();

        public int ChargeAmpThreshold { get; internal set; } = 650;
        public bool IsCharging()
        {
            bool _charging = Data.dischargeCurrent == 0 && Data.chargeCurrent >= ChargeAmpThreshold;
            if (!_charging && Data.chargeCurrent > 0)
            {
                Thread.Sleep(1000);
                _charging = Data.dischargeCurrent == 0 && Data.chargeCurrent >= ChargeAmpThreshold;
            }
            return _charging;
        }
        public override void CheckStateDataContent()
        {
            var error_code = Data.errorCode;
            Current_Warning_Code = error_code.ToBatteryAlarmCode();
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
