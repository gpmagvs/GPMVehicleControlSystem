using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static AGVSystemCommonNet6.AGVDispatch.Messages.clsTaskDownloadData;

namespace GPMVehicleControlSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskDispatchController : ControllerBase
    {
        private async void LogAsync(string api_name, object body = null, string method = "GET")
        {
            await Task.Factory.StartNew(() =>
            {
                string bodyJson = body == null ? "" : body.ToJson();
                Agv.AGVS.LogMsgFromAGVS($"({method}) api route= /api/TaskDispatch/{api_name},body={bodyJson}");
            });
        }
        private async void LogResponseAsync(string api_name, object response = null, string method = "GET")
        {
            await Task.Factory.StartNew(() =>
            {
                string bodyJson = response == null ? "" : response.ToJson();
                Agv.AGVS.LogMsgToAGVS($"({method}) api route= /api/TaskDispatch/{api_name},Response={bodyJson}");
            });
        }
        Vehicle Agv => StaStored.CurrentVechicle;
        [HttpPost("Execute")]
        public async Task<IActionResult> Execute([FromBody] object taskDto)
        {
            TaskDownloadRequestResponse task_download_feedback = new TaskDownloadRequestResponse();
            clsTaskDownloadData? data = JsonConvert.DeserializeObject<clsTaskDownloadData>(taskDto.ToString());
            data.IsLocalTask = false;
            LogAsync("Execute", data, method: "POST");

            TASK_DOWNLOAD_RETURN_CODES return_code = Agv.AGVSTaskDownloadConfirm(data);

            task_download_feedback.ReturnCode = return_code;
            if (return_code == TASK_DOWNLOAD_RETURN_CODES.OK)
            {
                Agv.ExecuteAGVSTask(this, data);
            }
            LogResponseAsync("Execute", task_download_feedback);
            return Ok(task_download_feedback);
        }
        [HttpPost("Cancel")]
        public async Task<IActionResult> CancelTask([FromBody] clsCancelTaskCmd cancelCmd)
        {
            LogAsync("Cancel", cancelCmd, method: "POST");
            SimpleRequestResponse reply = new SimpleRequestResponse()
            {
                ReturnCode = RETURN_CODE.OK
            };
            try
            {
                Agv.HandleAGVSTaskCancelRequest(cancelCmd.ResetMode);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
            LogResponseAsync("Cancel", reply);
            return Ok(reply);
        }

        [HttpPost("OrderInfo")]
        public async Task<IActionResult> OrderInfo([FromBody] clsOrderInfo OrderInfo)
        {
            LogAsync("Cancel", OrderInfo, method: "POST");
            try
            {
                if (Agv.Parameters.OrderInfoFetchSource == Vehicle.ORDER_INFO_FETCH_SOURCE.FROM_CIM_POST_IN)
                    Agv.orderInfoViewModel = OrderInfo;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
            LogResponseAsync("Cancel", true);
            return Ok(true);
        }
    }
}
