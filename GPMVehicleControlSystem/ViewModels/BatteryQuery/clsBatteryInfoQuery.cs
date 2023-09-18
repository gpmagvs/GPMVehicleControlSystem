using System.Globalization;
using static GPMVehicleControlSystem.ViewModels.BatteryQuery.clsBatQueryOptions;

namespace GPMVehicleControlSystem.ViewModels.BatteryQuery
{
    public class clsBatteryInfoQuery
    {
        public static string GPMLogFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "GPMLog");
            }
        }

        public clsBatQueryOptions options { get; }

        public clsBatteryInfoQuery(clsBatQueryOptions options)
        {
            this.options = options;
        }

        public async Task<Dictionary<string, double>> Query()
        {
            return await Task.Factory.StartNew(() =>
            {
                List<string> files = GetMatchTimeLogFiles();
                Dictionary<int, List<clsBatteryInfo>> datas = new Dictionary<int, List<clsBatteryInfo>>();
                foreach (string file in files)
                {
                    using (StreamReader sr = new StreamReader(file))
                    {
                        string line = null;
                        var directoryName = Path.GetDirectoryName(file);
                        directoryName = Path.GetDirectoryName(directoryName);
                        directoryName = Path.GetFileNameWithoutExtension(directoryName);
                        DateTime.TryParseExact(directoryName, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.AllowLeadingWhite, out DateTime date);
                        //ID, Level(%), Voltage(mV), ChargeCurrent(mA), DischargeCurrent(mA), Temperature(C)
                        Dictionary<int, DateTime> lastTime = new Dictionary<int, DateTime>()
                        {
                            { 1, DateTime.MinValue },
                            { 2, DateTime.MinValue },
                        };
                        while ((line = sr.ReadLine()) != null)
                        {
                            //I0918 11:34:17.215010 14535 battery_inner.cpp:289] 0, 92, 254, 0, 460, 26
                            string[] splited = line.Split(',');
                            if (splited.Length != 6)
                                continue;
                            var id_str = splited[0].Split(']')[1].Trim();
                            if (!int.TryParse(id_str, out int id))
                            {
                                continue;
                            }
                            if (id == 0)
                                continue;
                            var time_str = splited[0].Substring(6, 15);
                            DateTime.TryParseExact(time_str, "HH:mm:ss.ffffff", CultureInfo.CurrentCulture, DateTimeStyles.AllowLeadingWhite, out DateTime time);
                            DateTime.TryParseExact($"{date.ToString("yyyy-MM-dd")} {time.ToString("HH:mm:ss.ffffff")}", "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.CurrentCulture, DateTimeStyles.AllowLeadingWhite, out DateTime date_time);

                            if ((date_time - lastTime[id]).TotalMinutes < 10)
                                continue;
                            lastTime[id] = date_time;

                            double level = double.Parse(splited[1]);
                            double voltage = double.Parse(splited[2]);
                            double charge_current = double.Parse(splited[3]);
                            double discharge_current = double.Parse(splited[4]);
                            double temperature = double.Parse(splited[5]);

                            if (!datas.ContainsKey(id))
                                datas.Add(id, new List<clsBatteryInfo>());

                            datas[id].Add(new clsBatteryInfo
                            {
                                Level = level,
                                Voltage = voltage,
                                ChargeCurrent = charge_current,
                                DischargeCurrent = discharge_current,
                                Temperature = temperature,
                                Time = date_time
                            });


                        }
                    }
                }

                datas = datas.OrderBy(dat => dat.Key).ToDictionary(d => d.Key, d => d.Value);
                if (options.item == QUERY_ITEM.Level.ToString())
                {
                    return datas[options.id].ToDictionary(dat => dat.Time.ToString("yyyy/MM/dd HH:mm:ss.ffffff"), dat => dat.Level);
                }

                if (options.item == QUERY_ITEM.Voltage.ToString())
                {
                    return datas[options.id].ToDictionary(dat => dat.Time.ToString("yyyy/MM/dd HH:mm:ss.ffffff"), dat => dat.Voltage);
                }

                if (options.item == QUERY_ITEM.Charge_current.ToString())
                {
                    return datas[options.id].ToDictionary(dat => dat.Time.ToString("yyyy/MM/dd HH:mm:ss.ffffff"), dat => dat.ChargeCurrent);
                }

                if (options.item == QUERY_ITEM.Discharge_current.ToString())
                {
                    return datas[options.id].ToDictionary(dat => dat.Time.ToString("yyyy/MM/dd HH:mm:ss.ffffff"), dat => dat.DischargeCurrent);
                }
                else
                    return new Dictionary<string, double>();

            });
        }

        private List<string> GetMatchTimeLogFiles()
        {
            var folders = Directory.GetDirectories(GPMLogFolder).Where(folder_path => IsInTime(folder_path));
            List<string> output_files = new List<string>();
            foreach (var folder in folders)
            {
                var files = Directory.GetFiles(Path.Combine(folder, "batteryLog"));
                foreach (var file in files)
                {
                    if (Path.GetFileNameWithoutExtension(file).Contains("batteryLog.INFO."))
                    {
                        output_files.Add(file);
                    }
                }

            }
            return output_files;
        }
        private bool IsInTime(string folder_path)
        {
            string folder_name = Path.GetFileNameWithoutExtension(folder_path);
            bool is_timeformat = DateTime.TryParseExact(folder_name, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.AllowLeadingWhite, out DateTime time);
            if (!is_timeformat)
                return false;
            else
            {
                return time >= options.timedt_range[0] && time <= options.timedt_range[1];
            }
        }
    }
}
