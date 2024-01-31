using AGVSystemCommonNet6.Alarm;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsImpactDetectionParams
    {
        public Dictionary<string, string> Notes { get; set; } = new Dictionary<string, string>()
        {
            {"PitchErrorDetection","是否啟用AGV姿態異常檢測" },
            {"PitchErrorAlarmLevel","當AGV姿態異常檢出時的異常處理等級(0:Warning,1:Alarm)" }
        };
        public bool Enabled { get; set; } = false;
        public bool PitchErrorDetection { get; set; } = false;

        /// <summary>
        /// 碰撞偵測閥值(單位:G)
        /// </summary>
        public double ThresHold { get; set; } = 2;
        public double PitchErrorThresHold { get; set; } = 2;

        public ALARM_LEVEL PitchErrorAlarmLevel { get; set; } = ALARM_LEVEL.WARNING;

    }
}
