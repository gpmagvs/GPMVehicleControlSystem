using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    public class HorizonForkHomeSearchHelper : MotorBaseHomeSearchHelper
    {
        ForkAGVController AGVC => vehicle.AGVC as ForkAGVController;

        public HorizonForkHomeSearchHelper(Vehicle vehicle, string name) : base(vehicle, name)
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

        protected override bool IsHomePoseSensorOn => vehicle.WagoDI.GetState(DI_ITEM.Fork_Home_Pose);

        protected override bool IsDownLimitSensorOn => !vehicle.WagoDI.GetState(DI_ITEM.Fork_Short_Exist_Sensor);

        protected override bool IsUpLimitSensorOn => !vehicle.WagoDI.GetState(DI_ITEM.Fork_Extend_Exist_Sensor);

        protected override bool IsUnderPressingSensorOn => !vehicle.WagoDI.GetState(DI_ITEM.Fork_Under_Pressing_Sensor);

        protected override async Task<(bool confirm, string message)> DownSearchAsync(double speed = 0.1)
        {
            return await AGVC.HorizonActionService.DownSearch(speed);
        }

        protected override async Task<(bool confirm, string message)> PositionInit()
        {
            return await AGVC.HorizonActionService.Init();
        }

        protected override async Task<(bool confirm, string message)> SendChangePoseCmd(double pose, double speed = 0.1)
        {
            return await AGVC.HorizonActionService.Pose(pose, speed);
        }

        protected override async Task<(bool confirm, string message)> StopAsync()
        {
            return await AGVC.HorizonActionService.Stop();
        }

        protected override async Task<(bool confirm, string message)> UpSearchAsync(double speed = 0.1)
        {
            return await AGVC.HorizonActionService.UpSearch(speed);
        }
    }
}
