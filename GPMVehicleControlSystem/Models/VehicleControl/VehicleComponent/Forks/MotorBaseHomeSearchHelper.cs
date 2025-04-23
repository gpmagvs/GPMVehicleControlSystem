using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Vehicle = GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using NLog;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    public abstract class MotorBaseHomeSearchHelper
    {
        private enum SEARCH_DIRECTION
        {
            DOWN,
            UP
        }

        protected readonly Vehicle vehicle;
        protected readonly clsDOModule DOModule;
        protected readonly clsDIModule DIModule;
        protected Logger logger = LogManager.GetCurrentClassLogger();
        public MotorBaseHomeSearchHelper(Vehicle vehicle)
        {
            this.vehicle = vehicle;
            DOModule = vehicle.WagoDO;
            DIModule = vehicle.WagoDI;
            logger.Info($"Instance Created. AGV Type : {vehicle.Parameters.AgvType}");
        }

        protected bool hasCargoMounted
        {
            get
            {
                return vehicle.CargoStateStorer.HasAnyCargoOnAGV(false);
            }
        }

        protected abstract FORK_LOCATIONS CurrentLocation { get; }

        protected abstract double CurrentActualPosition { get; }

        public async Task<(bool done, AlarmCodes alarm_code)> StartSearchAsync()
        {
            bool _isInitializeDone = false;
            logger.Info("Start Search Home");
            try
            {
                while (!_isInitializeDone)
                {

                    SEARCH_DIRECTION search_direction = DetermineSearchDirection();
                    ForkStartSearch(search_direction, hasCargoMounted);

                    (bool reachHome, bool isReachLimitSensor, bool isReachUpLimit) = await WaitForkReachHome(jumpOutIfReachLimitSensor: true);
                    if (!isReachLimitSensor)
                    {
                        await WaitForkLeaveHome();
                    }
                    else
                    {
                        await BypassLimitSensor();
                        await Task.Delay(1000);
                        if (isReachUpLimit)
                            continue;
                    }

                    await UpSearchAndWaitLeaveHome(hasCargoMounted);
                    await Task.Delay(1000);
                    await ShortMoveToFindHome();
                    await Task.Delay(200);

                    if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        logger.Fatal($"Status Down Fork initialize action interupted.!");
                        break;
                    }
                    _isInitializeDone = CurrentLocation == FORK_LOCATIONS.HOME && Math.Abs(CurrentActualPosition - 0) < 0.01;
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                logger.Fatal($"[ForkInitialize] FAIL. {ex.Message}");
                return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
            }
            finally
            {
                await NoBypassLimitSensor();
            }

            return (false, AlarmCodes.Fork_Arm_Action_Error);
        }


        private SEARCH_DIRECTION DetermineSearchDirection()
        {
            if (CurrentLocation == FORK_LOCATIONS.HOME || CurrentLocation == FORK_LOCATIONS.UNKNOWN || CurrentLocation == FORK_LOCATIONS.UP_POSE)
                return SEARCH_DIRECTION.DOWN;
            else
                return SEARCH_DIRECTION.UP;
        }

        async Task ForkStartSearch(SEARCH_DIRECTION searchDriection, bool hasCargo)
        {
            if (searchDriection == SEARCH_DIRECTION.DOWN)
                await DownSearchAsync(1.0);
            else
                await UpSearchAsync(1.0);
        }



        protected abstract Task<(bool confirm, string message)> UpSearchAsync(double speed = 0.1);
        protected abstract Task<(bool confirm, string message)> DownSearchAsync(double speed = 0.1);
        protected abstract Task<(bool confirm, string message)> StopAsync();
        async Task<(bool confirm, string message)> PositionInit()
        {
            await Task.Delay(300);
            throw new NotImplementedException();
        }
        protected abstract Task<(bool confirm, string message)> SendChangePoseCmd(double pose, double speed = 0.1);
        async Task WaitForkLeaveHome()
        {
            while (CurrentLocation == FORK_LOCATIONS.HOME)
            {
                if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                {
                    throw new Exception("AGV Status Down");
                }
                await Task.Delay(1);
            }

            await StopAsync();
            await Task.Delay(500);
        }
        async Task<(bool reachHome, bool isReachLimitSensor, bool isReachUpHardwareLimit)> WaitForkReachHome(bool jumpOutIfReachLimitSensor)
        {
            while (CurrentLocation != FORK_LOCATIONS.HOME)
            {
                await Task.Delay(1);
                if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                {
                    throw new Exception("AGV Status Down");
                }
                if (jumpOutIfReachLimitSensor && (CurrentLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT || CurrentLocation == FORK_LOCATIONS.UP_HARDWARE_LIMIT))
                {
                    return (false, true, CurrentLocation == FORK_LOCATIONS.UP_HARDWARE_LIMIT);
                }
            }
            return (true, false, false);
        }
        async Task UpSearchAndWaitLeaveHome(bool hasCargo)
        {
            await StopAsync();
            await Task.Delay(100);
            await PositionInit();
            await Task.Delay(500);
            ForkStartSearch(SEARCH_DIRECTION.UP, hasCargo);

            await WaitForkReachHome(jumpOutIfReachLimitSensor: false);
            await WaitForkLeaveHome();
            _ = Task.Factory.StartNew(async () =>
            {
                await Task.Delay(500);
                await NoBypassLimitSensor();
            });
            await Task.Delay(hasCargo ? 1200 : 500);
            await SendChangePoseCmd(CurrentActualPosition + 0.3, 0.3);
        }
        async Task ShortMoveToFindHome()
        {
            while (CurrentLocation != FORK_LOCATIONS.HOME)
            {
                if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                {
                    throw new Exception("AGV Status Down");
                }
                double _pose = CurrentActualPosition - 0.1;
                await SendChangePoseCmd(_pose, 1);
                await Task.Delay(1000);
            }
            await PositionInit();
        }
        protected virtual async Task NoBypassLimitSensor()
        {
            await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
        }
        protected virtual async Task BypassLimitSensor()
        {
            await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
        }
    }
}
