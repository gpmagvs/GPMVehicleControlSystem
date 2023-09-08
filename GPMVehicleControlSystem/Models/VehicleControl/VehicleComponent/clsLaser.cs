
using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using System.Reflection;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsLaser : IDIOUsagable
    {
        public enum LASER_MODE
        {
            Unknow = 444,
            Bypass = 0,
            Move = 1,
            Secondary = 2,
            Spin = 5,
            Loading = 7,
            Special = 10,
            Move_Short = 11,
            Spin_Shor = 12,
            Bypass16 = 16,

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
        public LASER_MODE Spin_Laser_Mode = LASER_MODE.Spin;
        internal int CurrentLaserMonitoringCase = -1;
        private int _AgvsLsrSetting = 1;
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
                    return Enum.GetValues(typeof(LASER_MODE)).Cast<LASER_MODE>().First(mo => (int)mo == CurrentLaserMonitoringCase);
                }
                catch (Exception)
                {
                    return LASER_MODE.Unknow;
                }
            }
        }

        /// <summary>
        /// 前後雷射Bypass關閉
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task FrontBackLasersEnable(bool active)
        {
            await DOModule.SetState(DO_ITEM.Front_LsrBypass, !active);
            await DOModule.SetState(DO_ITEM.Back_LsrBypass, !active);
        }   /// <summary>
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
                await ModeSwitch(LASER_MODE.Bypass, false);
                return;
            }
            if (direction == clsNavigation.AGV_DIRECTION.FORWARD)
            {
                await ModeSwitch(AgvsLsrSetting, isSettingByAGVS: false);
                LOG.INFO($"雷射設定組 = {AgvsLsrSetting}", true);
                LOG.WARN($"AGVC Direction = {direction}, Laser Mode Changed to {AgvsLsrSetting}");
            }
            else // 左.右轉
            {
                await ModeSwitch(Spin_Laser_Mode, isSettingByAGVS: false);
                LOG.WARN($"AGVC Direction = {direction}, Laser Mode Changed to {Spin_Laser_Mode}");
            }
        }
        public async Task<bool> ModeSwitch(LASER_MODE mode, bool isSettingByAGVS = false)
        {
            int mode_int = (int)mode;
            if (CurrentLaserMonitoringCase == mode_int | (CurrentLaserMonitoringCase == 16 && mode_int == 0) | (CurrentLaserMonitoringCase == 0 && mode_int == 16))
                return true;
            return await ModeSwitch((int)mode, isSettingByAGVS);
        }
        public async Task<bool> ModeSwitch(int mode_int, bool isSettingByAGVS = false)
        {
            if (isSettingByAGVS)
                AgvsLsrSetting = mode_int;
            if (CurrentLaserMonitoringCase == mode_int)
                return true;

            try
            {

                bool[] lsSet = mode_int.To4Booleans();
                bool IN_1 = lsSet[0];
                bool IN_2 = lsSet[1];
                bool IN_3 = lsSet[2];
                bool IN_4 = lsSet[3];
                bool[] writeStates = new bool[]
                {
                IN_1,!IN_1,  IN_2,!IN_2,  IN_3,!IN_3,  IN_4,!IN_4,IN_1,!IN_1,  IN_2,!IN_2,  IN_3,!IN_3,  IN_4,!IN_4,
                };
                DOModule.SetState(DO_ITEM.Front_Protection_Sensor_IN_1, writeStates);
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                if (CurrentLaserMonitoringCase != -1)
                {
                    while (CurrentLaserMonitoringCase != mode_int)
                    {
                        if (cts.IsCancellationRequested)
                        {
                            return false;
                        }
                        await Task.Delay(1);
                    }
                }
                LOG.INFO($"Laser Mode Chaged To : {mode_int}({Mode})", true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
