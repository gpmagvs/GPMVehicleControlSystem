using AGVSystemCommonNet6.Alarm;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsLDULDParams
    {
        public bool LsrObstacleDetectionEnable { get; set; } = false;
        public int LsrObsLaserModeNumber { get; set; } = 8;
        public ALARM_LEVEL LsrObsDetectedAlarmLevel { get; set; } = ALARM_LEVEL.WARNING;
        /// <summary>
        /// 從設備站點退出至二次定位點時須詢問派車系統
        /// </summary>
        public bool LeaveWorkStationNeedSendRequestToAGVS { get; set; } = true;
        public int LeaveWorkStationRequestTimeout { get; set; } = 300;

        public bool BypassFrontLaserWhenEntryEQ { get; set; } = true;

        public int MoveActionTimeoutInSec { get; set; } = 60;

        /// <summary>
        /// 當取料時但騎框，是否可以讓人員介入確認，確認後恢富任務動作
        /// </summary>
        public bool MaunalCheckAndResumableWhenUnloadButCargoBias { get; set; } = false;

        /// <summary>
        /// 取貨時是否要檢查貨物 [類型](Tray or Rack match assigned type from AGVS)
        /// </summary>
        public bool CheckCargoTypeMatchWhenUnload { get; set; } = false;

        /// <summary>
        /// 取放貨AGV COMPT 訊號 ON之前是否要先向AGVS上報動作完成。
        /// </summary>
        public bool IsActionFinishReportBeforeCOMPTSignalON { get; set; } = false;
    }
}
