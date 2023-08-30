using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
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
using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.Models.WorkStation;
using System.ComponentModel;
using Polly.Caching;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

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
        public bool IsInitialized { get; private set; } = false;
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
        internal bool Enable => AppSettingsHelper.GetValue<bool>("VCS:ForkLifer_Enable");
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

                if (!AppSettingsHelper.GetValue<bool>("VCS:SensorBypass:BeltSensorBypass"))
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
                        fork_ros_controller?.ZAxisStop();
                        if (!IsInitialing)
                            Current_Alarm_Code = AlarmCodes.Zaxis_Down_Limit;
                    }
                    else if (!state && DI?.Input == DI_ITEM.Vertical_Up_Hardware_limit)
                    {
                        fork_ros_controller?.ZAxisStop();
                        if (!IsInitialing)
                            Current_Alarm_Code = AlarmCodes.Zaxis_Up_Limit;
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

        public override void CheckStateDataContent()
        {
        }
        public async Task<(bool confirm, string message)> ForkStopAsync()
        {
            return await fork_ros_controller.ZAxisStop();
        }
        public async Task<(bool confirm, string message)> ForkPositionInit()
        {
            await HardwareLimitSaftyCheck();
            return await fork_ros_controller.ZAxisInit();
        }

        private async Task<bool> HardwareLimitSaftyCheck()
        {
            if (CurrentForkLocation == FORK_LOCATIONS.UNKNOWN)
                return await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            return true;
        }

        public async Task<(bool confirm, string message)> ForkGoHome(double speed = 1, bool wait_done = true)
        {
            await HardwareLimitSaftyCheck();
            return await fork_ros_controller.ZAxisGoHome(speed, wait_done);
        }
        public async Task<(bool confirm, string message)> ForkPose(double pose, double speed = 0.1, bool wait_done = true)
        {
            if (pose < 0)
                pose = 0;
            await HardwareLimitSaftyCheck();
            //if (pose > ForkTeachData.Up_Pose_Limit)
            //    pose = ForkTeachData.Up_Pose_Limit;
            //if (pose < ForkTeachData.Down_Pose_Limit)
            //    pose = ForkTeachData.Down_Pose_Limit;
            return await fork_ros_controller.ZAxisGoTo(pose, speed, wait_done);
        }

        public async Task<(bool confirm, string message)> ForkUpAsync(double speed = 0.1)
        {
            await HardwareLimitSaftyCheck();
            return await fork_ros_controller.ZAxisUp(speed);
        }
        public async Task<(bool confirm, string message)> ForkDownAsync(double speed = 0.1)
        {
            await HardwareLimitSaftyCheck();
            return await fork_ros_controller.ZAxisDown(speed);
        }
        public async Task<(bool confirm, string message)> ForkUpSearchAsync(double speed = 0.1)
        {
            await HardwareLimitSaftyCheck();
            return await fork_ros_controller.ZAxisUpSearch(speed);
        }

        public async Task<(bool confirm, string message)> ForkDownSearchAsync(double speed = 0.1)
        {
            await HardwareLimitSaftyCheck();
            return await fork_ros_controller.ZAxisDownSearch(speed);
        }
        /// <summary>
        /// 牙叉伸出
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> ForkExtendOutAsync()
        {
            await ForkARMStop();
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.END)
                return (true, "");

            try
            {
                await DOModule.SetState(DO_ITEM.Fork_Shortend, true);
                //已經有註冊極限Sensor輸入變化事件,到位後OFF Y輸出
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
        public async Task<(bool confirm, string message)> ForkShortenInAsync()
        {
            await ForkARMStop();
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.HOME)
                return (true, "");

            try
            {
                await DOModule.SetState(DO_ITEM.Fork_Extend, true);
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
            try
            {
                this.InitForkSpeed = InitForkSpeed;
                IsInitialized = false;
                IsInitialing = true;
                bool IsDownSearch = CurrentForkLocation != FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;

                if (IsDownSearch)
                    await ForkDownSearchAsync(InitForkSpeed);
                else
                {
                    await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);
                    await ForkUpSearchAsync(InitForkSpeed);
                }
                LOG.INFO($"Fork {(IsDownSearch ? "Down " : "Up")} Search Start");

                bool reachHome = false;
                bool leaveHome = false;
                while (true)
                {
                    if (forkAGV.Sub_Status == SUB_STATUS.DOWN)
                    {
                        return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
                    }
                    await Task.Delay(1);
                    if (!reachHome)
                    {
                        reachHome = CurrentForkLocation == FORK_LOCATIONS.HOME;
                        if (reachHome)
                            LOG.INFO($"Fork reach home first");
                    }
                    if (reachHome && CurrentForkLocation != FORK_LOCATIONS.HOME)
                    {
                        LOG.INFO($"Fork leave home ");
                        await ForkStopAsync();
                        break;
                    }
                }
                await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
                await Task.Delay(2000);
                if (IsDownSearch)
                {
                    LOG.INFO($"Home above , Fork Go To 2.5");
                    await ForkPositionInit();
                    await ForkPose(2.5, InitForkSpeed);
                }

                LOG.INFO($"Fork Shorten move to find Home Point Start");
                await Task.Delay(400);
                while (CurrentForkLocation != FORK_LOCATIONS.HOME)
                {
                    Thread.Sleep(1000);
                    if (forkAGV.Sub_Status == SUB_STATUS.DOWN)
                    {
                        return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
                    }
                    var pose = Driver.CurrentPosition - 0.05;
                    LOG.INFO($"Fork Shorten move to find Home Point, pose commadn position is {pose}");
                    var response = await ForkPose(pose, InitForkSpeed);
                    LOG.INFO($"{response.confirm},{response.message}");
                }

                IsInitialized = CurrentForkLocation == FORK_LOCATIONS.HOME;
                IsInitialing = false;
                if (IsInitialized)
                {
                    (bool confirm, string message) response = (false, "");
                    CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    while (!response.confirm)
                    {
                        await Task.Delay(200);
                        if (cancellation.IsCancellationRequested)
                            return (false, AlarmCodes.Action_Timeout);
                        response = await ForkPositionInit();
                        LOG.INFO($"Fork cmd : init response: {response.confirm}<{response.message}>");
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
        /// <param name="layer">第N層(Zero-base)</param>
        /// <param name="position">該層之上/下位置</param>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<(bool success, AlarmCodes alarm_code)> ForkGoTeachedPoseAsync(int tag, int layer, FORK_HEIGHT_POSITION position, double speed)
        {
            try
            {
                if (!StationDatas.TryGetValue(tag, out clsWorkStationData? workStation))
                    return (false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_Tag);

                if (!workStation.LayerDatas.TryGetValue(layer, out clsStationLayerData? teach))
                    return (false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_layer);
                (bool confirm, string message) forkMoveREsult = (false, "");

                double position_to_reach = 0;

                if (position == FORK_HEIGHT_POSITION.UP_)
                    position_to_reach = teach.Up_Pose;
                if (position == FORK_HEIGHT_POSITION.DOWN_)
                    position_to_reach = teach.Down_Pose;

                int tryCnt = 0;
                double positionError = 0;
                double errorTorlence = 0.5;

                LOG.WARN($"Tag:{tag},{position} {position_to_reach}");
                bool belt_sensor_bypass = AppSettingsHelper.GetValue<bool>("VCS:SensorBypass:BeltSensorBypass");

                while ((positionError = Math.Abs(Driver.CurrentPosition - position_to_reach)) > errorTorlence)
                {
                    if (!belt_sensor_bypass && !DIModule.GetState(DI_ITEM.Vertical_Belt_Sensor) && !DOModule.GetState(DO_ITEM.Vertical_Belt_SensorBypass))
                        return (false, AlarmCodes.Belt_Sensor_Error);

                    if (forkAGV.Sub_Status == SUB_STATUS.DOWN)
                    {
                        LOG.WARN($"Tag:{tag},{position} AGV Status Not RUN ,Break Try ");
                        return (false, AlarmCodes.None);
                    }
                    Thread.Sleep(1);
                    tryCnt++;
                    LOG.WARN($"Tag:{tag},{position} Error:{positionError}_Try-{tryCnt}");
                    forkMoveREsult = await ForkPose(position_to_reach, speed);

                    if (!forkMoveREsult.confirm && tryCnt > 2)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                    else if (positionError > errorTorlence && tryCnt > 2)
                    {
                        return (false, AlarmCodes.Fork_Height_Setting_Error);
                    }
                }

                return (true, AlarmCodes.None);

            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return (false, AlarmCodes.Code_Error_In_System);
            }



        }
    }
}
