using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    public class VertialForkHomeSearchHelper : MotorBaseHomeSearchHelper
    {
        ForkAGVController AGVC => vehicle.AGVC as ForkAGVController;

        protected override FORK_LOCATIONS CurrentLocation
        {
            get
            {

                if (DIModule.GetState(DI_ITEM.Vertical_Home_Pos))
                    return FORK_LOCATIONS.HOME;

                else if (!DIModule.GetState(DI_ITEM.Vertical_Up_Hardware_limit))
                    return FORK_LOCATIONS.UP_HARDWARE_LIMIT;

                else if (!DIModule.GetState(DI_ITEM.Vertical_Down_Hardware_limit))
                    return FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;

                else if (DIModule.GetState(DI_ITEM.Vertical_Up_Pose))
                    return FORK_LOCATIONS.UP_POSE;

                else if (DIModule.GetState(DI_ITEM.Vertical_Down_Pose))
                    return FORK_LOCATIONS.DOWN_POSE;

                else
                    return FORK_LOCATIONS.UNKNOWN;
            }
        }

        protected override double CurrentActualPosition => Math.Round(vehicle.ForkLifter.Driver.CurrentPosition, 3);
        protected override double speedWhenSearchStartWithoutCargo { get; set; } = 1;
        public VertialForkHomeSearchHelper(Vehicle vehicle, string name) : base(vehicle, name)
        {
        }

        protected override async Task<(bool confirm, string message)> UpSearchAsync(double speed = 0.1)
        {
            logger.Info($"開始向上搜尋-速度 {speed} ");
            return await AGVC.verticalActionService.UpSearch(speed);
        }
        protected override async Task<(bool confirm, string message)> DownSearchAsync(double speed = 0.1)
        {
            logger.Info($"開始向下搜尋-速度 {speed} ");
            return await AGVC.verticalActionService.DownSearch(speed);
        }

        protected override async Task<(bool confirm, string message)> SendChangePoseCmd(double pose, double speed = 0.1)
        {
            return await AGVC.verticalActionService.Pose(pose, speed, true);
        }

        protected override async Task<(bool confirm, string message)> StopAsync()
        {
            return await AGVC.verticalActionService.Stop();
        }

        protected override async Task<(bool confirm, string message)> PositionInit()
        {
            return await AGVC.verticalActionService.Init();

        }
    }
}
