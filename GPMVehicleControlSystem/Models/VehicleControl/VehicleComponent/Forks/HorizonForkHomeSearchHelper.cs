using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    public class HorizonForkHomeSearchHelper : MotorBaseHomeSearchHelper
    {
        public HorizonForkHomeSearchHelper(Vehicle vehicle) : base(vehicle)
        {
        }

        protected override clsForkLifter.FORK_LOCATIONS CurrentLocation
        {
            get
            {

                if (DIModule.GetState(DI_ITEM.Fork_Home_Pose))
                    return FORK_LOCATIONS.HOME;

                else if (!DIModule.GetState(DI_ITEM.Fork_Extend_Exist_Sensor))
                    return FORK_LOCATIONS.UP_HARDWARE_LIMIT;

                else if (!DIModule.GetState(DI_ITEM.Fork_Short_Exist_Sensor))
                    return FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;

                else return FORK_LOCATIONS.UNKNOWN;
            }
        }

        protected override double CurrentActualPosition => throw new NotImplementedException();

        protected override Task<(bool confirm, string message)> DownSearchAsync(double speed = 0.1)
        {
            throw new NotImplementedException();
        }

        protected override Task<(bool confirm, string message)> PositionInit()
        {
            throw new NotImplementedException();
        }

        protected override Task<(bool confirm, string message)> SendChangePoseCmd(double pose, double speed = 0.1)
        {
            throw new NotImplementedException();
        }

        protected override Task<(bool confirm, string message)> StopAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task<(bool confirm, string message)> UpSearchAsync(double speed = 0.1)
        {
            throw new NotImplementedException();
        }
    }
}
