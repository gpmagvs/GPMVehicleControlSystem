using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsSick : CarComponent
    {
        public override COMPOENT_NAME component_name { get; } = COMPOENT_NAME.SICK;
        public new LocalizationControllerResultMessage0502 Data => StateData == null ? new LocalizationControllerResultMessage0502() : (LocalizationControllerResultMessage0502)StateData;
        public double MapSocre => Data.map_match_status / 100.0;
        public double HeadingAngle => Data.heading / 1000.0;

        public override string alarm_locate_in_name => component_name.ToString();

        public override void CheckStateDataContent()
        {
            //  1 byte LocalizationStatus [0...100, 10: OK, 20: Warning, 30: Not localized, 40: System error]
            if (Data.loc_status != 10)
                Current_Warning_Code = AlarmCodes.Map_Recognition_Rate_Too_Low;
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }
        }
    }
}
