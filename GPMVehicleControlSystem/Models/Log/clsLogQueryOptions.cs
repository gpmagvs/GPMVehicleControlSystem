using System.Globalization;

namespace GPMVehicleControlSystem.Models.Log
{
    public class clsLogQueryOptions
    {

        public string FromTimeStr { get; set; } = "";
        public string ToTimeStr { get; set; } = "";

        internal DateTime FromTime
        {
            get
            {
                if (DateTime.TryParseExact(FromTimeStr, "yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime time))
                {
                    return time;
                }
                else
                {
                    return new DateTime(2999, 1, 1);
                }
            }
        }
        internal DateTime ToTime
        {
            get
            {
                if (DateTime.TryParseExact(ToTimeStr, "yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime time))
                {
                    return time;
                }
                else
                {
                    return new DateTime(2999, 1, 1);
                }
            }
        }

        /// <summary>
        /// 顯示第幾頁
        /// </summary>
        public int Page { get; set; } = 1;
        /// <summary>
        /// 每一頁要顯示的個數
        /// </summary>
        public int NumberPerPage { get; set; } = 20;

        /// <summary>
        /// 特定字串搜尋
        /// </summary>
        public List<string> SpeficStrings { get; set; } = new List<string>();
    }
}
