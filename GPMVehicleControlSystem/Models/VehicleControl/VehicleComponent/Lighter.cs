using GPMVehicleControlSystem.Models.VehicleControl.DIOModule;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using NLog;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace AGVSystemCommonNet6.Abstracts
{
    public abstract class Lighter
    {
        public clsDOModule DOModule { get; set; }
        public Logger logger = LogManager.GetCurrentClassLogger();
        public Lighter()
        {
        }

        protected Lighter(clsDOModule dOModule)
        {
            DOModule = dOModule;
        }

        CancellationTokenSource flash_cts = new CancellationTokenSource();

        public void AbortFlash()
        {
            try
            {
                flash_cts?.Cancel();
                flash_cts?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        /// <summary>
        /// 交替閃滅
        /// </summary>
        /// <returns></returns>
        public async Task TwoLightChangedOnFlashAsync(DO_ITEM light1, DO_ITEM light2, int flash_period = 500)
        {
            AbortFlash();
            await Task.Delay(300);
            await DOModule.SetState(light1, false);
            await DOModule.SetState(light2, false);
            bool _active = true;
            flash_cts = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await DOModule.SetState(light1, _active);
                        await DOModule.SetState(light2, !_active);
                        await Task.Delay(flash_period, flash_cts.Token);
                        _active = !_active;
                    }
                    catch (Exception)
                    {
                        return;
                    }

                }

            });

        }

        public async Task FlashAsync(DO_ITEM light_DO, int flash_period = 400)
        {

            AbortFlash();
            await Task.Delay(500);
            await DOModule.SetState(light_DO, true);
            await Task.Delay(100);
            flash_cts = new CancellationTokenSource();
            bool light_active = false;
            await Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        await Task.Delay(flash_period, flash_cts.Token);
                        await DOModule.SetState(light_DO, light_active);
                        light_active = !light_active;
                        if (flash_cts.IsCancellationRequested)
                        {
                            break;  // 如果收到取消请求，则退出循环
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.Trace("Flash Task Canceled", false);
                }
                catch (Exception ex)
                {
                    logger.Trace("Flash Task Canceled", false);
                }
            });

        }


        public async Task Flash(DO_ITEM[] light_DOs, int on_period = 400, int off_period = 500)
        {
            AbortFlash();
            await Task.Delay(300);
            flash_cts = new CancellationTokenSource();
            DOWriteRequest request = new DOWriteRequest(light_DOs.GetIOSignalOfModule().Select(i => new DOModifyWrapper(i, false)));
            await DOModule.SetState(request);

            await Task.Run(async () =>
            {
                await Task.Delay(100);
                bool light_active = true;
                try
                {
                    while (true)
                    {
                        request = new DOWriteRequest(light_DOs.GetIOSignalOfModule().Select(i => new DOModifyWrapper(i, light_active)));
                        await DOModule.SetState(request);
                        await Task.Delay(light_active ? on_period : off_period, flash_cts.Token);
                        if (flash_cts.Token.IsCancellationRequested)
                            break;
                        light_active = !light_active;
                    }
                }
                catch (Exception ex)
                {
                    logger.Trace("Flash Task Canceled", false);
                }
            }, flash_cts.Token);
        }



        public abstract Task CloseAll(int delay_ms = 10);
        public abstract Task OpenAll();
    }
}
