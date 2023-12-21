using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using Newtonsoft.Json;

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
        internal RawMicroScanDataMsg SickRawData = new RawMicroScanDataMsg();

        public bool LaserModeSettingError
        {
            get => _LaserModeSettingError;
            set
            {
                if (_LaserModeSettingError != value)
                {
                    if (value)
                    {
                        Current_Warning_Code = AlarmCodes.Laser_Mode_value_fail;
                        LogSickRawData(SickRawData);
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
                        LogSickRawData(SickRawData);
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
        public override async Task<bool> CheckStateDataContent()
        {

            if (! await base.CheckStateDataContent())
                return false;
            if (LocalizationStatus != Data.loc_status)
            {
                LocalizationStatus = Data.loc_status;
                if (LocalizationStatus != 10)
                {
                    LOG.WARN($"Map Compare Rate Too Low [From Sick Data]");
                }
            }
            return true;
        }

        private void LogSickRawData(RawMicroScanDataMsg sick_scanner_raw_data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(sick_scanner_raw_data, Formatting.Indented);
                string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), Path.Combine(AppSettingsHelper.LogFolder, "SickData"));
                Directory.CreateDirectory(LogFolder);
                var fileName = Path.Combine(LogFolder, "tmp_sick_data.json");
                using (StreamWriter writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"{DateTime.Now} {json}");
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR($"紀錄sick data 的過程中發生錯誤 {ex.Message}", ex);
            }
        }

    }
}
