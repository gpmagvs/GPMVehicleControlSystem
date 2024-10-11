namespace GPMVehicleControlSystem.Tools.DiskUsage
{
    public class DiskUsageState
    {
        /// <summary>
        /// 硬碟/裝置名稱
        /// </summary>
        public string Name { get; internal set; } = "";

        public string DriverType { get; internal set; } = "";
        /// <summary>
        /// 總容量
        /// </summary>
        public double TotalSizeOfDriver { get; internal set; } = 0;
        /// <summary>
        /// 剩餘可用容量
        /// </summary>
        public double TotalAvailableSpace { get; internal set; } = 0;
        /// <summary>
        /// 已使用量
        /// </summary>
        public double Used => TotalSizeOfDriver - TotalAvailableSpace;

    }
}
