using AGVSystemCommonNet6.Vehicle_Control;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsGuideSensor : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.GUID_SENSOR;

        public override string alarm_locate_in_name => component_name.ToString();

        public override void CheckStateDataContent()
        {
        }
    }
}
