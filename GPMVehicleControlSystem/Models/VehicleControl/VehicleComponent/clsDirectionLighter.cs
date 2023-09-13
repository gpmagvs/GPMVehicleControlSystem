using AGVSystemCommonNet6.Abstracts;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsDirectionLighter : Lighter
    {
        public bool FrontLighterFlashWhenNormlMove = false;
        public clsDirectionLighter() : base()
        {
        }

        public clsDirectionLighter(clsDOModule dOModule) : base(dOModule)
        {
        }

        public override async void CloseAll()
        {
            try
            {
                AbortFlash();
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
            }
            catch (Exception ex)
            {
            }
        }

        public override void OpenAll()
        {
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, true);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, true);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, true);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, true);
        }

        public virtual void TurnRight(bool opened = true)
        {
            CloseAll();
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, true);
            if (opened)
                Flash(DO_ITEM.AGV_DiractionLight_Right);
            else
            {
                AbortFlash();
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            }
        }
        public virtual void TurnLeft(bool opened = true)
        {
            CloseAll();
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, true);
            if (opened)
                Flash(DO_ITEM.AGV_DiractionLight_Left);
            else
            {
                AbortFlash();
                this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
            }
        }

        public async virtual void Forward(bool opened = true)
        {
            await Task.Delay(500);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, opened);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
        }
        public async virtual void Backward(bool opened = true, int delay = 500)
        {
            await Task.Delay(delay);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Front, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Back, opened);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
            this.DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
        }

        internal void WaitPassLights(int interval = 1000)
        {
            Flash(new DO_ITEM[] { DO_ITEM.AGV_DiractionLight_Right, DO_ITEM.AGV_DiractionLight_Left }, interval);
        }

        internal void LightSwitchByAGVDirection(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            CloseAll();

            if (direction == clsNavigation.AGV_DIRECTION.FORWARD && FrontLighterFlashWhenNormlMove)
                Forward();
            else if (direction == clsNavigation.AGV_DIRECTION.RIGHT)
                TurnRight();
            else if (direction == clsNavigation.AGV_DIRECTION.LEFT)
                TurnLeft();
            else if (direction == clsNavigation.AGV_DIRECTION.STOP)
                CloseAll();

        }

    }
}
