using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsAdvanceParams
    {
        public bool ShutDownPCWhenLowBatteryLevel { get; set; } = false;
        public bool AutoInitAndOnlineWhenMoveWithCargo { get; set; } = false;
        public bool AutoInitAndOnlineWhenMoveWithoutCargo { get; set; } = false;

        public List<AlarmCodes> ForbidAutoInitialzeAlarmCodes { get; set; } = new();

        public bool IsAprilTagLocateSupport { get; set; } = false;
        /// <summary>
        /// 車控導航狀態訊息更新 Timeout 時間(秒)
        /// </summary>
        public double NavigationInfoUpdateTimeoutSec { get; set; } = 5.0;
    }
}
