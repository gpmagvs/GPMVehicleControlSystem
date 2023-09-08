using System.Globalization;

namespace GPMVehicleControlSystem.Models.Log
{
    public class clsLogQueryResults : clsLogQueryOptions
    {
        public List<clsLogQuResultDto> LogMessageList { get; set; } = new List<clsLogQuResultDto>();
        public int TotalCount { get; set; } = 0;
    }

    public class clsLogQuResultDto
    {
        public string Time { get; set; } = "";
        internal DateTime TimeDT
        {
            get
            {
                if (DateTime.TryParseExact(Time, "yyyy/MM/dd HH:mm:ss.ffff", CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime time))
                    return time;
                return new DateTime(2999, 1, 1);
            }
        }
        public string Message { get; set; } = "";
    }
}
