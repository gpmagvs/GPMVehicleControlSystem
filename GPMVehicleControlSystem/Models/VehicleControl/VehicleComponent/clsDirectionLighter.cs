using AGVSystemCommonNet6.Abstracts;
using GPMVehicleControlSystem.Models.VehicleControl.DIOModule;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using NLog;
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

        public override async Task CloseAll(int delay_ms = 10)
        {
            try
            {
                await Task.Delay(delay_ms);
                AbortFlash();
                DOWriteRequest writeRequest = new DOWriteRequest(new List<DOModifyWrapper>()
                                                    {
                                                         new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Front.GetIOSignalOfModule(), false),
                                                         new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Back.GetIOSignalOfModule(), false),
                                                         new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Right.GetIOSignalOfModule(), false),
                                                         new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Left.GetIOSignalOfModule(), false),
                                                    });
                await DOModule.SetState(writeRequest);


            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public override async Task OpenAll()
        {
            DOWriteRequest writeRequest = new DOWriteRequest(new List<DOModifyWrapper>()
            {
                 new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Front.GetIOSignalOfModule(), true),
                 new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Back.GetIOSignalOfModule(), true),
                 new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Right.GetIOSignalOfModule(), true),
                 new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Left.GetIOSignalOfModule(), true),
            });
            await DOModule.SetState(writeRequest);
        }

        public virtual async void TurnRight(bool opened = true)
        {
            CloseAll();
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

            DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Back.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Right.GetIOSignalOfModule(),  false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Left.GetIOSignalOfModule(),  false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Front.GetIOSignalOfModule(),  opened),
                    });
            await DOModule.SetState(request);
        }
        public async virtual void Backward(bool opened = true, int delay = 500)
        {
            await Task.Delay(delay);


            DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Front.GetIOSignalOfModule(), false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Right.GetIOSignalOfModule(),  false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Left.GetIOSignalOfModule(),  false),
                        new DOModifyWrapper(DO_ITEM.AGV_DiractionLight_Back.GetIOSignalOfModule(),  opened),
                    });
            await DOModule.SetState(request);
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
            else if (direction == clsNavigation.AGV_DIRECTION.REACH_GOAL)
                CloseAll();

        }

    }
}
