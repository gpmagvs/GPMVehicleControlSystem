using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;

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

        public byte LocalizationStatus { get; private set; } = 0x00;

        public override void OnAlarmResetHandle()
        {
            _LaserModeSettingError = _SickConnectionError = false;
        }
        public override void CheckStateDataContent()
        {
            if (LocalizationStatus != Data.loc_status)
            {
                LocalizationStatus = Data.loc_status;
                if (LocalizationStatus != 10)
                {
                    LOG.WARN($"Map Compare Rate Too Low [From Sick Data]");
                }
            }
        }
    }
}
