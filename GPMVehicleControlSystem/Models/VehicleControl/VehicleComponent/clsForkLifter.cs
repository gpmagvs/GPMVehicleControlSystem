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
using GPMVehicleControlSystem.Models.ForkTeach;
using Newtonsoft.Json;
using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.ViewModels.ForkTeach;

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
            LoadTeachDataSettingFromJsonConfigs();
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
                if (!DIModule.GetState(DI_ITEM.Fork_Extend_Exist_Sensor))
                    return FORK_ARM_LOCATIONS.HOME;
                else if (!DIModule.GetState(DI_ITEM.Fork_Short_Exist_Sensor))
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
        /// <summary>
        /// 可以走的上極限位置
        /// </summary>


        public override COMPOENT_NAME component_name => COMPOENT_NAME.VERTIVAL_DRIVER;
        public clsDriver Driver { get; set; }
        public override string alarm_locate_in_name => "FORK";

        public clsDOModule DOModule { get; set; }
        private clsDIModule _DIModule;
        public clsForkTeach ForkTeachData { get; set; } = new clsForkTeach();

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
                _DIModule.OnEMO += OnEmoButtonPushHandler;
            }
        }

        private void OnEmoButtonPushHandler(object? sender, EventArgs e)
        {
            ForkARMStop();
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
                        Current_Alarm_Code = AlarmCodes.Zaxis_Down_Limit;
                    }
                    else if (!state && DI?.Input == DI_ITEM.Vertical_Up_Hardware_limit)
                    {
                        fork_ros_controller?.ZAxisStop();
                        Current_Alarm_Code = AlarmCodes.Zaxis_Up_Limit;
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

        internal ForkAGVController fork_ros_controller;

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
            if (pose > ForkTeachData.Up_Pose_Limit)
                pose = ForkTeachData.Up_Pose_Limit;
            if (pose < ForkTeachData.Down_Pose_Limit)
                pose = ForkTeachData.Down_Pose_Limit;
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
        public async Task<bool> ForkExtendOutAsync()
        {
            await ForkARMStop();
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.END)
                return true;

            try
            {
                await DOModule.SetState(DO_ITEM.Fork_Extend, true);
                //已經有註冊極限Sensor輸入變化事件,到位後OFF Y輸出
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 牙叉縮回
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ForkShortenInAsync()
        {
            await ForkARMStop();
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.HOME)
                return true;

            try
            {
                await DOModule.SetState(DO_ITEM.Fork_Shortend, true);
                return true;
            }
            catch (Exception)
            {
                return false;
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
        public async Task<(bool done, AlarmCodes alarm_code)> ForkInitialize()
        {
            IsInitialized = false;
            try
            {

                if (CurrentForkARMLocation != FORK_ARM_LOCATIONS.HOME)
                    await ForkShortenInAsync();

                //尋找下點位位置(在Home點的下方 _ cm)
                async Task<(bool find, AlarmCodes alarm_code, bool isFindHome)> SearchDownLimitPose()
                {
                    bool isFindHome = false;
                    try
                    {
                        if (CurrentForkLocation == FORK_LOCATIONS.HOME)
                            return (true, AlarmCodes.None, true);
                        if (CurrentForkLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
                            return (true, AlarmCodes.None, false);
                        else
                        {
                            await fork_ros_controller.ZAxisDownSearch(); //向下搜尋
                        }
                        bool Upsearching = false;
                        while (CurrentForkLocation != FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)//TODO 確認是A/B接 
                        {
                            Thread.Sleep(1);
                            if (CurrentForkLocation == FORK_LOCATIONS.HOME)
                            {
                                isFindHome = true;
                                break;
                            }
                        }
                        await fork_ros_controller.ZAxisStop();

                    }
                    catch (Exception ex)
                    {
                        return (false, AlarmCodes.Action_Timeout, false);
                    }
                    return (true, AlarmCodes.None, isFindHome);

                }
                async Task<(bool find, AlarmCodes alarm_code)> SearchHomePose()//使用吋動的方式
                {
                    try
                    {
                        while (CurrentForkLocation != FORK_LOCATIONS.HOME)
                        {
                            Thread.Sleep(500);
                            var pose = Driver.CurrentPosition - 0.1;
                            await fork_ros_controller.ZAxisGoTo(pose);
                        }
                        return (true, AlarmCodes.None);
                    }
                    catch (Exception)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                }

                await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
                bool isHomeReachFirst = false;
                if (CurrentForkLocation != FORK_LOCATIONS.HOME)
                {

                    (bool find, AlarmCodes alarm_code, bool isFindHome) searchDonwPoseResult = await SearchDownLimitPose();
                    if (!searchDonwPoseResult.find)
                        return (searchDonwPoseResult.find, searchDonwPoseResult.alarm_code);
                    isHomeReachFirst = searchDonwPoseResult.isFindHome;

                    if (!isHomeReachFirst)
                    {
                        await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);  //
                        _ = Task.Factory.StartNew(async () =>
                        {
                            await Task.Delay(1000);
                            await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);  //重新啟動垂直軸硬體極限防護
                        });

                    }
                }
                else
                    isHomeReachFirst = true;

                (bool success, string message) Initresult = await fork_ros_controller.ZAxisInit(); //將當前位置暫時設為原點(0)
                if (!Initresult.success)
                    throw new Exception();

                var result = await fork_ros_controller.ZAxisGoTo(isHomeReachFirst ? 1 : 7.0, wait_done: true); //移動到上方五公分
                if (!result.success)
                    throw new Exception();

                (bool found, AlarmCodes alarm_code) findHomeResult = await SearchHomePose();
                if (findHomeResult.found)
                {
                    result = await fork_ros_controller.ZAxisInit();
                    if (!result.success)
                        throw new Exception();

                    IsInitialized = true;
                }
                return findHomeResult;
            }
            catch (Exception)
            {
                await DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);  //重新啟動垂直軸硬體極限防護
                return (false, AlarmCodes.Action_Timeout);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag">工位的TAG</param>
        /// <param name="layer">第N層(Zero-base)</param>
        /// <param name="position">該層之上/下位置</param>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<(bool success, AlarmCodes alarm_code)> ForkGoTeachedPoseAsync(int tag, int layer, FORK_HEIGHT_POSITION position)
        {
            try
            {

                if (!ForkTeachData.Teaches.TryGetValue(tag, out Dictionary<int, clsTeachData>? layerTeaches))
                    return (false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_Tag);

                if (!layerTeaches.TryGetValue(layer, out clsTeachData? teach))
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

                //
                while ((positionError = Math.Abs(Driver.CurrentPosition - position_to_reach)) > errorTorlence)
                {
                    Thread.Sleep(1);
                    tryCnt++;
                    LOG.WARN($"Tag:{tag},{position} Error:{positionError}_Try-{tryCnt}");
                    forkMoveREsult = await ForkPose(position_to_reach, 0.5);

                    if (!forkMoveREsult.confirm && tryCnt > 5)
                    {

                        return (false, AlarmCodes.Action_Timeout);
                    }
                    else if (positionError> errorTorlence && tryCnt>5)
                    {
                        return (false, AlarmCodes.Fork_Height_Setting_Error);
                    }
                }

                return (true, AlarmCodes.None);

            }
            catch (Exception ex)
            {
                throw ex;
            }



        }
    }
}
