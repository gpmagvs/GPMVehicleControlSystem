namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class InspectorAGVCarController : CarController
    {
        public InspectorAGVCarController()
        {
        }
        public InspectorAGVCarController(string IP, int Port) : base(IP, Port)
        {
        }

        public override string alarm_locate_in_name => "InspectorAGVCarController";
    }
}
