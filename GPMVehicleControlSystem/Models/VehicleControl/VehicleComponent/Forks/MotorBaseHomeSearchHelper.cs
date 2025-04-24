using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Vehicle = GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using NLog;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;
using System.Threading.Tasks;

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
        protected virtual double speedWhenSearchStartWithoutCargo { get; set; } = 0.5;
        public readonly string name;


        public MotorBaseHomeSearchHelper(Vehicle vehicle, string name)
        {
            this.vehicle = vehicle;
            this.name = name;
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

        private SEARCH_DIRECTION currentSearchDirection;

        public async Task<(bool done, AlarmCodes alarm_code)> StartSearchAsync()
        {
            bool _isInitializeDone = false;
            logger.Info("Start Search Home");
            try
            {
                while (!_isInitializeDone)
                {
                    if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        logger.Fatal($"Status Down Fork initialize action interupted.!");
                        break;
                    }
                    currentSearchDirection = DetermineSearchDirection();
                    UpdateInitMessge($"{currentSearchDirection} Search...");

                    ForkStartSearch(currentSearchDirection, hasCargoMounted);


                    (bool reachHome, bool isReachLimitSensor, bool isReachUpLimit) = await WaitForkReachHome(jumpOutIfReachLimitSensor: true, stopWhenReachHome: true);

                    if (reachHome || isReachLimitSensor)
                    {
                        if (isReachUpLimit)
                        {
                            logger.Info("觸發極限位置為上極限，continue search start.");
                            continue;
                        }
                        if (isReachLimitSensor)
                        {
                            UpdateInitMessge($"Reach limit Sensor. Bypass Sensor.");
                            logger.Info("觸發極限位置，BypassLimitSensor");
                            await BypassLimitSensor();
                            await Task.Delay(200);
                        }
                        if (reachHome)
                            UpdateInitMessge($"Reach Home!");
                        await Task.Delay(1000);
                        UpdateInitMessge($"Up Search Untill Leave Home");
                        logger.Info("向上搜尋直到離開原點 UpSearchAndWaitLeaveHome");
                        WaitLeaveLimitSensorAndNoBypassLimitSensor();
                        await UpSearchAndWaitLeaveHome(hasCargoMounted);
                        await Task.Delay(1000);
                        logger.Info("向下吋動搜尋直到抵達原點 ShortMoveToFindHome");
                        UpdateInitMessge($"Finding Home Pose..{CurrentActualPosition} cm");
                        await ShortMoveToFindHome();
                        await Task.Delay(200);
                        _isInitializeDone = CurrentLocation == FORK_LOCATIONS.HOME && Math.Abs(CurrentActualPosition - 0) < 0.01;
                    }
                    else
                    {
                        continue;
                    }
                }
                return (_isInitializeDone, _isInitializeDone ? AlarmCodes.None : AlarmCodes.Fork_Arm_Action_Timeout);

            }
            catch (Exception ex)
            {
                vehicle.SendNotifyierToFrontend($"牙叉尋原點失敗.{ex.Message}.{ex.StackTrace}");
                logger.Fatal($"[ForkInitialize] FAIL. {ex.Message}");
                return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
            }
            finally
            {
                await NoBypassLimitSensor();
            }
        }

        private void UpdateInitMessge(string msg)
        {
            vehicle.InitializingStatusText = $"[{name}]原點復歸中...{msg}";
        }
        private async Task WaitLeaveLimitSensorAndNoBypassLimitSensor()
        {
            while (CurrentLocation == FORK_LOCATIONS.UP_HARDWARE_LIMIT || CurrentLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
            {
                await Task.Delay(10);
            }
            NoBypassLimitSensor();
        }

        private SEARCH_DIRECTION DetermineSearchDirection()
        {
            if (CurrentLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
                return SEARCH_DIRECTION.UP;
            else
                return SEARCH_DIRECTION.DOWN;
            //if (CurrentLocation == FORK_LOCATIONS.HOME || CurrentLocation == FORK_LOCATIONS.UNKNOWN || CurrentLocation == FORK_LOCATIONS.UP_POSE)
            //    return SEARCH_DIRECTION.DOWN;
            //else
            //    return SEARCH_DIRECTION.UP;
        }

        async Task ForkStartSearch(SEARCH_DIRECTION searchDriection, bool hasCargo)
        {

            speedWhenSearchStartWithoutCargo = hasCargo ? 0.5 : speedWhenSearchStartWithoutCargo;
            if (searchDriection == SEARCH_DIRECTION.DOWN)
                await DownSearchAsync(speedWhenSearchStartWithoutCargo);
            else
                await UpSearchAsync(speedWhenSearchStartWithoutCargo);
        }



        protected abstract Task<(bool confirm, string message)> UpSearchAsync(double speed = 0.1);
        protected abstract Task<(bool confirm, string message)> DownSearchAsync(double speed = 0.1);
        protected abstract Task<(bool confirm, string message)> StopAsync();
        protected abstract Task<(bool confirm, string message)> PositionInit();
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
        async Task<(bool reachHome, bool isReachLimitSensor, bool isReachUpHardwareLimit)> WaitForkReachHome(bool jumpOutIfReachLimitSensor, bool stopWhenReachHome)
        {
            try
            {

                bool _isSlowDown = false;
                while (CurrentLocation != FORK_LOCATIONS.HOME)
                {
                    await Task.Delay(1);

                    if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        throw new Exception("AGV Status Down");
                    }
                    if (jumpOutIfReachLimitSensor && (CurrentLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT || CurrentLocation == FORK_LOCATIONS.UP_HARDWARE_LIMIT))
                    {
                        await StopAsync();
                        logger.Info("WaitForkReachHome->Reach Limit Sensor . Stopped");
                        return (false, true, CurrentLocation == FORK_LOCATIONS.UP_HARDWARE_LIMIT);
                    }
                }
                if (stopWhenReachHome)
                {
                    logger.Info("WaitForkReachHome->Reach Home . Stopped");
                    await StopAsync();
                }
                logger.Info("WaitForkReachHome->Reach Home");
                return (true, false, false);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
            }
        }
        async Task UpSearchAndWaitLeaveHome(bool hasCargo)
        {
            await StopAsync();
            await Task.Delay(500);
            ForkStartSearch(SEARCH_DIRECTION.UP, hasCargo);
            await WaitForkReachHome(jumpOutIfReachLimitSensor: false, stopWhenReachHome: false);
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
            await PositionInit();
            await Task.Delay(1000);
            while (CurrentLocation != FORK_LOCATIONS.HOME)
            {
                if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                {
                    throw new Exception("AGV Status Down");
                }
                double _pose = CurrentActualPosition - 0.1;
                await SendChangePoseCmd(_pose, 1);
                UpdateInitMessge($"Finding Home Pose..{CurrentActualPosition} cm");
                await Task.Delay(1000);
            }
            UpdateInitMessge($"Reach Home Pose!");
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
