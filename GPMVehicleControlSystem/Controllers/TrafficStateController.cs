using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;

namespace GPMVehicleControlSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrafficStateController : ControllerBase
    {

        private Vehicle _Vehicle => StaStored.CurrentVechicle;

        /// <summary>
        /// 接收派車系統上報之多車動態
        /// </summary>
        /// <returns></returns>
        [HttpPost("DynamicTrafficState")]
        public async Task<IActionResult> DynamicTrafficState(clsDynamicTrafficState traffic_state)
        {
            try
            {
                _Vehicle.DynamicTrafficState = traffic_state;

            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
            return Ok();
        }
        [HttpGet("SpeedDown")]
        public async Task<IActionResult> SpeedDown()
        {
            await _Vehicle.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE, SPEED_CONTROL_REQ_MOMENT.AGVS_REQUEST, false);
            return Ok(true);
        }

        [HttpGet("SpeedRecovery")]
        public async Task<IActionResult> SpeedRecovery()
        {
            await _Vehicle.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.AGVS_REQUEST, true);
            return Ok(true);
        }

        [HttpGet("TemporaryStopWhenReachTag")]
        public async Task<IActionResult> TemporaryStopWhenReachTag(int StopTag)
        {
            (bool confirm, string message) response = await _Vehicle.TryTemporaryStopWhenReachTag(StopTag);
            return Ok(new
            {
                confirm = response.confirm,
                message = response.message
            });
        }
    }
}
