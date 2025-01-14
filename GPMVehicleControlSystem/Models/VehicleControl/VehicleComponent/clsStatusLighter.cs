using AGVSystemCommonNet6.Abstracts;
using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsStatusLighter : Lighter
    {
        private Debouncer _Debouncer = new Debouncer();
        public clsStatusLighter(clsDOModule DOModule) : base(DOModule)
        {

        }

        public override async Task CloseAll(int delay_ms = 10)
        {
            AbortFlash();
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
        }

        public override async Task OpenAll()
        {
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, true);
            await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, true);
        }
        public async void RUN()
        {
            _Debouncer.Debounce(async () =>
            {
                try
                {
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
                }
                catch (Exception ex)
                {

                }
            }, 300);
        }
        public async void DOWN()
        {
            _Debouncer.Debounce(async () =>
            {
                try
                {
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, true);
                }
                catch (Exception ex)
                {

                }
            }, 300);
        }
        public async void IDLE()
        {
            _Debouncer.Debounce(async () =>
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
            }, 300);
        }
        public async Task ONLINE()
        {
            DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, true);
        }
        public async Task OFFLINE()
        {

            DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
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
