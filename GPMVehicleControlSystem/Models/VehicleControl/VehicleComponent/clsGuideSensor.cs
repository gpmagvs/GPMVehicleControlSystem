using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsGuideSensor : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.GUID_SENSOR;

        public override void CheckStateDataContent()
        {
        }
    }
}
