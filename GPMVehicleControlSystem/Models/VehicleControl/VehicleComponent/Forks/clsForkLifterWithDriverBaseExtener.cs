
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

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

        public override FORK_ARM_LOCATIONS CurrentForkARMLocation
        {
            get
            {
                if (forkAGV.WagoDI.GetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Fork_Home_Pose) || IsForkArmShortLocationCorrect)
                    return FORK_ARM_LOCATIONS.HOME;
                else if (IsForkArmExtendLocationCorrect)
                    return FORK_ARM_LOCATIONS.END;
                else
                    return FORK_ARM_LOCATIONS.UNKNOWN;
            }
        }
        public override bool IsForkArmShortLocationCorrect
        {
            get
            {
                bool isAtHome = this.forkAGV.WagoDI.GetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Fork_Home_Pose);
                if (isAtHome)
                    return false;
                var driverState = (forkAGV.AGVC as ForkAGVController).HorizonActionService.driverState;
                return Math.Abs(driverState.position - forkAGV.Parameters.ForkAGV.HorizonArmConfigs.ShortenPose) <= 1;
            }
        }
        public override bool IsForkArmExtendLocationCorrect
        {
            get
            {
                bool isAtHome = this.forkAGV.WagoDI.GetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Fork_Home_Pose);
                if (isAtHome)
                    return false;
                var driverState = (forkAGV.AGVC as ForkAGVController).HorizonActionService.driverState;
                return Math.Abs(driverState.position - forkAGV.Parameters.ForkAGV.HorizonArmConfigs.ExtendPose) <= 1;
            }
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
            await horizonForkService.Stop();
            await Task.Delay(100);
            (bool confirm, string message) actionResult = await (horizonForkService as HorizonForkActionService).Extend();
            if (actionResult.confirm)
            {
                if (wait_reach_end)
                {
                    CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    (bool confirm, string message) waitReachLocateResult = await WaitHorizonForkPositionSensorStateMatch(isExtend: true, cts.Token);
                    if (waitReachLocateResult.confirm)
                        return (true, AlarmCodes.None);
                    else
                        return (false, AlarmCodes.Fork_Arm_Action_Timeout);
                }
                return (true, AlarmCodes.None);
            }
            else
                return (false, AlarmCodes.Fork_Arm_Action_Error);
        }


        public override async Task<(bool confirm, string message)> ForkShortenInAsync(bool wait_reach_home = true)
        {
            await horizonForkService.Stop();
            await Task.Delay(100);
            (bool confirm, string message) actionResult = await (horizonForkService as HorizonForkActionService).Retract();

            if (actionResult.confirm)
            {
                if (wait_reach_home)
                {
                    CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                    (bool confirm, string message) waitReachLocateResult = await WaitHorizonForkPositionSensorStateMatch(isExtend: false, cts.Token);
                    if (waitReachLocateResult.confirm)
                        return (true, "");
                    else
                        return (false, AlarmCodes.Fork_Arm_Action_Timeout.ToString());
                }
                return (true, "");
            }
            else
                return actionResult;
        }

        public override async Task<(bool success, string message)> ForkHorizonResetAsync()
        {
            (bool success, string message) result = await (horizonForkService as HorizonForkActionService).Reset();
            return result;
        }
        private async Task<(bool, string)> WaitHorizonForkPositionSensorStateMatch(bool isExtend, CancellationToken cancellationToken)
        {
            while (isExtend ? !isExtendSensorStateMatch() : !isShortenSensorStateMatch())
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return (false, "Timeout");
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            }

            return (true, "");

            bool isExtendSensorStateMatch()
            {
                return true;
            }

            bool isShortenSensorStateMatch()
            {
                bool isReachHomePose = forkAGV.WagoDI.GetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Fork_Home_Pose);
                return isReachHomePose;
            }
        }
    }
}
