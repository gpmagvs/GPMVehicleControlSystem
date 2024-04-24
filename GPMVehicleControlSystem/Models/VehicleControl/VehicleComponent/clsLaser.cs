
using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient;
using System.Reflection;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsLaser : IDIOUsagable, IRosSocket
    {
        public enum LASER_MODE
        {
            Bypass = 0,
            Normal = 1,
            Secondary = 2,
            Move_Short = 3,
            Unknow_4 = 4,
            Turning = 5,
            Unknow_6 = 6,
            Loading = 7,
            Unknow_8 = 8,
            Unknow_9 = 9,
            Special = 10,
            Unknow_11 = 11,
            Narrow = 12,
            Narrow_Long = 13,
            Unknow_14 = 14,
            Unknow_15 = 15,
            Bypass16 = 16,
            Unknow = 444
        }

        /// <summary>
        /// AGVS站點雷射預設定植
        /// </summary>
        public enum AGVS_LASER_SETTING_ORDER
        {
            BYPASS,
            NORMAL,
        }
        public GeneralSystemStateMsg SickSsystemState { get; set; } = new GeneralSystemStateMsg();
        public LASER_MODE Spin_Laser_Mode = LASER_MODE.Turning;
        protected SemaphoreSlim modeSwitchSemaphoresSlim = new SemaphoreSlim(1, 1);
        private int _CurrentLaserModeOfSick = -1;
        internal int CurrentLaserModeOfSick
        {
            get => _CurrentLaserModeOfSick;
            set
            {
                if (_CurrentLaserModeOfSick != value)
                {
                    _CurrentLaserModeOfSick = value;
                    LOG.INFO($"Laser Mode Chaged To : {value}({Mode})", true);
                }
            }
        }
        private int _AgvsLsrSetting = 1;
        public delegate void LsrModeSwitchDelegate(int mode);
        public LsrModeSwitchDelegate OnLsrModeSwitchRequest;
        public clsDOModule DOModule { get; set; }
        public clsDIModule DIModule { get; set; }
        public int AgvsLsrSetting
        {
            get => _AgvsLsrSetting;
            set
            {
                if (_AgvsLsrSetting != value)
                {
                    _AgvsLsrSetting = value;
                    Console.WriteLine($"變更雷射預設組[AGVS 設定]");
                }
            }
        }
        public clsLaser(clsDOModule DOModule, clsDIModule DIModule)
        {
            this.DOModule = DOModule;
            this.DIModule = DIModule;
        }


        public virtual LASER_MODE Mode
        {
            get
            {
                try
                {
                    return Enum.GetValues(typeof(LASER_MODE)).Cast<LASER_MODE>().First(mo => (int)mo == CurrentLaserModeOfSick);
                }
                catch (Exception)
                {
                    return LASER_MODE.Unknow;
                }
            }
        }

        private RosSocket _rosSocket = null;
        private string _output_paths_subscribe_id;

        public RosSocket rosSocket
        {
            get => _rosSocket;
            set
            {
                try
                {

                    if (_output_paths_subscribe_id != null)
                        _rosSocket?.Unsubscribe(_output_paths_subscribe_id);
                }
                catch (Exception ex)
                {
                    LOG.ERROR(ex.Message, ex);
                }
                _rosSocket = value;
                _output_paths_subscribe_id = _rosSocket.Subscribe<OutputPathsMsg>("/sick_safetyscanners/output_paths", SickSaftyScannerOutputDataCallback);
                LOG.TRACE($"Subscribe /sick_safetyscanners/output_paths({_output_paths_subscribe_id})");
            }
        }

        private DateTime lastSickOutputPathDataUpdateTime = DateTime.MinValue;
        private bool isSickOutputPathDataNotUpdate
        {
            get
            {
                double period = (DateTime.Now - lastSickOutputPathDataUpdateTime).TotalSeconds;
                LOG.TRACE($"{period},lastSickOutputPathDataUpdateTime:{lastSickOutputPathDataUpdateTime.ToString("yyyy-MM-dd HH:mm:ss.ffff")}");
                return period > 1;
            }
        }
        private void SickSaftyScannerOutputDataCallback(OutputPathsMsg sick_scanner_out_data)
        {
            CurrentLaserModeOfSick = sick_scanner_out_data.active_monitoring_case;
            lastSickOutputPathDataUpdateTime = DateTime.Now;
        }

        internal async Task FrontBackLasersEnable(bool front_active, bool back_active)
        {
            await DOModule.SetState(DO_ITEM.Front_LsrBypass, !front_active);
            await DOModule.SetState(DO_ITEM.Back_LsrBypass, !back_active);
        }
        /// <summary>
        /// 前後雷射Bypass關閉
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task FrontBackLasersEnable(bool active)
        {
            await DOModule.SetState(DO_ITEM.Front_LsrBypass, !active);
            await DOModule.SetState(DO_ITEM.Back_LsrBypass, !active);
        }
        /// <summary>
        /// 前後雷射Bypass關閉
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task SideLasersEnable(bool active)
        {
            await DOModule.SetState(DO_ITEM.Right_LsrBypass, !active);
            await DOModule.SetState(DO_ITEM.Left_LsrBypass, !active);
        }
        /// <summary>
        /// 前後左右雷射Bypass全部關閉
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task AllLaserActive()
        {
            await DOModule.SetState(DO_ITEM.Front_LsrBypass, false);
            await DOModule.SetState(DO_ITEM.Back_LsrBypass, false);
            await DOModule.SetState(DO_ITEM.Right_LsrBypass, false);
            await DOModule.SetState(DO_ITEM.Left_LsrBypass, false);
        }


        /// <summary>
        /// 前後左右雷射Bypass全部開啟
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task AllLaserDisable()
        {
            await DOModule.SetState(DO_ITEM.Front_LsrBypass, true);
            await DOModule.SetState(DO_ITEM.Back_LsrBypass, true);
            await DOModule.SetState(DO_ITEM.Right_LsrBypass, true);
            await DOModule.SetState(DO_ITEM.Left_LsrBypass, true);
        }

        internal async void ApplyAGVSLaserSetting()
        {
            LOG.INFO($"雷射組數切換為AGVS Setting={AgvsLsrSetting}", false);
            await ModeSwitch(AgvsLsrSetting);
        }

        public clsNavigation.AGV_DIRECTION agvDirection { get; internal set; } = clsNavigation.AGV_DIRECTION.FORWARD;
        internal virtual async void LaserChangeByAGVDirection(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            if (direction == clsNavigation.AGV_DIRECTION.BYPASS)
            {
                LOG.INFO($"雷射設定組 =Bypass , AGVC Direction 11", true);
                //await FrontBackLasersEnable(false);
                await ModeSwitch(LASER_MODE.Bypass);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    if (agvDirection == clsNavigation.AGV_DIRECTION.LEFT || agvDirection == clsNavigation.AGV_DIRECTION.RIGHT)
                        await ModeSwitch(LASER_MODE.Turning);
                    else
                        await ModeSwitch(AgvsLsrSetting);
                });

                return;
            }
            if (direction == clsNavigation.AGV_DIRECTION.FORWARD)
            {
                await FrontBackLasersEnable(true);
                await ModeSwitch(AgvsLsrSetting);
                LOG.INFO($"雷射設定組 = {AgvsLsrSetting}", true);
                LOG.WARN($"AGVC Direction = {direction}, Laser Mode Changed to {AgvsLsrSetting}");
            }
            else // 左.右轉
            {
                await FrontBackLasersEnable(true);
                await ModeSwitch(Spin_Laser_Mode);
                LOG.WARN($"AGVC Direction = {direction}, Laser Mode Changed to {Spin_Laser_Mode}");
            }
        }
        public virtual async Task<bool> ModeSwitch(LASER_MODE mode, bool isSettingByAGVS = false)
        {
            return await ModeSwitch((int)mode, isSettingByAGVS);
        }
        public async Task<bool> ModeSwitch(int mode_int, bool isSettingByAGVS = false)
        {
            try
            {
                await modeSwitchSemaphoresSlim.WaitAsync();
                bool isModeAllowSetting = mode_int == (int)LASER_MODE.Turning || mode_int == (int)LASER_MODE.Bypass;
                if ((agvDirection == clsNavigation.AGV_DIRECTION.RIGHT || agvDirection == clsNavigation.AGV_DIRECTION.LEFT) && !isModeAllowSetting)
                {
                    LOG.Critical($"AGV旋轉中,雷射切換為 =>{mode_int} 請求已被Bypass");
                    return true;
                }
                if (isSettingByAGVS)
                    AgvsLsrSetting = mode_int;
                int retry_times_limit = 300;
                int try_count = 0;
                bool[] writeBools = mode_int.ToLaserDOSettingBits();
                while (true)
                {
                    await Task.Delay(10);
                    bool success = !IsLaserModeNeedChange(mode_int);
                    if (success)
                    {
                        break;
                    }
                    LOG.WARN($"Try Laser Output Setting  as {mode_int} --({try_count})");
                    if (try_count > retry_times_limit)
                        return false;
                    bool writeSuccess = await DOModule.SetState(DO_ITEM.Front_Protection_Sensor_IN_1, writeBools);

                    if (isSickOutputPathDataNotUpdate && writeSuccess)
                    {
                        _CurrentLaserModeOfSick = mode_int;
                        break;
                    }

                    try_count++;
                }
                if (isSickOutputPathDataNotUpdate)
                    AlarmManager.AddWarning(AlarmCodes.Laser_Mode_Switch_But_SICK_OUPUT_NOT_UPDATE);
                LOG.INFO($"Laser Output Setting as {mode_int} Success({try_count})");

                return true;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                return false;
            }
            finally
            {
                modeSwitchSemaphoresSlim.Release();
            }

        }

        private bool IsLaserModeNeedChange(int toSetMode)
        {
            if (isSickOutputPathDataNotUpdate)
                return true;
            if (toSetMode == 0 || toSetMode == 16)
            {
                return CurrentLaserModeOfSick != 16 && CurrentLaserModeOfSick != 0;
            }
            else
            {
                return toSetMode != CurrentLaserModeOfSick;
            }
        }
    }
}
