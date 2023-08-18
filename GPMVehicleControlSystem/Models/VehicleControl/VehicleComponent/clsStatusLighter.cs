using AGVSystemCommonNet6.Abstracts;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsStatusLighter : Lighter
    {
        public clsStatusLighter(clsDOModule DOModule) : base(DOModule)
        {

        }

        public override async void CloseAll()
        {
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
        }

        public override async void OpenAll()
        {
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, true);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, true);
        }
        public async void RUN()
        {
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
        }
        public async void DOWN()
        {
            try
            {
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, true);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
            }
            catch (Exception ex)
            {

            }
        }
        public async void IDLE()
        {
            try
            {
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, true);
            }
            catch (Exception ex)
            {
            }
        }
        public void ONLINE()
        {
            try
            {
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, true);
            }
            catch (Exception ex)
            {
            }
        }
        public void OFFLINE()
        {
            try
            {
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
            }
            catch (Exception ex)
            {
            }
        }

        internal void InActiveGreen()
        {
            DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
        }

        internal void ActiveGreen()
        {
            DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
        }
    }
}
