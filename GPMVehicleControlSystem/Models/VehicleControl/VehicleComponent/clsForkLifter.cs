using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsForkLifter : CarComponent, IDIOUsagable
    {
        public enum FORK_LOCATIONS
        {
            UP_HARDWARE_LIMIT,
            UP_POSE,
            HOME,
            DOWN_POSE,
            DOWN_HARDWARE_LIMIT,
            UNKNOWN
        }

        /// <summary>
        /// 牙叉伸縮位置
        /// </summary>
        public enum FORK_ARM_LOCATIONS
        {
            HOME,
            END,
            UNKNOWN
        }

        public clsForkLifter()
        {
        }

        public clsForkLifter(ForkAGV forkAGV)
        {
            this.forkAGV = forkAGV;
        }

        public double CurrentHeightPosition => Math.Round(Driver.CurrentPosition, 3);
        public FORK_LOCATIONS CurrentForkLocation
        {
            //TODO 
            get
            {
                if (DIModule.GetState(DI_ITEM.Vertical_Home_Pos))
                    return FORK_LOCATIONS.HOME;
                else if (!DIModule.GetState(DI_ITEM.Vertical_Up_Hardware_limit))
                    return FORK_LOCATIONS.UP_HARDWARE_LIMIT;
                else if (DIModule.GetState(DI_ITEM.Vertical_Up_Pose))
                    return FORK_LOCATIONS.UP_POSE;
                else if (!DIModule.GetState(DI_ITEM.Vertical_Down_Hardware_limit))
                    return FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;
                else if (DIModule.GetState(DI_ITEM.Vertical_Down_Pose))
                    return FORK_LOCATIONS.DOWN_POSE;
                else return FORK_LOCATIONS.UNKNOWN;
            }
        }
        public FORK_ARM_LOCATIONS CurrentForkARMLocation
        {
            get
            {
                bool IsForkAtHome = !DIModule.GetState(DI_ITEM.Fork_Short_Exist_Sensor);
                bool IsForkAtEnd = !DIModule.GetState(DI_ITEM.Fork_Extend_Exist_Sensor);
                if (IsForkAtHome)
                    return FORK_ARM_LOCATIONS.HOME;
                else if (IsForkAtEnd)
                    return FORK_ARM_LOCATIONS.END;
                else
                    return FORK_ARM_LOCATIONS.UNKNOWN;
            }
        }
        private SUB_STATUS Sub_Status = SUB_STATUS.IDLE;
        /// <summary>
        /// 是否以初始化
        /// </summary>
        public bool IsInitialized { get; internal set; } = false;
        public bool IsInitialing { get; private set; } = false;
        /// <summary>
        /// 可以走的上極限位置
        /// </summary>


        public override COMPOENT_NAME component_name => COMPOENT_NAME.VERTIVAL_DRIVER;
        public clsDriver Driver { get; set; }
        public override string alarm_locate_in_name => "FORK";
        public clsDOModule DOModule { get; set; }
        private clsDIModule _DIModule;
        private double InitForkSpeed = 1.0;

        /// <summary>
        /// 是否啟用牙叉功能
        /// </summary>
        internal bool Enable => forkAGV.Parameters.ForkAGV.ForkLifer_Enable;
        internal double HSafe => forkAGV.Parameters.ForkAGV.SaftyPositionHeight;
        public Dictionary<int, clsWorkStationData> StationDatas
        {
            get
            {
                return forkAGV.WorkStations.Stations;
            }
        }

        public clsDIModule DIModule
        {
            get => _DIModule;
            set
            {
                _DIModule = value;
                _DIModule.SubsSignalStateChange(DI_ITEM.Fork_Under_Pressing_Sensor, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Vertical_Down_Hardware_limit, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Vertical_Up_Hardware_limit, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Fork_Short_Exist_Sensor, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Fork_Extend_Exist_Sensor, OnForkLifterSensorsStateChange);

                if (!forkAGV.Parameters.SensorBypass.BeltSensorBypass)
                    _DIModule.SubsSignalStateChange(DI_ITEM.Vertical_Belt_Sensor, OnForkLifterSensorsStateChange);
            }
        }


        private void OnForkLifterSensorsStateChange(object? sender, bool state)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    clsIOSignal? DI = sender as clsIOSignal;
                    if (!state && DI?.Input == DI_ITEM.Fork_Under_Pressing_Sensor)
                        Current_Alarm_Code = AlarmCodes.Fork_Bumper_Error;

                    else if (!state && DI?.Input == DI_ITEM.Vertical_Down_Hardware_limit)
                    {
                        if (!IsInitialing)
                        {
                            fork_ros_controller?.ZAxisStop();
                            Current_Alarm_Code = AlarmCodes.Zaxis_Down_Limit;
                        }
                    }
                    else if (!state && DI?.Input == DI_ITEM.Vertical_Up_Hardware_limit)
                    {
                        if (!IsInitialing)
                        {
                            fork_ros_controller?.ZAxisStop();
                            Current_Alarm_Code = AlarmCodes.Zaxis_Up_Limit;
                        }
                    }
                    else if (!state && DI?.Input == DI_ITEM.Vertical_Belt_Sensor)
                    {
                        if (!DOModule.GetState(DO_ITEM.Vertical_Belt_SensorBypass))
                        {
                            fork_ros_controller.ZAxisStop();
                            Current_Alarm_Code = AlarmCodes.Belt_Sensor_Error;
                        }
                    }
                    else if (!state && (DI?.Input == DI_ITEM.Fork_Short_Exist_Sensor || DI?.Input == DI_ITEM.Fork_Extend_Exist_Sensor)) //牙叉伸縮極限Sensor
                    {
                        ForkARMStop();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"{OnForkLifterSensorsStateChange} code error", ex);
                }
            });
        }

        internal ForkAGVController fork_ros_controller => forkAGV.AGVC as ForkAGVController;

        public bool IsHeightPreSettingActionRunning { get; internal set; }
        public bool IsManualOperation { get; internal set; } = false;

        private ForkAGV forkAGV;

        public override bool CheckStateDataContent()
        {
            return base.CheckStateDataContent();
        }

        /// <summary>
        /// Fork升降動作暫停
        /// </summary>
        /// <param name="IsEMS"></param>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> ForkStopAsync(bool IsEMS = false)
        {
            if (IsEMS)
            {
                fork_ros_controller.BeforeForkStopActionRequesting = new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest();
                fork_ros_controller.wait_action_down_cts.Cancel();
                logger.Trace("Call fork_ros_controller.wait_action_down_cts.Cancel()");
            }
            return await fork_ros_controller.ZAxisStop();
        }

        /// <summary>
        /// 繼續動作
        /// </summary>
        /// <returns></returns>
        internal async Task<(bool confirm, string message)> ForkResumeAction()
        {
            return await fork_ros_controller.ZAxisResume();
        }

        public async Task<(bool confirm, string message)> ForkPositionInit()
        {
            await Task.Delay(300);
            return await fork_ros_controller.ZAxisInit();
        }

        public async Task<(bool confirm, AlarmCodes alarm_code)> ForkGoHome(double speed = 1, bool wait_done = true)
        {
            if (!forkAGV.ZAxisGoHomingCheck())
            {
                return (false, AlarmCodes.Fork_Cannot_Go_Home_At_Non_Normal_Point);
                AlarmManager.AddAlarm(AlarmCodes.Fork_Cannot_Go_Home_At_Non_Normal_Point, false);
            }

            (bool confirm, string message) response = await fork_ros_controller.ZAxisGoHome(speed, wait_done);
            if (!response.confirm)
                return (false, AlarmCodes.Action_Timeout);
            return (true, AlarmCodes.None);
            //if (!DIModule.GetState(DI_ITEM.Vertical_Home_Pos))
            //    return (false, AlarmCodes.Fork_Go_Home_But_Home_Sensor_Signal_Error);
            //else
        }
        public async Task<(bool confirm, string message)> ForkPose(double pose, double speed = 0.1, bool wait_done = true, bool bypassCheck = false)
        {
            if (!bypassCheck)
            {
                if (pose < forkAGV.Parameters.ForkAGV.DownlimitPose)
                    pose = forkAGV.Parameters.ForkAGV.DownlimitPose;
                else if (pose > forkAGV.Parameters.ForkAGV.UplimitPose)
                    pose = forkAGV.Parameters.ForkAGV.UplimitPose;
            }
            return await fork_ros_controller.ZAxisGoTo(pose, speed, wait_done);
        }

        public async Task<(bool confirm, string message)> ForkUpAsync(double speed = 0.1)
        {
            return await fork_ros_controller.ZAxisUp(speed);
        }
        public async Task<(bool confirm, string message)> ForkDownAsync(double speed = 0.1)
        {
            return await fork_ros_controller.ZAxisDown(speed);
        }
        public async Task<(bool confirm, string message)> ForkUpSearchAsync(double speed = 0.1)
        {
            return await fork_ros_controller.ZAxisUpSearch(speed);
        }

        public async Task<(bool confirm, string message)> ForkDownSearchAsync(double speed = 0.1)
        {
            return await fork_ros_controller.ZAxisDownSearch(speed);
        }
        /// <summary>
        /// 牙叉伸出
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, AlarmCodes)> ForkExtendOutAsync(bool wait_reach_end = true)
        {
            bool _checked = await ForkARMStop();
            if (!_checked)
                return (true, AlarmCodes.None);
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.END)
                return (true, AlarmCodes.None);

            try
            {
                DO_ITEM _DO_USE_TO_EXTEND_FORK = DO_ITEM.Fork_Shortend;
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await DOModule.SetState(_DO_USE_TO_EXTEND_FORK, true);
                logger.Warn($"Wait Fork Extent DO ON..({_DO_USE_TO_EXTEND_FORK})");
                while (!DOModule.GetState(_DO_USE_TO_EXTEND_FORK))
                {
                    await Task.Delay(1);
                    if (cts.IsCancellationRequested)
                        return (false, AlarmCodes.Fork_Arm_Action_Timeout);
                }

                logger.Warn($"Fork Extent DO ON..!!({_DO_USE_TO_EXTEND_FORK})");
                if (wait_reach_end)
                {
                    cts.TryReset();
                    while (CurrentForkARMLocation != FORK_ARM_LOCATIONS.END)
                    {
                        await Task.Delay(1);
                        bool isStopState = !DOModule.GetState(DO_ITEM.Fork_Extend) && !DOModule.GetState(DO_ITEM.Fork_Shortend);
                        if (isStopState)
                            return (true, AlarmCodes.None);
                        if (!DIModule.GetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor))
                        {
                            ForkARMStop();
                            return (false, AlarmCodes.Fork_Frontend_has_Obstacle);
                        }
                        if (cts.IsCancellationRequested)
                            return (false, AlarmCodes.Fork_Arm_Action_Timeout);
                    }
                }
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                return (false, AlarmCodes.Fork_Arm_Action_Error);
            }
        }

        /// <summary>
        /// 牙叉縮回
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> ForkShortenInAsync(bool wait_reach_home = true)
        {
            ForkARMStop();
            await Task.Delay(400);
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.HOME)
                return (true, "");

            try
            {
                await DOModule.SetState(DO_ITEM.Fork_Extend, true);
                if (wait_reach_home)
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                    while (CurrentForkARMLocation != FORK_ARM_LOCATIONS.HOME)
                    {
                        await Task.Delay(1);
                        bool isStopState = !DOModule.GetState(DO_ITEM.Fork_Extend) && !DOModule.GetState(DO_ITEM.Fork_Shortend);
                        if (isStopState)
                            return (true, "");
                        if (cts.IsCancellationRequested)
                            return (false, "Timeout");
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message); ;

            }
            //已經有註冊極限Sensor輸入變化事件,到位後OFF Y輸出
        }

        /// <summary>
        /// 牙叉伸縮停止動作
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ForkARMStop()
        {
            if (DOModule.VCSOutputs.Any(it => it.Output == DO_ITEM.Fork_Extend))
            {
                await DOModule.SetState(DO_ITEM.Fork_Extend, false);
                await Task.Delay(100);
                await DOModule.SetState(DO_ITEM.Fork_Shortend, false);
                return true;
            }
            else
                return false;
        }


        private enum SEARCH_DIRECTION
        {
            DOWN,
            UP
        }

        /// <summary>
        /// 初始化Fork , 尋找原點
        /// </summary>
        public async Task<(bool done, AlarmCodes alarm_code)> ForkInitialize(double InitForkSpeed = 0.5)
        {
            logger.Info($"Fork 初始化動作開始，速度={InitForkSpeed}");
            IsInitialing = true;
            bool _isInitializeDone = false;
            (this.forkAGV.AGVC as ForkAGVController).IsInitializing = true;
            fork_ros_controller.wait_action_down_cts = new CancellationTokenSource();
            //bool isStartAtDownLimit = !DIModule.GetState(DI_ITEM.Vertical_Down_Hardware_limit);
            try
            {
                //if (isStartAtDownLimit)
                //await BypassLimitSensor();

                while (!_isInitializeDone)
                {
                    bool hasCargo = !DIModule.GetState(DI_ITEM.RACK_Exist_Sensor_1) || !DIModule.GetState(DI_ITEM.RACK_Exist_Sensor_2);

                    SEARCH_DIRECTION search_direction = DetermineSearchDirection(CurrentForkLocation);
                    ForkStartSearch(search_direction, hasCargo);

                    (bool reachHome, bool isReachLimitSensor) = await WaitForkReachHome(jumpOutIfReachLimitSensor: true);
                    if (!isReachLimitSensor)
                    {
                        await WaitForkLeaveHome();
                    }
                    else
                    {
                        await BypassLimitSensor();
                        await Task.Delay(1000);
                    }

                    await UpSearchAndWaitLeaveHome(hasCargo);
                    await Task.Delay(1000);
                    await ShortMoveToFindHome();
                    await Task.Delay(200);

                    if (forkAGV.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        logger.Fatal($"Status Down Fork initialize action interupted.!");
                        break;
                    }
                    _isInitializeDone = CurrentForkLocation == FORK_LOCATIONS.HOME && Math.Abs(CurrentHeightPosition - 0) < 0.01;
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                IsInitialing = IsInitialized = false;
                logger.Fatal($"[ForkInitialize] FAIL. {ex.Message}");
                return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
            }
            finally
            {
                await NoBypassLimitSensor();
            }
            (this.forkAGV.AGVC as ForkAGVController).IsInitializing = false;
            IsInitialing = false;
            IsInitialized = CurrentForkLocation == FORK_LOCATIONS.HOME;
            logger.Info($"Fork Initialize Done,Current Position : {Driver.CurrentPosition}_cm");
            return (true, AlarmCodes.None);

            async Task ShortMoveToFindHome()
            {
                while (CurrentForkLocation != FORK_LOCATIONS.HOME)
                {
                    if (this.forkAGV.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        throw new Exception("AGV Status Down");
                    }
                    double _pose = CurrentHeightPosition - 0.1;
                    await ForkPose(_pose, 1);
                    await Task.Delay(1000);
                }
                await ForkPositionInit();
            }
            async Task UpSearchAndWaitLeaveHome(bool hasCargo)
            {
                await ForkStopAsync();
                await Task.Delay(100);
                await ForkPositionInit();
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
                await ForkPose(CurrentHeightPosition + 0.3, 0.3, bypassCheck: true);
            }

            async Task<(bool reachHome, bool isReachLimitSensor)> WaitForkReachHome(bool jumpOutIfReachLimitSensor)
            {
                while (CurrentForkLocation != FORK_LOCATIONS.HOME)
                {
                    await Task.Delay(1);
                    if (this.forkAGV.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        throw new Exception("AGV Status Down");
                    }
                    if (jumpOutIfReachLimitSensor && CurrentForkLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
                    {
                        return (false, true);
                    }
                }
                return (true, false);
            }
            async Task WaitForkLeaveHome()
            {
                while (CurrentForkLocation == FORK_LOCATIONS.HOME)
                {
                    if (this.forkAGV.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        throw new Exception("AGV Status Down");
                    }
                    await Task.Delay(1);
                }

                await ForkStopAsync();
                await Task.Delay(500);
            }
            async Task NoBypassLimitSensor()
            {
                await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            }
            async Task BypassLimitSensor()
            {
                await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
            }
            async void ForkStartSearch(SEARCH_DIRECTION searchDriection, bool hasCargo)
            {
                if (searchDriection == SEARCH_DIRECTION.DOWN)
                    await ForkDownSearchAsync(hasCargo ? InitForkSpeed : 1.0);
                else
                    await ForkUpSearchAsync(hasCargo ? InitForkSpeed : 1.0);
            }

            SEARCH_DIRECTION DetermineSearchDirection(FORK_LOCATIONS forkLocation)
            {
                if (forkLocation == FORK_LOCATIONS.HOME || forkLocation == FORK_LOCATIONS.UNKNOWN || forkLocation == FORK_LOCATIONS.UP_POSE)
                    return SEARCH_DIRECTION.DOWN;
                else
                    return SEARCH_DIRECTION.UP;
            }

            //fork_ros_controller.wait_action_down_cts = new CancellationTokenSource();
            //try
            //{
            //    bool hasCargo = !DIModule.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor) || !DIModule.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
            //    int _delay_time_before_next_action() => hasCargo ? 2000 : 1000;
            //    bool IsHomeSensorOn() => CurrentForkLocation == FORK_LOCATIONS.HOME;
            //    this.InitForkSpeed = InitForkSpeed;
            //    IsInitialized = false;
            //    IsInitialing = true;
            //    bool IsDownSearch = CurrentForkLocation != FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;

            //    if (IsDownSearch)
            //        await ForkDownSearchAsync(hasCargo ? InitForkSpeed : 1.0);
            //    else
            //    {
            //        await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
            //        var rsponse = await ForkUpSearchAsync(InitForkSpeed);
            //    }
            //    forkAGV.InitializingStatusText = $"Fork {(IsDownSearch ? "Down " : "Up")} Search Start";


            //    bool _reachHome = IsHomeSensorOn();
            //    bool _leaveHome = false;
            //    while (true)
            //    {
            //        if (forkAGV.GetSub_Status() == SUB_STATUS.DOWN)
            //        {
            //            return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
            //        }
            //        await Task.Delay(1);
            //        if (IsDownSearch && CurrentForkLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
            //        {
            //            _reachHome = false;
            //            IsDownSearch = false;
            //            await ForkPositionInit();
            //            await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
            //            await ForkUpSearchAsync();

            //        }
            //        if (!_reachHome)
            //        {
            //            _reachHome = IsHomeSensorOn();
            //            if (_reachHome)
            //            {
            //                forkAGV.InitializingStatusText = $"Fork reach home first";
            //            }
            //        }
            //        if (_reachHome && !IsHomeSensorOn())
            //        {
            //            forkAGV.InitializingStatusText = $"Fork leave home";
            //            if (!IsDownSearch)
            //                await Task.Delay(hasCargo ? 500 : 10);
            //            var rsponse = await ForkStopAsync();
            //            break;
            //        }

            //    }
            //    await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            //    await Task.Delay(_delay_time_before_next_action());

            //    if (IsDownSearch)
            //    {
            //        double _shift_up_distance = 2.3;
            //        Thread.Sleep(10);
            //        await GoUpToAboveHome(_shift_up_distance, true);
            //        Thread.Sleep(300);
            //        while (CurrentForkLocation == FORK_LOCATIONS.HOME)
            //        {
            //            logger.Warn($"Not leave home, up 0.5 continue...");
            //            Thread.Sleep(300);
            //            await GoUpToAboveHome(CurrentHeightPosition + 0.5, false);
            //        }

            //        async Task GoUpToAboveHome(double pose, bool initPose)
            //        {
            //            (bool confirm, string message) response = (false, "");
            //            while (!response.confirm)
            //            {
            //                Thread.Sleep(10);
            //                if (initPose)
            //                {
            //                    response = await ForkPositionInit();
            //                    logger.Info($" Fork init and Go To {pose} ForkPositionInit {response.confirm},{response.message}");
            //                    if (!response.confirm)
            //                        continue;
            //                }
            //                Thread.Sleep(1000);
            //                response = await ForkPose(pose, 1);
            //                logger.Info($" Fork init and Go To {pose} ForkPose {response.confirm},{response.message}");
            //                if (!response.confirm)
            //                    continue;
            //            }
            //        }

            //        //_shift_up_distance = 0.1;

            //    }
            //    forkAGV.InitializingStatusText = $"Fork尋原點動作中...";
            //    Thread.Sleep(_delay_time_before_next_action());
            //    while (!IsHomeSensorOn())
            //    {
            //        Thread.Sleep(1000);
            //        if (CurrentForkLocation == FORK_LOCATIONS.HOME)
            //            break;
            //        if (forkAGV.GetSub_Status() == SUB_STATUS.DOWN)
            //        {
            //            return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
            //        }
            //        var pose = Driver.CurrentPosition - 0.05;
            //        logger.Info($"Fork Shorten move to find Home Point, pose commadn position is {pose}");
            //        var response = await ForkPose(pose, 0.1);
            //        logger.Info($"{response.confirm},{response.message}");
            //    }

            //    if (IsHomeSensorOn())
            //    {
            //        (bool confirm, string message) fork_init_response = (false, "");
            //        CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            //        bool IsForkInitFinish((bool confirm, string message) response)
            //        {
            //            return response.confirm && Math.Abs(Driver.CurrentPosition - 0) < 0.1;
            //        }

            //        while (!IsForkInitFinish(fork_init_response))
            //        {
            //            Thread.Sleep(200);
            //            if (cancellation.IsCancellationRequested)
            //                return (false, AlarmCodes.Action_Timeout);
            //            fork_init_response = await ForkPositionInit();
            //            logger.Info($"Fork cmd : init response: {fork_init_response.confirm}<{fork_init_response.message}>");
            //        }
            //        var home_position_error_ = Math.Abs(Driver.CurrentPosition - 0);
            //        IsInitialized = IsHomeSensorOn() && home_position_error_ < 0.01;

            //        if (!IsInitialized)
            //        {
            //            logger.Fatal(!IsHomeSensorOn() ? $"Fork Initialize Done but Home DI Not ON" : $"Fork Initialize Done But Driver Position Error to much ({home_position_error_} cm)");
            //            return (false, !IsHomeSensorOn() ? AlarmCodes.Fork_Initialized_But_Home_Input_Not_ON : AlarmCodes.Fork_Initialized_But_Driver_Position_Not_ZERO);
            //        }
            //        IsInitialing = false;
            //        logger.Info($"Fork Initialize Done,Current Position : {Driver.CurrentPosition}_cm");
            //        return (true, AlarmCodes.None);
            //    }
            //    else
            //    {
            //        IsInitialing = false;
            //        return (false, AlarmCodes.Fork_Initialized_But_Home_Input_Not_ON);
            //    }
            //}
            //catch (TimeoutException ex)
            //{
            //    IsInitialing = false;
            //    return (false, AlarmCodes.Action_Timeout);
            //}
            //catch (Exception ex)
            //{
            //    IsInitialing = false;
            //    return (false, AlarmCodes.Fork_Initialize_Process_Error_Occur);
            //}

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag">工位的TAG</param>
        /// <param name="height">第N層(Zero-base)</param>
        /// <param name="position">該層之上/下位置</param>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<(double position, bool success, AlarmCodes alarm_code)> ForkGoTeachedPoseAsync(int tag, int height, FORK_HEIGHT_POSITION position, double speed)
        {
            double target = 0;
            try
            {
                fork_ros_controller.wait_action_down_cts = new CancellationTokenSource();
                if (!StationDatas.TryGetValue(tag, out clsWorkStationData? workStation))
                    return (target, false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_Tag);

                if (!workStation.LayerDatas.TryGetValue(height, out clsStationLayerData? teach))
                    return (target, false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_layer);

                if (teach.Down_Pose == 0 && teach.Up_Pose == 0)
                    return (target, false, AlarmCodes.Fork_Slot_Teach_Data_ERROR);

                (bool confirm, string message) forkMoveResult = (false, "");


                if (position == FORK_HEIGHT_POSITION.UP_)
                    target = teach.Up_Pose;
                if (position == FORK_HEIGHT_POSITION.DOWN_)
                    target = teach.Down_Pose;

                int tryCnt = 0;
                double positionError = 0;

                await ForkStopAsync();
                await Task.Delay(200);

                logger.Warn($"Fork Start Goto Height={height},Position={target}(Current Position={Driver.CurrentPosition}cm) at Tag:{tag}.[{position}]");

                bool belt_sensor_bypass = forkAGV.Parameters.SensorBypass.BeltSensorBypass;


                double _errorTorlence = 0.5;
                while (ForkPositionLargeThanTorrlence(CurrentHeightPosition, target, _errorTorlence, out positionError))
                {
                    await Task.Delay(1, fork_ros_controller.wait_action_down_cts.Token);

                    if (AGVBeltStatusError())
                        return (target, false, AlarmCodes.Belt_Sensor_Error);

                    if (AGVStatusError())
                    {
                        logger.Error($"Tag:{tag},Height:{height},{position} AGV Status Error ,Fork Try  Go to teach position process break!");
                        return (target, false, AlarmCodes.None);
                    }
                    tryCnt++;
                    if (fork_ros_controller.wait_action_down_cts.IsCancellationRequested)
                        return (target, false, AlarmCodes.Fork_Action_Aborted);

                    logger.Warn($"[Tag={tag}] Fork pose error to Height-{height} {target} is {positionError}。Try change pose-{tryCnt}");
                    forkMoveResult = await ForkPose(target, speed);//TODO move to error position (0) 0416
                    logger.Warn($"[Tag={tag}] Call Fork Service and Fork Action done.(Current Position={Driver.CurrentPosition} cm)");
                    if (!forkMoveResult.confirm)
                    {
                        AlarmCodes _alarm_code = fork_ros_controller.wait_action_down_cts.IsCancellationRequested ? AlarmCodes.Fork_Action_Aborted : AlarmCodes.Action_Timeout;
                        return (target, false, _alarm_code);
                    }
                }

                logger.Trace($"Position={Driver.CurrentPosition}/{target}(Error={positionError})");
                //Final Check
                if (ForkPositionLargeThanTorrlence(CurrentHeightPosition, target, _errorTorlence, out _) && forkMoveResult.confirm)
                    return (target, false, AlarmCodes.Fork_Height_Setting_Error);

                return (target, true, AlarmCodes.None);

                #region Local Functions
                //計算牙叉當前位置距離目標位置的誤差值
                double GetPositionErrorVal(double currentHeightPosition, double position_to_reach)
                {
                    return positionError = Math.Abs(currentHeightPosition - position_to_reach);
                }
                //計算牙叉當前位置距離目標位置的誤差值是否在允許範圍內
                bool ForkPositionLargeThanTorrlence(double currentHeightPosition, double position_to_reach, double errorTorlence, out double positionError)
                {
                    positionError = GetPositionErrorVal(currentHeightPosition, position_to_reach);
                    return positionError > errorTorlence;
                }
                //皮帶是否有異常
                bool AGVBeltStatusError() => !belt_sensor_bypass && !DIModule.GetState(DI_ITEM.Vertical_Belt_Sensor) && !DOModule.GetState(DO_ITEM.Vertical_Belt_SensorBypass);
                bool AGVStatusError() => forkAGV.GetSub_Status() == SUB_STATUS.DOWN || forkAGV.GetSub_Status() == SUB_STATUS.Initializing;
                #endregion
            }
            catch (TaskCanceledException ex)
            {
                logger.Error(ex);
                return (target, false, AlarmCodes.Fork_Action_Aborted);
            }
            catch (OperationCanceledException ex)
            {
                logger.Error(ex);
                return (target, false, AlarmCodes.Fork_Action_Aborted);
            }
            catch (Exception ex)
            {
                logger.Fatal(ex);
                return (target, false, AlarmCodes.Code_Error_In_System);
            }

        }

    }
}
