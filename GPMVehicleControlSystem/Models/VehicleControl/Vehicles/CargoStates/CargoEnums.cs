namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.CargoStates
{
    public enum CARGO_STATUS
    {
        /// <summary>
        /// 沒有貨物(通常為所有在席訊號皆ON)
        /// </summary>
        NO_CARGO,
        /// <summary>
        /// 有貨物且正常裝載(通常為所有在席訊號皆OFF)
        /// </summary>
        HAS_CARGO_NORMAL,
        /// <summary>
        /// 有貨物但傾斜(部分在席訊號OFF/部分ON)
        /// </summary>
        HAS_CARGO_BUT_BIAS,
        /// <summary>
        /// 無載物功能(如巡檢AGV)
        /// </summary>
        NO_CARGO_CARRARYING_CAPABILITY
    }
}
