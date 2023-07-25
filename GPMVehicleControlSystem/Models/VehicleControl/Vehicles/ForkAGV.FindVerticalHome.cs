using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using System.Diagnostics;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class ForkAGV
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

        public FORK_LOCATIONS CurrentForkLocation
        {
            //TODO 
            get
            {
                if (WagoDI.GetState(DI_ITEM.Vertical_Home_Pos))
                    return FORK_LOCATIONS.HOME;
                else if (!WagoDI.GetState(DI_ITEM.Vertical_Up_Hardware_limit))
                    return FORK_LOCATIONS.UP_HARDWARE_LIMIT;
                else if (WagoDI.GetState(DI_ITEM.Vertical_Up_Pose))
                    return FORK_LOCATIONS.UP_POSE;
                else if (!WagoDI.GetState(DI_ITEM.Vertical_Down_Hardware_limit))
                    return FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;
                else if (WagoDI.GetState(DI_ITEM.Vertical_Down_Pose))
                    return FORK_LOCATIONS.DOWN_POSE;
                else return FORK_LOCATIONS.UNKNOWN;
            }
        }
        /// <summary>
        /// 初始化Fork , 尋找原點
        /// </summary>
        public async Task<(bool done, AlarmCodes alarm_code)> ForkInitialize()
        {
            try
            {
                await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);

                //尋找下點位位置(在Home點的下方 _ cm)
                async Task<(bool find, AlarmCodes alarm_code)> SearchDownLimitPose()
                {
                    try
                    {
                        if (CurrentForkLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
                            return (true, AlarmCodes.None);
                        else
                        {
                            await ForkAGVC.ZAxisDownSearch(); //向下搜尋
                        }
                        bool Upsearching = false;
                        while (CurrentForkLocation != FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)//TODO 確認是A/B接 
                        {
                            if (Sub_Status != AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                                return (false, AlarmCodes.UserAbort_Initialize_Process);
                            await Task.Delay(1);
                        }
                        await ForkAGVC.ZAxisStop();

                    }
                    catch (Exception ex)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                    return (true, AlarmCodes.None);

                }
                async Task<(bool find, AlarmCodes alarm_code)> SearchHomePose()//使用吋動的方式
                {
                    try
                    {
                        while (CurrentForkLocation != FORK_LOCATIONS.HOME)
                        {
                            Thread.Sleep(500);

                            if (Sub_Status != AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                                return (false, AlarmCodes.None);
                            var pose = VerticalDriverState.CurrentPosition - 0.1;
                            await ForkAGVC.ZAxisGoTo(pose);
                            if (CurrentForkLocation == FORK_LOCATIONS.DOWN_POSE && !Debugger.IsAttached)
                                return (false, AlarmCodes.Action_Timeout);
                        }
                        return (true, AlarmCodes.None);
                    }
                    catch (Exception)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                }

                (bool find, AlarmCodes alarm_code) searchDonwPoseResult = await SearchDownLimitPose();
                if (!searchDonwPoseResult.find)
                    return searchDonwPoseResult;

                (bool success, string message) result = await ForkAGVC.ZAxisInit(); //將當前位置暫時設為原點(0)
                if (!result.success)
                    throw new Exception();

                bool do_set_success = await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);  //bypass 垂直軸硬體極限防護
                result = await ForkAGVC.ZAxisGoTo(7, wait_done: true); //移動到上方五公分
                await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);  //重新啟動垂直軸硬體極限防護

                if (!result.success)
                    throw new Exception();

                (bool found, AlarmCodes alarm_code) findHomeResult = await SearchHomePose();
                if (findHomeResult.found)
                {
                    result = await ForkAGVC.ZAxisInit();
                    if (!result.success)
                        throw new Exception();
                }
                return findHomeResult;
            }
            catch (Exception)
            {
                await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);  //重新啟動垂直軸硬體極限防護
                return (false, AlarmCodes.Action_Timeout);
            }
        }

    }
}
