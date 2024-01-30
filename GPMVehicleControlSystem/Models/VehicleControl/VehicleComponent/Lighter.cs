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
            Flash(new DO_ITEM[] { light_DO }, flash_period);
        }

        public async void Flash(DO_ITEM[] light_DOs, int flash_period = 400)
        {
            Thread _flash_thred = new Thread(async () =>
            {
                foreach (var item in light_DOs)
                    await DOModule.SetState(item, true);
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

            flash_cts = new CancellationTokenSource();
            _flash_thred.IsBackground = false;
            _flash_thred.Start();

        }


        public abstract void CloseAll(int delay_ms = 10);
        public abstract void OpenAll();
    }
}
