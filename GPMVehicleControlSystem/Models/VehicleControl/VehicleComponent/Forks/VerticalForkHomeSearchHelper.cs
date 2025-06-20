using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    public class VerticalForkHomeSearchHelper : MotorBaseHomeSearchHelper
    {
        ForkAGVController AGVC => vehicle.AGVC as ForkAGVController;

        protected override FORK_LOCATIONS CurrentLocation
        {
            get
            {


                if (!DIModule.GetState(DI_ITEM.Vertical_Up_Hardware_limit))
                    return FORK_LOCATIONS.UP_HARDWARE_LIMIT;

                else if (!DIModule.GetState(DI_ITEM.Vertical_Down_Hardware_limit))
                    return FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;

                else if (DIModule.GetState(DI_ITEM.Vertical_Home_Pos))
                    return FORK_LOCATIONS.HOME;

                else if (DIModule.GetState(DI_ITEM.Vertical_Up_Pose))
                    return FORK_LOCATIONS.UP_POSE;

                else if (DIModule.GetState(DI_ITEM.Vertical_Down_Pose))
                    return FORK_LOCATIONS.DOWN_POSE;

                else
                    return FORK_LOCATIONS.UNKNOWN;
            }
        }

        protected override double CurrentActualPosition => Math.Round(AGVC.verticalActionService.CurrentPosition, 3);
        protected override double speedWhenSearchStartWithoutCargo { get; set; } = 1;

        protected override bool IsHomePoseSensorOn => vehicle.WagoDI.GetState(DI_ITEM.Vertical_Home_Pos);

        protected override bool IsDownLimitSensorOn => !vehicle.WagoDI.GetState(DI_ITEM.Vertical_Down_Hardware_limit);

        protected override bool IsUpLimitSensorOn => !vehicle.WagoDI.GetState(DI_ITEM.Vertical_Up_Hardware_limit);

        protected override bool IsUnderPressingSensorOn
        {
            get
            {
                bool isNotDefine = !vehicle.WagoDI.VCSInputs.Any(inp => inp.Input == DI_ITEM.Fork_Under_Pressing_Sensor);
                if (isNotDefine)
                    return false;
                return !vehicle.WagoDI.GetState(DI_ITEM.Fork_Under_Pressing_Sensor);
            }
        }

        public VerticalForkHomeSearchHelper(Vehicle vehicle, string name) : base(vehicle, name)
        {
        }

        protected override async Task<(bool confirm, string message)> UpSearchAsync(double speed = 0.1)
        {
            logger.Info($"開始向上搜尋-速度 {speed} ");
            return await AGVC.verticalActionService.UpSearch(speed, startActionInvoke: false);
        }
        protected override async Task<(bool confirm, string message)> DownSearchAsync(double speed = 0.1)
        {
            logger.Info($"開始向下搜尋-速度 {speed} ");
            return await AGVC.verticalActionService.DownSearch(speed);
        }

        protected override async Task<(bool confirm, string message)> SendChangePoseCmd(double pose, double speed = 0.1)
        {
            return await AGVC.verticalActionService.Pose(pose, speed, true, startActionInvoke: false);
        }

        protected override async Task<(bool confirm, string message)> StopAsync()
        {
            var stopCmdResult = await vehicle.ForkLifter.ForkStopAsync(waitSpeedZero: true);
            //var stopCmdResult = AGVC.verticalActionService.Stop();
            if (!stopCmdResult.confirm)
                return stopCmdResult;
            return (true, "停止完成");
        }

        protected override async Task<(bool confirm, string message)> PositionInit()
        {
            var initCmdResult = await AGVC.verticalActionService.Init();
            return initCmdResult;
        }
    }
}
