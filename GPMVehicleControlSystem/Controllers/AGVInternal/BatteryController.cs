using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.ViewModels.BatteryQuery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class BatteryController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Query([FromBody] clsBatQueryOptions options)
        {
            clsBatteryInfoQuery queryer = new clsBatteryInfoQuery(options);
            var results = await queryer.Query();
            results = results.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
            return Ok(results);
        }

        /// <summary>
        /// 控制充電迴路
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        [HttpGet("RechargeSwitch")]
        public async Task<IActionResult> RechargeSwitch(bool enabled)
        {
            bool result = await StaStored.CurrentVechicle.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.Recharge_Circuit, enabled);
            return Ok(result);
        }

        [HttpGet("ChargeCicuitState")]
        public async Task<IActionResult> ChargeCicuitState()
        {
            bool result = StaStored.CurrentVechicle.WagoDO.GetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.Recharge_Circuit);
            return Ok(result);
        }
    }
}
