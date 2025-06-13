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
    public abstract class MotorBaseHomeSearchHelper : IDisposable
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
            START_UP_SEARCH_WAIT_UNDER_PRESS_SENSOR_RELEASE,
            UP_SEARCHING_LEAVE_HOME,
            UP_SEARCHING_FIND_HOME,
            MOVE_STEP_TO_FIND_HOME,
            WAIT_UNDER_PRESS_SENSOR_RELEASE,
            COMPLETE
        }

        protected readonly Vehicle vehicle;
        protected readonly clsDOModule DOModule;
        protected readonly clsDIModule DIModule;
        protected Logger logger = LogManager.GetCurrentClassLogger();
        protected virtual double speedWhenSearchStartWithoutCargo { get; set; } = 0.5;

        public readonly string name;

        private string _initText = string.Empty;

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
        protected abstract bool IsUnderPressingSensorOn { get; }
        protected abstract bool IsHomePoseSensorOn { get; }
        protected abstract bool IsDownLimitSensorOn { get; }
        protected abstract bool IsUpLimitSensorOn { get; }

        protected abstract double CurrentActualPosition { get; }

        private CancellationToken cancelToken;

        private SEARCH_DIRECTION currentSearchDirection;
        private bool disposedValue;

        public async Task<(bool done, AlarmCodes alarm_code)> StartSearchAsync(CancellationToken token)
        {
            cancelToken = token;
            bool _isInitializeDone = false;
            logger.Info("Start Search Home");
            string key = $"{DateTime.Now.ToString("yyyyMMddHHmmssffff")}";
            double downSearchSpeed = vehicle.Parameters.ForkAGV.DownSearchSpeedWhenInitialize;
            double start_down_step_find_home_pose = vehicle.Parameters.ForkAGV.START_DONW_STEP_FIND_HOME_POSE;
            try
            {
                SEARCH_STATUS _searchStatus = SEARCH_STATUS.DETERMINE_SEARCH_DIRECTION;
                StartUpdateInitTextProcess(token);
                while (!_isInitializeDone)
                {
                    if (vehicle.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        await StopAsync();
                        logger.Fatal($"Status Down Fork initialize action interupted.!");
                        break;
                    }
                    await Task.Delay(50, cancelToken);
                    switch (_searchStatus)
                    {
                        case SEARCH_STATUS.DETERMINE_SEARCH_DIRECTION:
                            _initText = $"尋原點方向:{_searchStatus}";
                            _searchStatus = await GetSearchDirectionAsync();

                            break;

                        case SEARCH_STATUS.START_UP_SEARCH_WAIT_UNDER_PRESS_SENSOR_RELEASE:
                            await UpSearchAsync(0.1);
                            _searchStatus = SEARCH_STATUS.WAIT_UNDER_PRESS_SENSOR_RELEASE;
                            break;
                        case SEARCH_STATUS.WAIT_UNDER_PRESS_SENSOR_RELEASE:
                            if (!IsUnderPressingSensorOn)
                            {
                                _initText = $"Under Pressing 狀態已解除,停止上升";
                                await StopAsync();
                                _searchStatus = SEARCH_STATUS.DETERMINE_SEARCH_DIRECTION;
                                break;
                            }
                            _initText = $"牙叉移動中|等待 Under Pressing Sensor 解除狀態...";
                            break;
                        case SEARCH_STATUS.START_DOWN_SEARCH_FIND_HOME:
                            await DownSearchAsync(downSearchSpeed);
                            _searchStatus = SEARCH_STATUS.DOWN_SEARCHING_FIND_HOME;
                            break;
                        case SEARCH_STATUS.START_UP_SEARCH_WAIT_LEAVE_HOME:
                            await UpSearchAsync(0.1);
                            _searchStatus = SEARCH_STATUS.UP_SEARCHING_LEAVE_HOME;
                            break;
                        case SEARCH_STATUS.START_UP_SEARCH_FIND_HOME:
                            await UpSearchAsync(0.1);
                            _searchStatus = SEARCH_STATUS.UP_SEARCHING_FIND_HOME;
                            break;
                        case SEARCH_STATUS.DOWN_SEARCHING_FIND_HOME:
                            _initText = $"向下搜尋原點,Speed:{downSearchSpeed}";

                            if (IsHomePoseSensorOn)
                            {
                                await StopAsync();
                                await Task.Delay(1000, cancelToken);

                                if (IsDownLimitSensorOn)
                                    await BypassLimitSensor();

                                if (!IsHomePoseSensorOn) //停止後可能又離開了 Home位置=>等同於需要向上蒐尋找原點
                                {
                                    _searchStatus = SEARCH_STATUS.START_UP_SEARCH_FIND_HOME;
                                    break;
                                }

                                _searchStatus = SEARCH_STATUS.START_UP_SEARCH_WAIT_LEAVE_HOME;
                                break;
                            }

                            if (IsDownLimitSensorOn && !IsHomePoseSensorOn)
                            {
                                await StopAsync();
                                await BypassLimitSensor();
                                await Task.Delay(200);
                                _searchStatus = SEARCH_STATUS.START_UP_SEARCH_FIND_HOME;
                                break;
                            }

                            break;
                        case SEARCH_STATUS.UP_SEARCHING_LEAVE_HOME:

                            _initText = $"等待下緣離開原點";
                            if (!IsHomePoseSensorOn)
                            {
                                await StopAsync();
                                _Log($"下緣離開原點 StopAsync");
                                await Task.Delay(1000, cancelToken);
                                await PositionInit();
                                _Log($"下緣離開原點 PositionInit");
                                await Task.Delay(1000, cancelToken);
                                _Log($"下緣離開原點 SendChangePoseCmd({start_down_step_find_home_pose}, 1)");

                                _initText = $"移動至吋動搜尋位置:向上 {start_down_step_find_home_pose} cm";
                                await SendChangePoseCmd(start_down_step_find_home_pose, 1);
                                await Task.Delay(1000, cancelToken);
                                _searchStatus = SEARCH_STATUS.MOVE_STEP_TO_FIND_HOME;
                            }
                            break;
                        case SEARCH_STATUS.UP_SEARCHING_FIND_HOME:
                            _initText = $"向上搜尋原點...";
                            if (IsHomePoseSensorOn)
                            {
                                _searchStatus = SEARCH_STATUS.UP_SEARCHING_LEAVE_HOME;
                                break;
                            }

                            break;
                        case SEARCH_STATUS.MOVE_STEP_TO_FIND_HOME:
                            if (!IsHomePoseSensorOn)
                            {
                                await Task.Delay(1000, cancelToken);
                                double newPose = CurrentActualPosition - 0.1;
                                string currentPosStr = Math.Round(CurrentActualPosition, 3).ToString();
                                string newPoseStr = Math.Round(newPose, 3).ToString();
                                _initText = $"吋動搜尋中..{currentPosStr}->{newPoseStr}";
                                await SendChangePoseCmd(newPose);
                            }
                            else
                            {
                                _searchStatus = SEARCH_STATUS.COMPLETE;
                            }
                            break;
                        case SEARCH_STATUS.COMPLETE:
                            await PositionInit();
                            await Task.Delay(1000, cancelToken);
                            logger.Info($"牙叉尋原點完成.目前位置:{CurrentActualPosition}.");
                            _isInitializeDone = true;
                            break;
                        default:
                            break;
                    }

                }
                return (_isInitializeDone, _isInitializeDone ? AlarmCodes.None : AlarmCodes.Fork_Arm_Action_Timeout);

                void _Log(string msg)
                {
                    logger.Trace($"[{key}]{_searchStatus}:{msg}");
                }
            }
            catch (TaskCanceledException ex)
            {
                logger.Warn("[TaskCanceledException] " + ex.Message);
                return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
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

        private async Task StartUpdateInitTextProcess(CancellationToken token)
        {
            _ = Task.Run(async () =>
            {
                using Vehicle.InitMessageUpdater initMsgUpdater = vehicle.CreateInitMsgUpdater();
                while (true)
                {
                    try
                    {
                        if (vehicle.GetSub_Status() == SUB_STATUS.DOWN || vehicle.IsInitialized)
                            break;
                        await initMsgUpdater.Update(_initText);
                        await Task.Delay(100, token);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
                vehicle.LogDebugMessage("Home search Init Message updater process end", true);
            });
        }

        private async Task<SEARCH_STATUS> GetSearchDirectionAsync()
        {

            if (IsUnderPressingSensorOn)
            {
                //壓到東西了
                await BypassUnderPressingSensor();
                return SEARCH_STATUS.START_UP_SEARCH_WAIT_UNDER_PRESS_SENSOR_RELEASE;
            }

            //僅有上極限ON 
            if (!IsHomePoseSensorOn && !IsDownLimitSensorOn && IsUpLimitSensorOn)
            {
                await BypassLimitSensor();
                await Task.Delay(200);
                return SEARCH_STATUS.START_DOWN_SEARCH_FIND_HOME;
            }
            //原點、極限皆未ON 
            else if (!IsHomePoseSensorOn && !IsDownLimitSensorOn && !IsUpLimitSensorOn)
            {
                return SEARCH_STATUS.START_DOWN_SEARCH_FIND_HOME;
            }
            else if (IsHomePoseSensorOn && !IsDownLimitSensorOn) //原點ON但下極限OFF 
            {
                return SEARCH_STATUS.START_UP_SEARCH_WAIT_LEAVE_HOME;
            }
            else if (IsHomePoseSensorOn && IsDownLimitSensorOn) //原點ON且下極限ON
            {
                await BypassLimitSensor();
                await Task.Delay(200);
                return SEARCH_STATUS.START_UP_SEARCH_WAIT_LEAVE_HOME;
            }
            else if (!IsHomePoseSensorOn && IsDownLimitSensorOn) //僅下極限 ON 且原點OFF
            {
                await BypassLimitSensor();
                await Task.Delay(200);
                return SEARCH_STATUS.START_UP_SEARCH_FIND_HOME;
            }
            else
                return SEARCH_STATUS.START_DOWN_SEARCH_FIND_HOME;
        }

        private async Task BypassUnderPressingSensor()
        {

            await DOModule.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, true);
            _ = Task.Delay(100).ContinueWith(async t =>
            {
                //等待壓力感測器解除
                while (IsUnderPressingSensorOn)
                {
                    await Task.Delay(10);
                }
                await DOModule.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, false);
            });
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)
                }

                // TODO: 釋出非受控資源 (非受控物件) 並覆寫完成項
                // TODO: 將大型欄位設為 Null
                disposedValue = true;
            }
        }

        // // TODO: 僅有當 'Dispose(bool disposing)' 具有會釋出非受控資源的程式碼時，才覆寫完成項
        // ~MotorBaseHomeSearchHelper()
        // {
        //     // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
