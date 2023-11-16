using AGVSystemCommonNet6.Alarm;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsObstacleDetection
    {

        public enum FRONTEND_OBS_DETECTION_METHOD
        {
            BEGIN_ACTION,
            CONTINUE,
        }

        public bool Enable_Load { get; set; } = false;
        public bool Enable_UnLoad { get; set; } = false;
        /// <summary>
        /// 偵測秒數
        /// </summary>
        public int Duration { get; set; } = 4;

        public ALARM_LEVEL AlarmLevelWhenTrigger { get; set; } = ALARM_LEVEL.WARNING;
        public FRONTEND_OBS_DETECTION_METHOD Detection_Method { get; set; } = FRONTEND_OBS_DETECTION_METHOD.BEGIN_ACTION;
    }
}
