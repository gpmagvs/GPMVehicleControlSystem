using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    /// <summary>
    /// 
    /// </summary>
    public class clsInspectorAGVDirectionLighter : clsDirectionLighter
    {
        public clsInspectorAGVDirectionLighter() : base()
        {

        }

        public override async Task OpenAll()
        {
            try
            {
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, true);
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left_2, true);
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, true);
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right_2, true);

            }
            catch (Exception)
            {
            }
        }

        public override async Task CloseAll(int delay_ms = 10)
        {
            try
            {
                AbortFlash();
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left, false);
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Left_2, false);
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right, false);
                DOModule.SetState(DO_ITEM.AGV_DiractionLight_Right_2, false);

            }
            catch (Exception)
            {
            }
        }

        public override void TurnRight(bool opened = true)
        {
            CloseAll();
            Flash(new DO_ITEM[2] { DO_ITEM.AGV_DiractionLight_Right, DO_ITEM.AGV_DiractionLight_Right_2 });
        }
        public override void TurnLeft(bool opened = true)
        {
            CloseAll();
            Flash(new DO_ITEM[2] { DO_ITEM.AGV_DiractionLight_Left, DO_ITEM.AGV_DiractionLight_Left_2 });
        }
        public override void Forward(bool opened = true)
        {
            CloseAll();
            Flash(new DO_ITEM[2] { DO_ITEM.AGV_DiractionLight_Right, DO_ITEM.AGV_DiractionLight_Left });
        }
        public override void Backward(bool opened = true, int delay = 500)
        {
            CloseAll();
            Flash(new DO_ITEM[2] { DO_ITEM.AGV_DiractionLight_Right_2, DO_ITEM.AGV_DiractionLight_Left_2 });
        }
    }
}
