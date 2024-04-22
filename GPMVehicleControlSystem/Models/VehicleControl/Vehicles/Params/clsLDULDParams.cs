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
        public bool LeaveWorkStationNeedSendRequestToAGVS { get; set; } = false;
        public int LeaveWorkStationRequestTimeout { get; set; } = 3;

        public bool BypassFrontLaserWhenEntryEQ { get; set; } = true;
    }
}
