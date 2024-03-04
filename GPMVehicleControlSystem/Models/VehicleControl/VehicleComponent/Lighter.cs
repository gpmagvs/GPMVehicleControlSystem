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

        public void Flash(DO_ITEM light_DO, int flash_period = 400)
        {
            Flash(new DO_ITEM[] { light_DO }, flash_period);
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



        public abstract void CloseAll(int delay_ms = 10);
        public abstract void OpenAll();
    }
}
