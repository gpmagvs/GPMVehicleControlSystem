using GPMVehicleControlSystem.Models.VehicleControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController
    {
        [HttpGet("BatteryLockCtrl")]
        public async Task<IActionResult> BatteryLockControl(int battery_no, bool islock)
        {
            var inspAGV = (agv as TsmcMiniAGV);
            bool _result = false;
            if (battery_no == 1)
            {
                if (islock)
                    _result = await inspAGV.Battery1Lock();
                else
                    _result = await inspAGV.Battery1UnLock();
            }
            else
            {
                if (islock)
                    _result = await inspAGV.Battery2Lock();
                else
                    _result = await inspAGV.Battery2UnLock();
            }

            return Ok(_result);
        }

        public async Task<IActionResult> BatteryLockActionStop()
        {
            var _agv = (agv as DemoMiniAGV);
            _agv.BatteryLockActionStop();
            return Ok();

        }
    }
}
