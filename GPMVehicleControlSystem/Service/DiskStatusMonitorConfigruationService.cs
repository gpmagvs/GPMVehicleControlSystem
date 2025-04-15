using Newtonsoft.Json;

namespace GPMVehicleControlSystem.Service
{
    public class DiskStatusMonitorConfigruationService
    {
        internal static event EventHandler<DiskMonitorParams> OnConfigurationChanged;
        string diskMonitorParamjsonFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/DiskMonitorParam.json");

        public DiskMonitorParams? LoadDiskMonitorParam()
        {
            if (File.Exists(diskMonitorParamjsonFilePath))
            {
                try
                {
                    var existparams = JsonConvert.DeserializeObject<DiskMonitorParams>(File.ReadAllText(diskMonitorParamjsonFilePath));
                    _RollbackJsonFile(existparams);
                    return existparams;
                }
                catch (Exception)
                {
                    return new DiskMonitorParams();
                }
            }
            else
            {
                var defaultParams = new DiskMonitorParams();
                _RollbackJsonFile(defaultParams);
                return defaultParams;
            }

        }

        internal void SaveConfiguration(DiskMonitorParams configruation)
        {
            _RollbackJsonFile(configruation);
            OnConfigurationChanged?.Invoke(this, configruation);
        }
        void _RollbackJsonFile(DiskMonitorParams _defaultParam)
        {
            if (_defaultParam == null)
                return;
            File.WriteAllText(diskMonitorParamjsonFilePath, JsonConvert.SerializeObject(_defaultParam, Formatting.Indented));
        }
    }
}
