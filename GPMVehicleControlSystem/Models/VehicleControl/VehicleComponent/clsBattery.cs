using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsBattery : CarComponent
    {
        /// <summary>
        /// 電池位置(面向電池Port左/右)
        /// </summary>
        public enum BATTERY_LOCATION
        {
            RIGHT = 0,
            LEFT = 1,
            NAN = 404
        }
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
            return Data.chargeCurrent > 650;
        }
        public override async Task<bool> CheckStateDataContent()
        {

            if (!await base.CheckStateDataContent())
                return false;

            var error_code = Data.errorCode;
            Current_Warning_Code = error_code.ToBatteryAlarmCode();
            return true;
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
