using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;

namespace GPMVehicleControlSystem.Models.RDTEST
{
    public enum TEST_STATE
    {
        RUNNING,
        IDLE,
    }
    public abstract class RDTesterAbstract
    {

        public CancellationTokenSource testCancelCts = new CancellationTokenSource();
        protected Vehicle AGV => StaStored.CurrentVechicle;
        public clsTestStateModel testing_data = new clsTestStateModel();
        protected RDTesterAbstract() { }
        public abstract void Start();
        public void Stop()
        {
            testCancelCts.Cancel();
        }
    }
}
