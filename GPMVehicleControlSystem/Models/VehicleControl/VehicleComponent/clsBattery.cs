using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsBattery : CarComponent
    {

        public override string RosParmYamlPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "catkin_ws\\src\\gpm_project\\battery\\param\\battery_setting.yaml");
        public static event EventHandler<clsBattery> OnBatteryUnderVoltage;
        public static event EventHandler<clsBattery> OnBatteryOverTemperature;
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

        public override bool IsCommunicationError
        {
            get => base.IsCommunicationError || Data.voltage == 0;
            set
            {
                base.IsCommunicationError = value;
            }
        }

        public bool TryGetOverVoltageThreshold(out double threshold)
        {
            threshold = -1;
            try
            {
                string _threshold = RosNodeSettingParam["/battery/Alarm/overVoltage"];
                return double.TryParse(_threshold, out threshold);

            }
            catch (Exception)
            {
                return false;
            }
        }

        public int ChargeAmpThreshold { get; internal set; } = 650;
        public bool IsCharging()
        {
            return Data.chargeCurrent > 650;
        }
        public override bool CheckStateDataContent()
        {

            if (!base.CheckStateDataContent())
                return false;

            var error_code = Data.errorCode;
            Current_Warning_Code = error_code.ToBatteryAlarmCode();
            return true;
        }
        public override AlarmCodes Current_Warning_Code
        {
            get => base.Current_Warning_Code;
            set
            {
                bool hasChanged = base.Current_Warning_Code != value;
                if (hasChanged && value == AlarmCodes.Under_Voltage)
                {
                    OnBatteryUnderVoltage?.Invoke(this, this);
                }
                base.Current_Warning_Code = value;
            }
        }

        protected override void HandleCommunicationError()
        {
            logger.Info($"[Battery-{Data.batteryID}] state={Data.state}");
            base.HandleCommunicationError();
            Current_Warning_Code = AlarmCodes.Battery_Status_Error_;
        }

        protected override void HandleCommunicationRecovery()
        {
            base.HandleCommunicationRecovery();
            Current_Warning_Code = AlarmCodes.None;
        }

        protected override void _CommunicationErrorJudge()
        {
            if (Data.state == -1)
            {
                IsCommunicationError = true;
                return;
            }
            base._CommunicationErrorJudge();
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
