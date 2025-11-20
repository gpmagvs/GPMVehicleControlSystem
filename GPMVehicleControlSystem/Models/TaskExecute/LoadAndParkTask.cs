using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    public class LoadAndParkTask : LoadTask
    {
        public LoadAndParkTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        protected override bool isParkingAfterLoad { get; set; } = true;
    }
}
