using AGVSystemCommonNet6.Abstracts;
using GPMVehicleControlSystem.Models.VehicleControl.DIOModule;
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

            DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_R.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_G.GetIOSignalOfModule(),  false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_B.GetIOSignalOfModule(),  false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Y.GetIOSignalOfModule(),  false),
                    });
            await DOModule.SetState(request);
        }

        public override async Task OpenAll()
        {

            DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_R.GetIOSignalOfModule(), true),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_G.GetIOSignalOfModule(),  true),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_B.GetIOSignalOfModule(),  true),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Y.GetIOSignalOfModule(),  true),
                    });
            await DOModule.SetState(request);
        }
        public void RUN(int debunce = 300)
        {
            _Debouncer.Debounce(async () =>
            {
                try
                {
                    DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_R.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Y.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_G.GetIOSignalOfModule(), true),
                    });
                    await DOModule.SetState(request);
                }
                catch (Exception ex)
                {

                }
            }, 300);
        }
        public void DOWN()
        {
            _Debouncer.Debounce(async () =>
            {
                try
                {
                    DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_G.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Y.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_R.GetIOSignalOfModule(), true),
                    });
                    await DOModule.SetState(request);
                }
                catch (Exception ex)
                {

                }
            }, 300);
        }
        public void IDLE()
        {
            _Debouncer.Debounce(async () =>
            {
                try
                {
                    DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_R.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_G.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Y.GetIOSignalOfModule(), true),
                    });
                    await DOModule.SetState(request);
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
        internal void ActiveGreen()
        {
            DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
        }
        CancellationTokenSource lightFlowCancellationTokenSource = new CancellationTokenSource();
        internal async Task StopFlow()
        {
            try
            {
                lightFlowCancellationTokenSource.Cancel();
            }
            catch (Exception)
            {
            }
        }
        internal async Task FLowAsync(int mode = 0)
        {
            try
            {
                StopFlow();
                await Task.Delay(100);

                lightFlowCancellationTokenSource = new CancellationTokenSource();
                //R Y G B
                await CloseAll();
                while (!lightFlowCancellationTokenSource.IsCancellationRequested)
                {
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, true);
                    await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, true);
                    await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, true);
                    await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                    await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, true);
                    await Task.Delay(300, lightFlowCancellationTokenSource.Token);

                    if (mode == 0)
                    {
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                    }
                    else
                    {
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_R, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_Y, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_G, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                        await DOModule.SetState(DO_ITEM.AGV_DiractionLight_B, false);
                        await Task.Delay(300, lightFlowCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                CloseAll();
            }
        }
    }
}
