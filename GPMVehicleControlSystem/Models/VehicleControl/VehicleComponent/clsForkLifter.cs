using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using System.Diagnostics;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using System.Security.AccessControl;
using AGVSystemCommonNet6.Log;
using Newtonsoft.Json;

using GPMVehicleControlSystem.Models.WorkStation;
using System.ComponentModel;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;

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
        private bool IsSimulationMode => forkAGV.Parameters.SimulationMode;
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
        internal bool Enable => forkAGV.Parameters.ForkLifer_Enable;
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
                    else if (!state && (DI?.Input == DI_ITEM.Fork_Short_Exist_Sensor | DI?.Input == DI_ITEM.Fork_Extend_Exist_Sensor)) //牙叉伸縮極限Sensor
                    {
                        ForkARMStop();
                    }
                }
                catch (Exception ex)
                {
                    LOG.ERROR($"{OnForkLifterSensorsStateChange} code error", ex);
                }
            });
        }

        internal ForkAGVController fork_ros_controller => forkAGV.AGVC as ForkAGVController;
        private ForkAGV forkAGV;

        public override async Task<bool> CheckStateDataContent()
        {
            return await base.CheckStateDataContent();
        }
        public async Task<(bool confirm, string message)> ForkStopAsync(bool IsEMS = true)
        {
            if (IsEMS)
            {
                (forkAGV.AGVC as ForkAGVController).wait_action_down_cts.Cancel();
                LOG.TRACE("Call fork_ros_controller.wait_action_down_cts.Cancel()");
            }
            return await fork_ros_controller.ZAxisStop();
        }

        internal async Task ForkResumeAction()
        {
            await fork_ros_controller.ZAxisResume();
        }

        public async Task<(bool confirm, string message)> ForkPositionInit()
        {
            await Task.Delay(300);
            return await fork_ros_controller.ZAxisInit();
        }

        public async Task<(bool confirm, AlarmCodes alarm_code)> ForkGoHome(double speed = 1, bool wait_done = true)
        {
            (bool confirm, string message) response = await fork_ros_controller.ZAxisGoHome(speed, wait_done);

            if (!response.confirm)
                return (false, AlarmCodes.Action_Timeout);
            if (!DIModule.GetState(DI_ITEM.Vertical_Home_Pos))
                return (false, AlarmCodes.Fork_Go_Home_But_Home_Sensor_Signal_Error);
            else
                return (true, AlarmCodes.None);
        }
        public async Task<(bool confirm, string message)> ForkPose(double pose, double speed = 0.1, bool wait_done = true)
        {
            if (pose < forkAGV.Parameters.ForkAGV.DownlimitPose)
                pose = forkAGV.Parameters.ForkAGV.DownlimitPose;
            else if (pose > forkAGV.Parameters.ForkAGV.UplimitPose)
                pose = forkAGV.Parameters.ForkAGV.UplimitPose;
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
        public async Task<(bool confirm, string message)> ForkExtendOutAsync(bool wait_reach_end = true)
        {
            await ForkARMStop();
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.END)
                return (true, "");

            try
            {
                await DOModule.SetState(DO_ITEM.Fork_Shortend, true);
                if (wait_reach_end)
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                    while (CurrentForkARMLocation != FORK_ARM_LOCATIONS.END)
                    {
                        Thread.Sleep(1);
                        bool isStopState = !DOModule.GetState(DO_ITEM.Fork_Extend) && !DOModule.GetState(DO_ITEM.Fork_Shortend);
                        if (isStopState)
                            return (true, "");
                        if (!DIModule.GetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor))
                        {
                            ForkARMStop();
                            return (false, "前端障礙物檢出");
                        }
                        if (cts.IsCancellationRequested)
                            return (false, "Timeout");
                    }
                }
                return (true, ""); ;
            }
            catch (Exception ex)
            {
                return (false, ex.Message); ;
            }
        }

        /// <summary>
        /// 牙叉縮回
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> ForkShortenInAsync(bool wait_reach_home = true)
        {
            ForkARMStop();
            Thread.Sleep(400);
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
        public async Task ForkARMStop()
        {
            await DOModule.SetState(DO_ITEM.Fork_Extend, false);
            await Task.Delay(100);
            await DOModule.SetState(DO_ITEM.Fork_Shortend, false);
        }




        /// <summary>
        /// 初始化Fork , 尋找原點
        /// </summary>
        public async Task<(bool done, AlarmCodes alarm_code)> ForkInitialize(double InitForkSpeed = 0.5)
        {
            fork_ros_controller.wait_action_down_cts = new CancellationTokenSource();
            try
            {
                bool hasCargo = !DIModule.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor) | !DIModule.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
                this.InitForkSpeed = InitForkSpeed;
                IsInitialized = false;
                IsInitialing = true;
                bool IsDownSearch = CurrentForkLocation != FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;

                if (IsDownSearch)
                    await ForkDownSearchAsync(hasCargo ? InitForkSpeed : 1.0);
                else
                {
                    await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
                    var rsponse = await ForkUpSearchAsync(InitForkSpeed);
                }
                LOG.INFO($"Fork {(IsDownSearch ? "Down " : "Up")} Search Start");

                bool reachHome = CurrentForkLocation == FORK_LOCATIONS.HOME;
                bool leaveHome = false;
                while (true)
                {
                    if (forkAGV.Sub_Status == SUB_STATUS.DOWN)
                    {
                        return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
                    }
                    await Task.Delay(1);
                    if (IsDownSearch && CurrentForkLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
                    {
                        reachHome = false;
                        IsDownSearch = false;
                        await ForkPositionInit();
                        await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
                        await ForkUpSearchAsync();

                    }
                    if (!reachHome)
                    {
                        reachHome = CurrentForkLocation == FORK_LOCATIONS.HOME;
                        if (reachHome)
                            LOG.INFO($"Fork reach home first");
                    }
                    if (reachHome && CurrentForkLocation != FORK_LOCATIONS.HOME)
                    {
                        LOG.INFO($"Fork leave home ");
                        if (!IsDownSearch)
                            await Task.Delay(hasCargo ? 500 : 10);
                        var rsponse = await ForkStopAsync();
                        break;
                    }

                }
                await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
                if (hasCargo)
                    LOG.WARN($"Has Cargo On Fork, 緩衝時間2s");
                await Task.Delay(hasCargo ? 2000 : 1000);

                if (IsDownSearch)
                {
                    LOG.INFO($"Home above , Fork init and Go To 2.5");
                    (bool confirm, string message) response = (false, "");
                    while (!response.confirm)
                    {
                        Thread.Sleep(10);
                        response = await ForkPositionInit();
                        LOG.INFO($" Fork init and Go To 2.5 ForkPositionInit {response.confirm},{response.message}");
                        if (!response.confirm)
                            continue;
                        Thread.Sleep(1000);
                        response = await ForkPose(2.3, 1);
                        LOG.INFO($" Fork init and Go To 2.5 ForkPose {response.confirm},{response.message}");
                        if (!response.confirm)
                            continue;
                    }
                }

                LOG.INFO($"Fork Shorten move to find Home Point Start");
                if (hasCargo)
                    LOG.WARN($"[Find Home]Has Cargo On Fork, 緩衝時間2s");
                await Task.Delay(hasCargo ? 2000 : 1000);
                while (CurrentForkLocation != FORK_LOCATIONS.HOME)
                {
                    await Task.Delay(1000);
                    if (CurrentForkLocation == FORK_LOCATIONS.HOME)
                        break;
                    if (forkAGV.Sub_Status == SUB_STATUS.DOWN)
                    {
                        return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
                    }
                    var pose = Driver.CurrentPosition - 0.05;
                    LOG.INFO($"Fork Shorten move to find Home Point, pose commadn position is {pose}");
                    var response = await ForkPose(pose, 0.1);
                    LOG.INFO($"{response.confirm},{response.message}");
                }

                var IsReachHome = CurrentForkLocation == FORK_LOCATIONS.HOME;
                IsInitialing = false;
                if (IsReachHome)
                {
                    (bool confirm, string message) response = (false, "");
                    CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    while (!response.confirm | Math.Abs(Driver.CurrentPosition - 0) > 0.1)
                    {
                        await Task.Delay(200);
                        if (cancellation.IsCancellationRequested)
                            return (false, AlarmCodes.Action_Timeout);
                        response = await ForkPositionInit();
                        LOG.INFO($"Fork cmd : init response: {response.confirm}<{response.message}>");
                    }
                    var home_position_error_ = Math.Abs(Driver.CurrentPosition - 0);
                    IsInitialized = IsReachHome && home_position_error_ < 0.01;

                    if (!IsInitialized)
                    {
                        LOG.Critical(!IsReachHome ? $"Fork Initialize Done but Home DI Not ON" : $"Fork Initialize Done But Driver Position Error to much ({home_position_error_} cm)");
                        return (false, !IsReachHome ? AlarmCodes.Fork_Initialized_But_Home_Input_Not_ON : AlarmCodes.Fork_Initialized_But_Driver_Position_Not_ZERO);
                    }

                    LOG.INFO($"Fork Initialize Done,Current Position : {Driver.CurrentPosition}_cm");
                    return (true, AlarmCodes.None);
                }
                else
                {
                    return (false, AlarmCodes.Fork_Initialized_But_Home_Input_Not_ON);
                }
            }
            catch (TimeoutException ex)
            {
                IsInitialing = false;
                return (false, AlarmCodes.Action_Timeout);
            }
            catch (Exception ex)
            {
                IsInitialing = false;
                return (false, AlarmCodes.Fork_Initialize_Process_Error_Occur);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag">工位的TAG</param>
        /// <param name="height">第N層(Zero-base)</param>
        /// <param name="position">該層之上/下位置</param>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<(bool success, AlarmCodes alarm_code)> ForkGoTeachedPoseAsync(int tag, int height, FORK_HEIGHT_POSITION position, double speed)
        {

            try
            {
                fork_ros_controller.wait_action_down_cts = new CancellationTokenSource();
                if (!StationDatas.TryGetValue(tag, out clsWorkStationData? workStation))
                    return (false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_Tag);

                if (!workStation.LayerDatas.TryGetValue(height, out clsStationLayerData? teach))
                    return (false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_layer);

                (bool confirm, string message) forkMoveResult = (false, "");
                double position_to_reach = 0;

                if (position == FORK_HEIGHT_POSITION.UP_)
                    position_to_reach = teach.Up_Pose;
                if (position == FORK_HEIGHT_POSITION.DOWN_)
                    position_to_reach = teach.Down_Pose;

                int tryCnt = 0;
                double positionError = 0;
                double errorTorlence = 0.5;

                await ForkStopAsync(IsEMS: false);
                await Task.Delay(200);

                LOG.WARN($"Fork Start Goto Height={height},Position={position_to_reach}(Current Position={Driver.CurrentPosition}cm) at Tag:{tag}.[{position}]");
                bool belt_sensor_bypass = forkAGV.Parameters.SensorBypass.BeltSensorBypass;
                while ((positionError = Math.Abs(Driver.CurrentPosition - position_to_reach)) > errorTorlence)
                {
                    await Task.Delay(1, fork_ros_controller.wait_action_down_cts.Token);
                    if (!belt_sensor_bypass && !DIModule.GetState(DI_ITEM.Vertical_Belt_Sensor) && !DOModule.GetState(DO_ITEM.Vertical_Belt_SensorBypass))
                        return (false, AlarmCodes.Belt_Sensor_Error);

                    if (forkAGV.Sub_Status == SUB_STATUS.DOWN | forkAGV.Sub_Status == SUB_STATUS.Initializing)
                    {
                        LOG.ERROR($"Tag:{tag},Height:{height},{position} AGV Status Error ,Fork Try  Go to teach position process break!");
                        return (false, AlarmCodes.None);
                    }
                    tryCnt++;
                    if (fork_ros_controller.wait_action_down_cts.IsCancellationRequested)
                        return (false, AlarmCodes.Fork_Action_Aborted);

                    LOG.WARN($"[Tag={tag}] Fork pose error to Height-{height} {position_to_reach} is {positionError}。Try change pose-{tryCnt}");
                    forkMoveResult = await ForkPose(position_to_reach, speed);

                    if (!forkMoveResult.confirm)
                    {
                        return (false, fork_ros_controller.wait_action_down_cts.IsCancellationRequested ? AlarmCodes.Fork_Action_Aborted : AlarmCodes.Action_Timeout);
                    }
                }
                LOG.TRACE($"Position={Driver.CurrentPosition}/{position_to_reach}(Error={positionError})");
                if (positionError > errorTorlence && forkMoveResult.confirm)
                    return (false, AlarmCodes.Fork_Height_Setting_Error);

                return (true, AlarmCodes.None);

            }
            catch (TaskCanceledException ex)
            {
                LOG.ERROR(ex);
                return (false, AlarmCodes.Fork_Action_Aborted);
            }
            catch (OperationCanceledException ex)
            {
                LOG.ERROR(ex);
                return (false, AlarmCodes.Fork_Action_Aborted);
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                return (false, AlarmCodes.Code_Error_In_System);
            }



        }

    }
}
