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

        public enum SEARCH_STATUS
        {
            DETERMINE_SEARCH_DIRECTION,
            START_DOWN_SEARCH_FIND_HOME,
            DOWN_SEARCHING_FIND_HOME,
            START_UP_SEARCH_WAIT_LEAVE_HOME,
            START_UP_SEARCH_FIND_HOME,
            UP_SEARCHING_LEAVE_HOME,
            UP_SEARCHING_FIND_HOME,
            MOVE_STEP_TO_FIND_HOME,
            COMPLETE
        }

        protected readonly Vehicle vehicle;
        protected readonly clsDOModule DOModule;
        protected readonly clsDIModule DIModule;
        protected Logger logger = LogManager.GetCurrentClassLogger();
        protected virtual double speedWhenSearchStartWithoutCargo { get; set; } = 1;
        private double searchSpeed => hasCargoMounted ? 0.5 : speedWhenSearchStartWithoutCargo;

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

        protected abstract bool IsHomePoseSensorOn { get; }
        protected abstract bool IsDownLimitSensorOn { get; }
        protected abstract bool IsUpLimitSensorOn { get; }

        protected abstract double CurrentActualPosition { get; }

        private SEARCH_DIRECTION currentSearchDirection;

        public async Task<(bool done, AlarmCodes alarm_code)> StartSearchAsync()
        {
            bool _isInitializeDone = false;
            logger.Info("Start Search Home");
            try
            {
                SEARCH_STATUS _searchStatus = SEARCH_STATUS.DETERMINE_SEARCH_DIRECTION;
                while (!_isInitializeDone)
                {
                    if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        StopAsync();
                        logger.Fatal($"Status Down Fork initialize action interupted.!");
                        break;
                    }
                    await Task.Delay(1);
                    switch (_searchStatus)
                    {
                        case SEARCH_STATUS.DETERMINE_SEARCH_DIRECTION:
                            _searchStatus = GetSearchDirection();
                            UpdateInitMessge($"尋原點方向:{_searchStatus}");
                            break;
                        case SEARCH_STATUS.START_DOWN_SEARCH_FIND_HOME:
                            await PositionInit();
                            await Task.Delay(1000);
                            await DownSearchAsync(searchSpeed);
                            _searchStatus = SEARCH_STATUS.DOWN_SEARCHING_FIND_HOME;
                            break;
                        case SEARCH_STATUS.START_UP_SEARCH_WAIT_LEAVE_HOME:
                            await PositionInit();
                            await Task.Delay(1000);
                            await UpSearchAsync(0.5);
                            _searchStatus = SEARCH_STATUS.UP_SEARCHING_LEAVE_HOME;
                            break;
                        case SEARCH_STATUS.START_UP_SEARCH_FIND_HOME:
                            await PositionInit();
                            await Task.Delay(1000);
                            await UpSearchAsync(0.5);
                            _searchStatus = SEARCH_STATUS.UP_SEARCHING_FIND_HOME;
                            break;
                        case SEARCH_STATUS.DOWN_SEARCHING_FIND_HOME:
                            UpdateInitMessge($"向下搜尋原點...");
                            if (IsHomePoseSensorOn)
                            {
                                await StopAsync();
                                await Task.Delay(1000);
                                _searchStatus = SEARCH_STATUS.START_UP_SEARCH_WAIT_LEAVE_HOME;
                                break;
                            }
                            break;
                        case SEARCH_STATUS.UP_SEARCHING_LEAVE_HOME:
                            UpdateInitMessge($"等待下緣離開原點...");
                            if (!IsHomePoseSensorOn)
                            {
                                await StopAsync();
                                UpdateInitMessge($"等待下緣離開原點..StopAsync.");
                                await Task.Delay(1000);
                                await PositionInit();
                                UpdateInitMessge($"等待下緣離開原點..PositionInit.");
                                await Task.Delay(5000);
                                await SendChangePoseCmd(0.3, 1);
                                UpdateInitMessge($"等待下緣離開原點..Pose 0.3.");
                                await Task.Delay(1000);
                                _searchStatus = SEARCH_STATUS.MOVE_STEP_TO_FIND_HOME;
                            }
                            break;
                        case SEARCH_STATUS.UP_SEARCHING_FIND_HOME:
                            UpdateInitMessge($"向上搜尋原點...");
                            if (IsHomePoseSensorOn)
                            {
                                _searchStatus = SEARCH_STATUS.UP_SEARCHING_LEAVE_HOME;
                                break;
                            }

                            break;
                        case SEARCH_STATUS.MOVE_STEP_TO_FIND_HOME:

                            if (!IsHomePoseSensorOn)
                            {
                                await SendChangePoseCmd(CurrentActualPosition - 0.1);
                                UpdateInitMessge($"原點搜尋..{CurrentActualPosition}");
                                await Task.Delay(1000);
                            }
                            else
                            {
                                _searchStatus = SEARCH_STATUS.COMPLETE;
                            }
                            break;
                        case SEARCH_STATUS.COMPLETE:
                            await PositionInit();
                            await Task.Delay(1000);
                            logger.Info($"牙叉尋原點完成.目前位置:{CurrentActualPosition}.");
                            _isInitializeDone = true;
                            break;
                        default:
                            break;
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

        private SEARCH_STATUS GetSearchDirection()
        {
            if ((!IsHomePoseSensorOn && !IsDownLimitSensorOn && !IsUpLimitSensorOn) || IsUpLimitSensorOn)
            {
                return SEARCH_STATUS.START_DOWN_SEARCH_FIND_HOME;
            }
            else if (IsHomePoseSensorOn)
            {
                if (IsDownLimitSensorOn)
                    BypassLimitSensor();
                return SEARCH_STATUS.START_UP_SEARCH_WAIT_LEAVE_HOME;
            }
            else if (!IsHomePoseSensorOn && IsDownLimitSensorOn)
            {
                BypassLimitSensor();
                return SEARCH_STATUS.START_UP_SEARCH_FIND_HOME;
            }
            else
                return SEARCH_STATUS.START_DOWN_SEARCH_FIND_HOME;
        }

        private void UpdateInitMessge(string msg)
        {
            vehicle.InitializingStatusText = $"[{name}]原點復歸中...{msg}";
        }

        protected abstract Task<(bool confirm, string message)> UpSearchAsync(double speed = 0.1);
        protected abstract Task<(bool confirm, string message)> DownSearchAsync(double speed = 0.1);
        protected abstract Task<(bool confirm, string message)> StopAsync();
        protected abstract Task<(bool confirm, string message)> PositionInit();
        protected abstract Task<(bool confirm, string message)> SendChangePoseCmd(double pose, double speed = 0.1);
        protected virtual async Task NoBypassLimitSensor()
        {
            await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
        }
        protected virtual async Task BypassLimitSensor()
        {
            await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
            _ = Task.Delay(100).ContinueWith(async t =>
            {
                while (IsDownLimitSensorOn || IsUpLimitSensorOn)
                {
                    await Task.Delay(10);
                }
                await NoBypassLimitSensor();
            });
        }
    }
}
