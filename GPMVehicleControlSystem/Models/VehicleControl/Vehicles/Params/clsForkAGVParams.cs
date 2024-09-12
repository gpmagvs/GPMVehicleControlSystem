using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.ForkAGV;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsForkAGVParams
    {
        public bool ForkLifer_Enable { get; set; } = true;
        /// <summary>
        /// 牙叉伸出後總車長(cm)
        /// </summary>
        public double VehielLengthWitchForkArmExtend { get; set; } = 160.0;
        public double UplimitPose { get; set; } = 35;
        public double DownlimitPose { get; set; } = 0;
        public double UplimitPoseSettingMax { get; set; } = 35;
        public double StandbyPose { get; set; } = 10;
        public bool HomePoseUseStandyPose { get; set; } = true;

        /// <summary>
        /// 是否搭載PIN
        /// </summary>
        public bool IsPinMounted { get; set; } = true;
        public bool IsForkIsExtendable { get; set; } = true;
        public bool NoWaitForkArmFinishAndMoveOutInWorkStation { get; set; } = true;
        /// <summary>
        /// 退至二次定位點不等待就定位牙叉即開始回HOME
        /// </summary>
        public bool NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary { get; set; } = true;
        /// <summary>
        /// 開始從充電樁退出時就下降牙叉
        /// </summary>
        public bool NoWaitParkingFinishAndForkGoHomeWhenBackToSecondaryAtChargeStation { get; set; } = false;
        public FORK_SAFE_STRATEGY ForkSaftyStratrgy { get; set; } = FORK_SAFE_STRATEGY.UNDER_SAFTY_POSITION;
        public double SaftyPositionHeight { get; set; } = 20;
        public clsForkInit InitParams = new clsForkInit();

        public clsForkSpeedParams ManualModeOperationSpeed { get; set; } = new clsForkSpeedParams();
        public clsForkSpeedParams AutoModeOperationSpeed { get; set; } = new clsForkSpeedParams();
        [JsonConverter(typeof(StringEnumConverter))]
        public IO_CONEECTION_POINT_TYPE ObsSensorPointType { get; set; } = IO_CONEECTION_POINT_TYPE.A;
    }
    public class clsForkInit
    {
        /// <summary>
        /// 初始化時.車上有貨物的Fork移動速度
        /// </summary>
        public double ForkInitActionSpeedWithCargo { get; set; } = 1.0;
        /// <summary>
        /// 初始化時.車上沒有貨物的Fork移動速度
        /// </summary>
        public double ForkInitActionSpeedWithoutCargo { get; set; } = 0.5;

    }

    public class clsForkSpeedParams
    {
        /// <summary>
        /// 0~1 速度比例
        /// </summary>
        public double MoveToPoseSpeed { get; set; } = 0.5;
    }
}
