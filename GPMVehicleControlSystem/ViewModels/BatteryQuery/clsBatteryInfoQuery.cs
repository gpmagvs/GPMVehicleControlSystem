using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models;
using System;
using System.Globalization;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.ViewModels.BatteryQuery.clsBatQueryOptions;

namespace GPMVehicleControlSystem.ViewModels.BatteryQuery
{
    public class clsBatteryInfoQuery
    {
        public static string GPMLogFolder
        {
            get
            {
                return StaStored.CurrentVechicle.Parameters.BatteryModule.BatteryLogFolder;
            }
        }

        public clsBatQueryOptions options { get; }

        public clsBatteryInfoQuery(clsBatQueryOptions options)
        {
            this.options = options;
        }
        public class clsStatus
        {
            public double value { get; set; }
            public SUB_STATUS status { get; set; } = SUB_STATUS.IDLE;
        }
        public async Task<Dictionary<DateTime, clsStatus>> Query()
        {
            return await Task.Factory.StartNew(() =>
            {
                List<string> files = GetMatchTimeLogFiles();
                Dictionary<int, List<clsBatteryInfo>> datas = new Dictionary<int, List<clsBatteryInfo>>();
                foreach (string file in files)
                {
                    var tempFile = Path.GetTempFileName();
                    File.Copy(file, tempFile, true);

                    using (StreamReader sr = new StreamReader(tempFile))
                    {
                        string line = null;
                        var directoryName = Path.GetDirectoryName(file);
                        directoryName = Path.GetDirectoryName(directoryName);
                        directoryName = Path.GetFileNameWithoutExtension(directoryName);
                        DateTime.TryParseExact(directoryName, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.AllowLeadingWhite, out DateTime date);
                        //ID, Level(%), Voltage(mV), ChargeCurrent(mA), DischargeCurrent(mA), Temperature(C)
                        Dictionary<int, DateTime> lastTime = new Dictionary<int, DateTime>()
                        {
                            { 0, DateTime.MinValue },
                            { 1, DateTime.MinValue },
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

                            var time_str = splited[0].Substring(6, 15);
                            DateTime.TryParseExact(time_str, "HH:mm:ss.ffffff", CultureInfo.CurrentCulture, DateTimeStyles.AllowLeadingWhite, out DateTime time);
                            DateTime.TryParseExact($"{date.ToString("yyyy-MM-dd")} {time.ToString("HH:mm:ss.ffffff")}", "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.CurrentCulture, DateTimeStyles.AllowLeadingWhite, out DateTime date_time);


                            //var currentStatus = GetAGVStatusWithTime(date_time);
                            //var lastStatus = GetAGVStatusWithTime(lastTime[id]);

                            if (IsIntervalTooShort(date_time, lastTime[id]))
                            {
                                continue;
                            }
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
                    return datas[options.id].ToDictionary(dat => dat.Time, dat => new clsStatus
                    { value = dat.Level, status = GetAGVStatusWithTime(dat.Time, dat) });
                }

                if (options.item == QUERY_ITEM.Voltage.ToString())
                {
                    return datas[options.id].ToDictionary(dat => dat.Time, dat => new clsStatus
                    {
                        value = dat.Voltage,
                        status = GetAGVStatusWithTime(dat.Time, dat)
                    });
                }

                if (options.item == QUERY_ITEM.Charge_current.ToString())
                {
                    return datas[options.id].ToDictionary(dat => dat.Time, dat => new clsStatus
                    {
                        value = dat.ChargeCurrent,
                        status = GetAGVStatusWithTime(dat.Time, dat)
                    });
                }

                if (options.item == QUERY_ITEM.Discharge_current.ToString())
                {
                    return datas[options.id].ToDictionary(dat => dat.Time, dat => new clsStatus
                    {
                        value = dat.DischargeCurrent,
                        status = GetAGVStatusWithTime(dat.Time, dat)
                    });
                }
                else
                    return new Dictionary<DateTime, clsStatus>();

            });
        }

        private bool IsIntervalTooShort(DateTime current_dateTime, DateTime last_dateTime)
        {
            double threshold_seconds = 60;
            var query_time_sumup = (options.timedt_range[1] - options.timedt_range[0]).TotalSeconds;
            if (query_time_sumup <= TimeSpan.FromDays(1).TotalSeconds)
                threshold_seconds = TimeSpan.FromMinutes(1).TotalSeconds;

            else if (query_time_sumup > TimeSpan.FromDays(1).TotalSeconds && query_time_sumup < TimeSpan.FromDays(7).TotalSeconds)
                threshold_seconds = TimeSpan.FromMinutes(30).TotalSeconds;
            else
                threshold_seconds = TimeSpan.FromHours(1).TotalSeconds;

            return (current_dateTime - last_dateTime).TotalSeconds < threshold_seconds;
        }

        private SUB_STATUS GetAGVStatusWithTime(DateTime time, clsBatteryInfo dat = null)
        {
            if (dat != null && dat.ChargeCurrent > 0 && dat.DischargeCurrent == 0)
            {
                return SUB_STATUS.Charging;
            }
            return DBhelper.Query.QueryStatusWithTime(time);
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
                    if (Path.GetFileNameWithoutExtension(file).Equals("batteryLog"))
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
