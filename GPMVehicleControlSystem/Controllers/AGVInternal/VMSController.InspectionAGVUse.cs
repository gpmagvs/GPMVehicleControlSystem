using GPMVehicleControlSystem.Models.VehicleControl;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController
    {
        [HttpGet("BatteryLockCtrl")]
        public async Task<IActionResult> BatteryLockControl(int battery_no, bool islock)
        {
            var inspAGV = (agv as InspectionAGV);
            if (battery_no == 1)
            {
                if (islock)
                    inspAGV.Battery1Lock();
                else
                    inspAGV.Battery1UnLock();
            }
            else
            {
                if (islock)
                    inspAGV.Battery2Lock();
                else
                    inspAGV.Battery2UnLock();
            }

            return Ok();
        }
    }
}
