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
        public bool IsPinDisabledTemptary { get; set; } = false;
        public bool IsForkIsExtendable { get; set; } = true;
        public bool IsHorizonExtendDisabledTemptary { get; set; } = false;
        public bool NoWaitForkArmFinishAndMoveOutInWorkStation { get; set; } = true;
        /// <summary>
        /// 退至二次定位點不等待就定位牙叉即開始回HOME
        /// </summary>
        public bool NoWaitParkingFinishAndForkGoHomeWhenBackToSecondary { get; set; } = true;
        /// <summary>
        /// 開始從充電樁退出時就下降牙叉
        /// </summary>
        public bool NoWaitParkingFinishAndForkGoHomeWhenBackToSecondaryAtChargeStation { get; set; } = false;

        public bool TriggerCstReaderWhenUnloadBackToEntryPointAndReachTag { get; set; } = false;

        /// <summary>
        /// 需交握時，在 AGV_VALID 訊號 ON 起後就開始牙叉動作
        /// </summary>
        public bool ForkStartActionEarlyWhenVALIDOuputON { get; set; } = false;
        /// <summary>
        /// 浮動牙叉是否會鎖住伸縮牙叉
        /// </summary>
        public bool IsFloatingPinLockHorizonForkArm { get; set; } = false;
        public FORK_SAFE_STRATEGY ForkSaftyStratrgy { get; set; } = FORK_SAFE_STRATEGY.UNDER_SAFTY_POSITION;
        public double SaftyPositionHeight { get; set; } = 20;
        public clsForkInit InitParams = new clsForkInit();

        public clsForkSpeedParams ManualModeOperationSpeed { get; set; } = new clsForkSpeedParams();
        public clsForkSpeedParams AutoModeOperationSpeed { get; set; } = new clsForkSpeedParams();

        /// <summary>
        /// 水平伸縮牙叉參數
        /// </summary>
        public clsForkHorizon HorizonArmConfigs { get; set; } = new clsForkHorizon();

        [JsonConverter(typeof(StringEnumConverter))]
        public IO_CONTACT_TYPE ObsSensorPointType { get; set; } = IO_CONTACT_TYPE.A;
        public double DownSearchSpeedWhenInitialize { get; set; } = 0.8;
        public double START_DONW_STEP_FIND_HOME_POSE { get; set; } = 0.2;

        public List<double> NonRotatableWhenLiftingTags { get; set; } = new List<double>();

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

    public class clsForkHorizon
    {
        public double ExtendPose { get; set; } = 4999;
        public double ShortenPose { get; set; } = 1;
        public bool ExtendWhenStartMoveToPort { get; set; } = false;
    }
}
