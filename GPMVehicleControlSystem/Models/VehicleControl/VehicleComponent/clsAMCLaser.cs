using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsAMCLaser : clsLaser
    {

        public enum AMC_LASER_MODE
        {
            Bypass = 0,
            Normal = 1,
            Normal2 = 2,
            Unknow_3 = 3,
            Bay4 = 4,
            Turning = 5,
            Turning6 = 6,
            Unknow_7 = 7,
            Unknow_8 = 8,
            Bay9 = 9,
            Unknow_10 = 10,
            Charge = 11,
            LeftShift = 12,
            RightShift = 13,
            ObstacleGoBack = 14,
            Bypass15 = 15,
            Bypass16 = 16,
            Unknow = 444
        }
        public clsAMCLaser(clsDOModule DOModule, clsDIModule DIModule) : base(DOModule, DIModule)
        {
            logger.Info($"AMC Laser instance created!");
        }

        public new AMC_LASER_MODE Mode
        {
            get
            {
                try
                {
                    return Enum.GetValues(typeof(AMC_LASER_MODE)).Cast<AMC_LASER_MODE>().First(mo => (int)mo == CurrentLaserModeOfSick);
                }
                catch (Exception)
                {
                    return AMC_LASER_MODE.Unknow;
                }
            }
        }

        private Dictionary<clsNavigation.AGV_DIRECTION, AMC_LASER_MODE> DirectionLaserMap = new Dictionary<clsNavigation.AGV_DIRECTION, AMC_LASER_MODE>() {
            { clsNavigation.AGV_DIRECTION.BYPASS ,AMC_LASER_MODE.Bypass16 },
            { clsNavigation.AGV_DIRECTION.LEFT_TRANSVERSE ,AMC_LASER_MODE.LeftShift },
            { clsNavigation.AGV_DIRECTION.RIGHT_TRANSVERSE ,AMC_LASER_MODE.RightShift },
            { clsNavigation.AGV_DIRECTION.LEFT ,AMC_LASER_MODE.Turning6 },
            { clsNavigation.AGV_DIRECTION.RIGHT,AMC_LASER_MODE.Turning6 },
            { clsNavigation.AGV_DIRECTION.BACKWARD_OBSTACLE,AMC_LASER_MODE.ObstacleGoBack },
        };
        internal override async void LaserChangeByAGVDirection(int lastVisitTag, clsNavigation.AGV_DIRECTION direction)
        {

            if (direction == clsNavigation.AGV_DIRECTION.FORWARD)
            {
                (bool succeess, int currentModeInt) = await ModeSwitchAGVSMapSettingOfCurrentTag(lastVisitTag);
                logger.Info($"[AMC]雷射設定組 = {currentModeInt}", true);
                logger.Warn($"AGVC Direction = {direction}, Laser Mode Changed to {currentModeInt}");
            }
            else
            {
                if (DirectionLaserMap.TryGetValue(direction, out var laserMode))
                {
                    await ModeSwitch(laserMode);
                    await SideLaserModeSwitch(laserMode);
                    logger.Info($"[AMC]雷射設定組 = {laserMode}", true);
                    logger.Info($"AGVC Direction = {direction}, Laser Mode Changed to {laserMode}");
                }
                else
                {
                    logger.Warn($"AGVC Direction = {direction} But Laser Mode Not Defined!!!!");

                }
            }
        }
        public override async Task<(bool succeess, int currentModeInt)> ModeSwitchAGVSMapSettingOfCurrentTag(int tag)
        {
            (bool success, int mode) = await base.ModeSwitchAGVSMapSettingOfCurrentTag(tag);
            success = await SideLaserModeSwitch(mode);
            return (success, mode);
        }
        public async Task<bool> SideLaserModeSwitch(int mode_int)
        {
            try
            {
                await modeSwitchSemaphoresSlim.WaitAsync();
                bool[] writeBools = mode_int.ToSideLaserDOSettingBits();
                int retry_times_limit = 300;
                int try_count = 0;
                while (true)
                {
                    await Task.Delay(10);
                    logger.Warn($"Try Side Laser Output Setting  as {mode_int}");
                    if (try_count > retry_times_limit)
                        return false;
                    bool writeSuccess = await DOModule.SetState(DO_ITEM.Left_Protection_Sensor_IN_1, writeBools);
                    if (writeSuccess)
                        break;
                    try_count++;
                }
                logger.Info($"Side Laser Output Setting as {mode_int} Success({try_count})");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return false;
            }
            finally
            {
                modeSwitchSemaphoresSlim.Release();
            }
        }

        public async Task<bool> SideLaserModeSwitch(AMC_LASER_MODE mode)
        {
            return await SideLaserModeSwitch((int)mode);
        }


        public new async Task<bool> ModeSwitch(AMC_LASER_MODE mode, bool isSettingByAGVS = false)
        {
            return await ModeSwitch((int)mode, isSettingByAGVS);
        }
    }
}
