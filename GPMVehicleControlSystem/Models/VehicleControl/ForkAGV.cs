using AGVSystemCommonNet6.Log;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl
{
    public class ForkAGV : SubmarinAGV
    {
        public ForkAGV()
        {
        }
        public override async Task<bool> ResetMotor()
        {
            try
            {
                base.ResetMotor();
                WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, true);
                await Task.Delay(100);
                WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, false);
                return true;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex.Message, ex);
                return false;
            }
        }
    }
}
