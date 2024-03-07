using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsDirectionLighter : Lighter
    {
        internal delegate bool NormalMoveOpenLigherdelegate();
        internal NormalMoveOpenLigherdelegate OnAGVDirectionChangeToForward;
        public clsDirectionLighter() : base()
        {
        }

        public clsDirectionLighter(clsDOModule dOModule) : base(dOModule)
        {
        }

        public override async void CloseAll(int delay_ms = 10)
        {
            try
            {
                await Task.Delay(delay_ms);
                AbortFlash();
                await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
                await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
                await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
                await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
              
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
        }

        public override async void OpenAll()
        {
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, true);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, true);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, true);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, true);
        }

        public virtual async void TurnRight(bool opened = true)
        {
            CloseAll();
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, true);
            if (opened)
                FlashAsync(DO_ITEM.AGV_DiractionLight_Right);
            else
            {
                AbortFlash();
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            }
        }
        public virtual async void TurnLeft(bool opened = true)
        {
            CloseAll();
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, true);
            if (opened)
                FlashAsync(DO_ITEM.AGV_DiractionLight_Left);
            else
            {
                AbortFlash();
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
            }
        }

        public async virtual void Forward(bool opened = true)
        {
            await Task.Delay(500);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, opened);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
        }
        public async virtual void Backward(bool opened = true, int delay = 500)
        {
            await Task.Delay(delay);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, opened);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            await this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
        }

        internal void WaitPassLights(int interval = 1000)
        {
            Flash(new DO_ITEM[] { DO_ITEM.AGV_DiractionLight_Right, DO_ITEM.AGV_DiractionLight_Left }, interval);
        }

        internal void LightSwitchByAGVDirection(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            CloseAll();

            if (direction == clsNavigation.AGV_DIRECTION.FORWARD)
            {
                if (OnAGVDirectionChangeToForward != null)
                {
                    if (OnAGVDirectionChangeToForward())
                        Forward();
                }
            }
            else if (direction == clsNavigation.AGV_DIRECTION.RIGHT)
                TurnRight();
            else if (direction == clsNavigation.AGV_DIRECTION.LEFT)
                TurnLeft();
            else if (direction == clsNavigation.AGV_DIRECTION.STOP)
                CloseAll();

        }

    }
}
