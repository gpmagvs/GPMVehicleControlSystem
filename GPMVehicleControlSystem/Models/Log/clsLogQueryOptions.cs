namespace GPMVehicleControlSystem.Models.Log
{
    public class clsLogQueryOptions
    {
        public DateTime FromTime { get; set; }
        public DateTime ToTime { get; set; }

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
