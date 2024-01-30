
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
            Turning = 5,
            Loading = 7,
            Special = 10,
            Narrow = 12,
            Narrow_Long = 13,
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
        private LASER_MODE _Mode = LASER_MODE.Bypass;
        public LASER_MODE Spin_Laser_Mode = LASER_MODE.Turning;
        private int _CurrentLaserModeOfSick = -1;
        private DateTime _lastSickOuputPathDataUpdateDateTime = DateTime.MinValue;
        internal int CurrentLaserModeOfSick
        {
            get => _CurrentLaserModeOfSick;
            set
            {
                _lastSickOuputPathDataUpdateDateTime = DateTime.Now;
                if (_CurrentLaserModeOfSick != value)
                {
                    _CurrentLaserModeOfSick = value;
                    LOG.TRACE($"[From sick_safetyscanners topic] Laser Mode Switch to {value}");
                }
            }
        }
        public bool IsSickOutputDataNoUpdated => (DateTime.Now - _lastSickOuputPathDataUpdateDateTime).TotalSeconds > 3;

        internal int CurrentLaserModeOfDO = -1;
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


        public LASER_MODE Mode
        {
            get
            {
                try
                {
                    if (CurrentLaserModeOfSick == -1 || IsSickOutputDataNoUpdated)
                    {
                        return Enum.GetValues(typeof(LASER_MODE)).Cast<LASER_MODE>().First(mo => (int)mo == CurrentLaserModeOfDO);
                    }
                    else
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
                _output_paths_subscribe_id = _rosSocket.Subscribe<OutputPathsMsg>("/sick_safetyscanners/output_paths", SickSaftyScannerOutputDataCallback, throttle_rate: 10, queue_length: 5);
                LOG.TRACE($"Subscribe /sick_safetyscanners/output_paths({_output_paths_subscribe_id})");
            }
        }
        private void SickSaftyScannerOutputDataCallback(OutputPathsMsg sick_scanner_out_data)
        {
            CurrentLaserModeOfSick = sick_scanner_out_data.active_monitoring_case;
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


        internal async void LaserChangeByAGVDirection(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            if (direction == clsNavigation.AGV_DIRECTION.BYPASS)
            {
                LOG.INFO($"雷射設定組 =Bypass , AGVC Direction 11", true);
                await ModeSwitch(LASER_MODE.Bypass);
                return;
            }
            if (direction == clsNavigation.AGV_DIRECTION.FORWARD)
            {
                await ModeSwitch(AgvsLsrSetting);
                LOG.INFO($"雷射設定組 = {AgvsLsrSetting}", true);
                LOG.WARN($"AGVC Direction = {direction}, Laser Mode Changed to {AgvsLsrSetting}");
            }
            else // 左.右轉
            {
                await ModeSwitch(Spin_Laser_Mode);
                LOG.WARN($"AGVC Direction = {direction}, Laser Mode Changed to {Spin_Laser_Mode}");
            }
        }
        public async Task<bool> ModeSwitch(LASER_MODE mode, bool isSettingByAGVS = false)
        {
            int mode_int = (int)mode;
            if (CurrentLaserModeOfSick == mode_int || (CurrentLaserModeOfSick == 16 && mode_int == 0) || (CurrentLaserModeOfSick == 0 && mode_int == 16))
                return true;
            return await ModeSwitch((int)mode, isSettingByAGVS);
        }
        public async Task<bool> ModeSwitch(int mode_int, bool isSettingByAGVS = false)
        {

            if (OnLsrModeSwitchRequest != null)
                OnLsrModeSwitchRequest(mode_int);

            if (isSettingByAGVS)
                AgvsLsrSetting = mode_int;
            if (CurrentLaserModeOfSick == mode_int)
                return true;

            try
            {
                bool do_write_success = await DOModule.SetState(DO_ITEM.Front_Protection_Sensor_IN_1, mode_int.ToLaserDOSettingBits());
                if (!do_write_success)
                {
                    AlarmManager.AddWarning(AlarmCodes.Laser_Mode_Switch_Fail_DO_Write_Fail);
                    return false;
                }
                CurrentLaserModeOfDO = mode_int;
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                _CurrentLaserModeOfSick = 999;
                if (CurrentLaserModeOfSick != -1 && !IsSickOutputDataNoUpdated)
                {
                    while ((mode_int != 0 & mode_int != 16) ? CurrentLaserModeOfSick != mode_int : (CurrentLaserModeOfSick != 0 && CurrentLaserModeOfSick != 16))
                    {
                        if (cts.IsCancellationRequested)
                        {
                            AlarmManager.AddWarning(AlarmCodes.Laser_Mode_Switch_Fail_Timeout);
                            return false;
                        }
                        await Task.Delay(1);
                    }
                }
                LOG.INFO($"Laser Mode Chaged To : {mode_int}({Mode})", true);
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex.ToString(), ex);
                AlarmManager.AddWarning(AlarmCodes.Laser_Mode_Switch_Fail_Exception);
                return false;
            }
        }
    }
}
