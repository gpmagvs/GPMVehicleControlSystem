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
    }
}
