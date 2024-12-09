using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace AGVSystemCommonNet6.Abstracts
{
    public abstract class Lighter
    {
        public clsDOModule DOModule { get; set; }
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
            flash_cts?.Cancel();
        }


        public async Task FlashAsync(DO_ITEM light_DO, int flash_period = 400)
        {
            flash_cts?.Cancel();  // 取消之前的闪烁任务（如果存在）
            await DOModule.SetState(light_DO, true);
            await Task.Delay(100);
            flash_cts = new CancellationTokenSource();
            bool light_active = false;
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
                LOG.TRACE("Flash Task Canceled", false);
            }
            catch (Exception ex)
            {
                LOG.TRACE("Flash Task Canceled", false);
            }
        }


        public async Task Flash(DO_ITEM[] light_DOs, int flash_period = 400)
        {
            foreach (var item in light_DOs)
            {
                await this.DOModule.SetState(item, true);
            }

            flash_cts = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                await Task.Delay(100);  // 允许取消操作的触发
                bool light_active = false;
                try
                {
                    while (true)
                    {
                        foreach (var item in light_DOs)
                        {
                            await DOModule.SetState(item, light_active);
                        }

                        await Task.Delay(flash_period, flash_cts.Token);

                        if (flash_cts.Token.IsCancellationRequested)
                        {
                            break;
                        }
                        light_active = !light_active;
                    }
                }
                catch (Exception ex)
                {
                    LOG.TRACE("Flash Task Canceled", false);
                }
            }, flash_cts.Token);
        }



        public abstract Task CloseAll(int delay_ms = 10);
        public abstract Task OpenAll();
    }
}
