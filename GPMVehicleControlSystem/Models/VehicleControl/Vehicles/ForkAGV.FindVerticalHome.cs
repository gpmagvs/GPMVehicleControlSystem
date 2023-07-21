using AGVSystemCommonNet6.Alarm.VMS_ALARM;
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
                else if (WagoDI.GetState(DI_ITEM.Vertical_Up_Hardware_limit))
                    return FORK_LOCATIONS.UP_HARDWARE_LIMIT;
                else if (WagoDI.GetState(DI_ITEM.Vertical_Up_Pose))
                    return FORK_LOCATIONS.UP_POSE;
                else if (WagoDI.GetState(DI_ITEM.Vertical_Down_Hardware_limit))
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
                //尋找下點位位置(在Home點的下方 _ cm)
                async Task<(bool find, AlarmCodes alarm_code)> SearchDownPose()
                {
                    try
                    {
                        if (CurrentForkLocation == FORK_LOCATIONS.DOWN_POSE) //已經在Down Pose
                            return (true, AlarmCodes.None);

                        if (CurrentForkLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
                            await ForkAGVC.ZAxisUpSearch();
                        else
                        {
                            bool do_set_success = await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, true);  //bypass 垂直軸硬體極限防護
                            if (!do_set_success)
                                return (false, AlarmCodes.Wago_IO_Write_Fail);

                            await ForkAGVC.ZAxisDownSearch(); //向下搜尋
                        }
                        while (CurrentForkLocation != FORK_LOCATIONS.DOWN_POSE)//TODO 確認是A/B接 
                        {
                            await Task.Delay(1);
                            if (CurrentForkLocation == FORK_LOCATIONS.DOWN_HARDWARE_LIMIT)
                            {
                                await ForkAGVC.ZAxisStop();
                                await ForkAGVC.ZAxisUpSearch(); //改為向上搜尋

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                    return (true, AlarmCodes.None);

                }
                async Task<(bool find, AlarmCodes alarm_code)> SearchHomePose()
                {
                    try
                    {
                        double current_position = ForkState.CurrentPosition;
                        while (CurrentForkLocation != FORK_LOCATIONS.HOME)
                        {
                            await Task.Delay(1);
                            await ForkAGVC.ZAxisGoTo(current_position - 0.1);
                            if (CurrentForkLocation == FORK_LOCATIONS.DOWN_POSE)
                                return (false, AlarmCodes.Action_Timeout);
                        }
                        return (true, AlarmCodes.None);
                    }
                    catch (Exception)
                    {
                        return (false, AlarmCodes.Action_Timeout);
                    }
                }

                (bool find, AlarmCodes alarm_code) searchDonwPoseResult = await SearchDownPose();
                if (!searchDonwPoseResult.find)
                    return searchDonwPoseResult;

                (bool success, string message) result = await ForkAGVC.ZAxisInit(); //將當前位置暫時設為原點(0)
                if (!result.success)
                    throw new Exception();

                result = await ForkAGVC.ZAxisGoTo(5, wait_done: true); //移動到上方五公分
                if (!result.success)
                    throw new Exception();

                (bool found, AlarmCodes alarm_code) findHomeResult = await SearchHomePose();
                await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);  //重新啟動垂直軸硬體極限防護

                return (findHomeResult.found, AlarmCodes.None);
            }
            catch (Exception)
            {
                await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);  //重新啟動垂直軸硬體極限防護
                return (false, AlarmCodes.Action_Timeout);
            }
        }

    }
}
