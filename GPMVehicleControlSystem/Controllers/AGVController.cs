using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
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
        private ILogger<clsAGVSConnection> logger;
        public AGVController()
        {
            this.agv = StaStored.CurrentVechicle;
            logger = agv.AGVS.logger;
        }
        private async void LogAsync(string api_name, string method = "GET")
        {
            await Task.Factory.StartNew(() =>
            {
                logger.LogTrace($"({method}) api route= /api/AGV/{api_name}");
            });
        }
        private async void LogResponseAsync(string api_name, string method = "GET", object response = null)
        {
            await Task.Factory.StartNew(() =>
            {
                logger.LogTrace($"({method}) api route= /api/AGV/{api_name} {(response == null ? "" : $"Response={response.ToJson(Newtonsoft.Json.Formatting.None)}")} ");
            });
        }
        [HttpGet("RunningState")]
        public async Task<IActionResult> GetRunningStatus()
        {
            LogAsync("RunningState");
            var state = agv.HandleWebAPIProtocolGetRunningStatus();
            var response = state.ToJson();
            LogResponseAsync("RunningState", response: response);
            return Ok(response);
        }

        [HttpGet("OnlineState")]
        public async Task<IActionResult> GetOnlineState()
        {
            LogAsync("OnlineState");
            LogResponseAsync("agv_online", response: agv.Remote_Mode);
            return Ok(agv.Remote_Mode);
        }


        [HttpGet("agv_online")]
        public async Task<IActionResult> agv_online()
        {
            //if (agv.Sub_Status != clsEnums.SUB_STATUS.IDLE && agv.Sub_Status != clsEnums.SUB_STATUS.Charging)
            //{
            //    return Ok(new { ReturnCode = 4231, Message = $"當前狀態不可上線({agv.Sub_Status})" });
            //}
            LogAsync("agv_online");
            bool success = agv.HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE, IsAGVSRequest: true);
            var response = new
            {
                ReturnCode = success ? 0 : RETURN_CODE.NG,
                Message = "",
                Success = success
            };
            LogResponseAsync("agv_online", response: response);
            return Ok(response);
        }


        [HttpGet("agv_offline")]
        public async Task<IActionResult> agv_offline()
        {
            LogAsync("agv_offline");
            bool success = agv.HandleRemoteModeChangeReq(REMOTE_MODE.OFFLINE, IsAGVSRequest: true);
            var response = new
            {
                ReturnCode = success ? 0 : RETURN_CODE.NG,
                Success = success,
                Message = ""
            };
            LogResponseAsync("agv_offline", response: response);
            return Ok(response);
        }


        /// <summary>
        /// 定位
        /// </summary>
        /// <returns></returns>
        [HttpPost("Localization")]
        public async Task<IActionResult> Localization([FromBody] clsLocalizationVM localization)
        {
            LogAsync("Localization");
            var result = await agv.Localization((ushort)localization.currentID, localization.x, localization.y);
            var response = new { Success = result.confirm, Message = result.message };
            LogResponseAsync("agv_offline", response: response);
            return Ok(response);
        }

    }
}
