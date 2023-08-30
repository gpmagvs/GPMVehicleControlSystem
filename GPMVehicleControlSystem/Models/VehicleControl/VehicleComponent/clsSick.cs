using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsSick : CarComponent
    {
        public override COMPOENT_NAME component_name { get; } = COMPOENT_NAME.SICK;
        public new LocalizationControllerResultMessage0502 Data => StateData == null ? new LocalizationControllerResultMessage0502() : (LocalizationControllerResultMessage0502)StateData;
        public double MapSocre => Data.map_match_status / 100.0;
        public double HeadingAngle => Data.heading / 1000.0;

        public override string alarm_locate_in_name => component_name.ToString();

        public bool _LaserModeSettingError = false;
        public bool _SickConnectionError = false;

        public bool LaserModeSettingError
        {
            get => _LaserModeSettingError;
            set
            {
                if (_LaserModeSettingError != value)
                {
                    if (value)
                    {
                        Current_Alarm_Code = AlarmCodes.Laser_Mode_value_fail;
                    }
                    _LaserModeSettingError = value;

                }
            }
        }
        public bool SickConnectionError
        {
            get => _SickConnectionError;
            set
            {
                if (_SickConnectionError != value)
                {
                    if (value)
                    {
                        Current_Warning_Code = AlarmCodes.Sick_Lidar_Communication_Error;
                    }
                    _SickConnectionError = value;

                }
            }
        }
        public override void OnAlarmResetHandle()
        {
            _LaserModeSettingError = _SickConnectionError = false;
        }
        public override void CheckStateDataContent()
        {
            //  1 byte LocalizationStatus [0...100, 10: OK, 20: Warning, 30: Not localized, 40: System error]
            if (Data.loc_status != 10)
                Current_Warning_Code = AlarmCodes.Map_Recognition_Rate_Too_Low;
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }
        }
    }
}
