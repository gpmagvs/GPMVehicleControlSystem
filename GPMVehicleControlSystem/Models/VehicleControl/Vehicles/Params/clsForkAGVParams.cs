using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.ForkAGV;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsForkAGVParams
    {
        /// <summary>
        /// 牙叉伸出後總車長(cm)
        /// </summary>
        public double VehielLengthWitchForkArmExtend { get; set; } = 160.0;
        public double UplimitPose { get; set; } = 35;
        public double DownlimitPose { get; set; } = 0;
        public bool NoWaitForkArmFinishAndMoveOutInWorkStation { get; set; } = true;
        /// <summary>
        /// 退至二次定位點不等待就定位牙叉即開始回HOME
        /// </summary>
        public bool NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary { get; set; } = true;
        public bool NoWaitParkingFinishAndForkGoHomeWhenBackToSecondaryAtChargeStation { get; set; } = true;
        public FORK_SAFE_STRATEGY ForkSaftyStratrgy { get; set; } = FORK_SAFE_STRATEGY.UNDER_SAFTY_POSITION;
        public double SaftyPositionHeight { get; set; } = 20;
    }
}
