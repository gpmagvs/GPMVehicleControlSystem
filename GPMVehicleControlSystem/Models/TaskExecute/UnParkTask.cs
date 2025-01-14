using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    public class UnParkTask : DischargeTask
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Unpark;

        public UnParkTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }
    }
}
