namespace GPMVehicleControlSystem.Models.RDTEST
{
    public class clsTestStateModel
    {
        public TEST_STATE state { get; set; } = TEST_STATE.IDLE;
        /// <summary>
        /// 歷經時間
        /// </summary>
        public int duration { get; set; } = 0;
    }


}
