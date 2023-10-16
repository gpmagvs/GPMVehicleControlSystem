using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace AGVSystemCommonNet6.Abstracts
{
    public abstract class Lighter
    {
        public bool AllCloseFlag = false;
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
            AllCloseFlag = true;
        }

        public void Flash(DO_ITEM light_DO, int flash_period = 400)
        {
            Task.Factory.StartNew(async () =>
            {
                await DOModule.SetState(light_DO, true);
                await Task.Delay(100);
                flash_cts = new CancellationTokenSource();
                AllCloseFlag = false;
                bool light_active = false;
                while (true)
                {
                    try
                    {
                        if (AllCloseFlag)
                            break;
                        await DOModule.SetState(light_DO, light_active);
                        await Task.Delay(flash_period, flash_cts.Token);
                        light_active = !light_active;
                    }
                    catch (Exception ex)
                    {
                        LOG.ERROR(ex);
                    }
                }
            });
        }

        public async void Flash(DO_ITEM[] light_DOs, int flash_period = 400)
        {
            foreach (var item in light_DOs)
                await this.DOModule.SetState(item, true);

            flash_cts = new CancellationTokenSource();
            _ = Task.Factory.StartNew(async () =>
            {
                await Task.Delay(100);
                AllCloseFlag = false;
                bool light_active = false;
                while (true)
                {
                    try
                    {
                        if (AllCloseFlag)
                            break;
                        foreach (var item in light_DOs)
                            await DOModule.SetState(item, light_active);
                        await Task.Delay(flash_period, flash_cts.Token);
                        light_active = !light_active;
                    }
                    catch (Exception ex)
                    {
                        LOG.ERROR(ex);
                    }
                }
            });
        }


        public abstract void CloseAll(int delay_ms = 10);
        public abstract void OpenAll();
    }
}
