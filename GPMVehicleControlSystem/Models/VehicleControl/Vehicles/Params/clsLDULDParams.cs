using AGVSystemCommonNet6.Alarm;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsLDULDParams
    {
        public bool LsrObstacleDetectionEnable { get; set; } = false;
        public int LsrObsLaserModeNumber { get; set; } = 8;
        public ALARM_LEVEL LsrObsDetectedAlarmLevel { get; set; } = ALARM_LEVEL.WARNING;
    }
}
