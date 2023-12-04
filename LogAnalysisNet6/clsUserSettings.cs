using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogAnalysisNet6
{
    public class clsUserSettings
    {

        private string _LogFolder;
        private DateTime _IOQueryFromTime;
        private DateTime _IOQueryToTime;

        public string LogFolder
        {
            get => _LogFolder;
            set
            {
                _LogFolder = value;
                Save();
            }
        }
        public DateTime IOQueryFromTime
        {
            get => _IOQueryFromTime;
            set
            {
                _IOQueryFromTime = value;
                Save();
            }
        }
        public DateTime IOQueryToTime
        {
            get => _IOQueryToTime;
            set
            {
                _IOQueryToTime = value;
                Save();
            }
        }

        internal static string tempFilePath => Path.Combine(Path.GetTempPath(), "log_analysis_user_settings.json");
        public void Save()
        {
            File.WriteAllText(tempFilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static clsUserSettings? RestoreFromTempFile()
        {
            if (File.Exists(tempFilePath))
            {
                var settings = JsonConvert.DeserializeObject<clsUserSettings>(File.ReadAllText(tempFilePath));
                return settings;

            }
            else
                return new clsUserSettings();

        }

    }
}
