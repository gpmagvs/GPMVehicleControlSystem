namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    /// <summary>
    /// 
    /// </summary>
    public partial class clsManualCheckCargoStatusParams
    {

        /// <summary>
        /// 檢查時機點列舉
        /// </summary>
        public enum CHECK_MOMENT
        {
            BEFORE_LOAD,
            AFTER_UNLOAD
        }

        /// <summary>
        /// 開關
        /// </summary>
        public bool Enabled { get; set; } = false;

        public List<CheckPointModel> CheckPoints { get; set; } = new List<CheckPointModel>();

    }

}
