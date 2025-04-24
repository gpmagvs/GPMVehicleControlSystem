
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    /// <summary>
    /// 牙叉模組但伸縮方式是用馬達驅動的方式
    /// </summary>
    public class clsForkLifterWithDriverBaseExtener : clsForkLifter
    {
        public bool IsHorizonForkInitialized { get; internal set; } = false;

        private clsForkHorizon HorizonArmConfigs => forkAGV.Parameters.ForkAGV.HorizonArmConfigs;
        private ForkActionServiceBase horizonForkService => fork_ros_controller.HorizonActionService;
        public clsForkLifterWithDriverBaseExtener(ForkAGV forkAGV) : base(forkAGV)
        {
            logger.Info("Fork Lifter with driver base extener instance created(牙叉伸縮使用馬達驅動方式)");
        }

        public async Task<(bool done, AlarmCodes alarm_code)> HorizonForkInitialize(double InitForkSpeed = 0.5)
        {
            HorizonForkHomeSearchHelper horizonForkHomeSearchHelper = new HorizonForkHomeSearchHelper(forkAGV, "Horizon");
            return await horizonForkHomeSearchHelper.StartSearchAsync();
        }

        public override async Task<bool> ForkARMStop()
        {
            (bool confirm, string message) = await fork_ros_controller.HorizonActionService.Stop();
            return confirm;
        }

        public override async Task<(bool confirm, AlarmCodes)> ForkExtendOutAsync(bool wait_reach_end = true)
        {
            double pose = HorizonArmConfigs.ExtendPose;
            (bool success, string message) = await horizonForkService.Pose(pose, 1);
            if (success)
                return (true, AlarmCodes.None);
            else
                return (false, AlarmCodes.Action_Timeout);
        }

        public override async Task<(bool confirm, string message)> ForkShortenInAsync(bool wait_reach_home = true)
        {
            double pose = HorizonArmConfigs.ShortenPose;
            return await horizonForkService.Pose(pose, 1);
        }
    }
}
