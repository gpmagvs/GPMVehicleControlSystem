using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;

namespace GPMVehicleControlSystem.Models.RDTEST
{
    public abstract class RDTesterAbstract
    {
        public enum TEST_STATE
        {
            RUNNING,
            IDLE,
        }
        public CancellationTokenSource testCancelCts = new CancellationTokenSource();
        protected Vehicle AGV => StaStored.CurrentVechicle;
        protected TEST_STATE test_state = TEST_STATE.IDLE;
        protected RDTesterAbstract() { }
        public abstract void Start();
        public void Stop()
        {
            testCancelCts.Cancel();
        }
    }
}
