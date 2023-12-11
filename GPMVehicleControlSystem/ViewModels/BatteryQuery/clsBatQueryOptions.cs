using System.Globalization;

namespace GPMVehicleControlSystem.ViewModels.BatteryQuery
{
    public class clsBatQueryOptions
    {
        public enum QUERY_ITEM
        {
            Voltage,
            Level,
            Charge_current,
            Discharge_current
        }
        public int id { get; set; }
        public string item { get; set; }
        public string[] time_range { get; set; } = new string[2];
        internal DateTime[] timedt_range
        {
            get
            {
                DateTime.TryParseExact(time_range[0], "yyyy/MM/dd", CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime start_time);
                DateTime.TryParseExact(time_range[1], "yyyy/MM/dd", CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime end_time);
                var _start_time = new DateTime(start_time.Year,start_time.Month,start_time.Day,0,0,0);
                var _end_time =time_range[0]==time_range[1] ? new DateTime(end_time.Year, end_time.Month, end_time.Day, 23, 59, 59):  new DateTime(end_time.Year, end_time.Month, end_time.Day,0,0,0);
                return new DateTime[2] { _start_time, _end_time };
            }
        }
    }
}
