using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

namespace GPMVehicleControlSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AGVController : ControllerBase
    {

        private Vehicle agv;
        public AGVController()
        {

            this.agv = StaStored.CurrentVechicle;
        }

        [HttpGet("RunningState")]
        public async Task<IActionResult> GetRunningStatus()
        {
            var state = agv.HandleWebAPIProtocolGetRunningStatus();
            return Ok(state.ToJson());
        }

        [HttpGet("OnlineState")]
        public async Task<IActionResult> GetOnlineState()
        {
            return Ok(agv.Remote_Mode);
        }


        [HttpGet("agv_online")]
        public async Task<IActionResult> agv_online()
        {
            //if (agv.Sub_Status != clsEnums.SUB_STATUS.IDLE && agv.Sub_Status != clsEnums.SUB_STATUS.Charging)
            //{
            //    return Ok(new { ReturnCode = 4231, Message = $"當前狀態不可上線({agv.Sub_Status})" });
            //}
            agv.HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE, IsAGVSRequest: true);
            return Ok(new
            {
                ReturnCode = 0,
                Message = ""
            });
        }


        [HttpGet("agv_offline")]
        public async Task<IActionResult> agv_offline()
        {
            agv.HandleRemoteModeChangeReq(REMOTE_MODE.OFFLINE, IsAGVSRequest: true);
            return Ok(new { Success = true, Message = "" });
        }


        /// <summary>
        /// 定位
        /// </summary>
        /// <returns></returns>
        [HttpPost("Localization")]
        public async Task<IActionResult> Localization([FromBody] clsLocalizationVM localization)
        {
            var result = await (agv as TsmcMiniAGV).Localization((ushort)localization.currentID);
            return Ok(new { Success = result.confirm, Message = result.message });
        }

    }
}
