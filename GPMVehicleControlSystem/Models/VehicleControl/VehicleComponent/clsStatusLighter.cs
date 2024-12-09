using AGVSystemCommonNet6.Abstracts;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsStatusLighter : Lighter
    {
        private SemaphoreSlim seSlim = new SemaphoreSlim(1, 1);
        public clsStatusLighter(clsDOModule DOModule) : base(DOModule)
        {

        }

        public override async Task CloseAll(int delay_ms = 10)
        {
            try
            {
                await seSlim.WaitAsync();
                AbortFlash();
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                seSlim.Release();
            }
        }

        public override async Task OpenAll()
        {
            try
            {
                await seSlim.WaitAsync();
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, true);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, true);
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, true);
            }
            catch
            {

            }
            finally
            {
                seSlim.Release();
            }
        }
        public async void RUN()
        {
            try
            {
                await seSlim.WaitAsync();
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false).ContinueWith(async t =>
                {
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false).ContinueWith(async t =>
                    {
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
                    });
                });
            }
            catch
            {

            }
            finally
            {
                seSlim.Release();
            }
        }
        public async void DOWN()
        {
            try
            {
                await seSlim.WaitAsync();
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false).ContinueWith(async t =>
                {
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false).ContinueWith(async t =>
                    {
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, true);
                    });
                });
            }
            catch (Exception ex)
            {

            }
            finally
            {
                seSlim.Release();
            }
        }
        public async void IDLE()
        {
            try
            {
                await seSlim.WaitAsync();
                await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false).ContinueWith(async t =>
                {
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false).ContinueWith(async t =>
                    {
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, true);
                    });
                });
            }
            catch (Exception ex)
            {
            }
            finally
            {
                seSlim.Release();
            }
        }
        public async Task ONLINE()
        {
            try
            {
                await seSlim.WaitAsync();
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, true);
            }
            catch (Exception ex)
            {
            }
            finally
            {
                seSlim.Release();
            }
        }
        public async Task OFFLINE()
        {
            try
            {
                await seSlim.WaitAsync();
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
            }
            catch (Exception ex)
            {
            }
            finally
            {
                seSlim.Release();
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
