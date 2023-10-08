using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.TASK;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GPMVehicleControlSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskDispatchController : ControllerBase
    {
        Vehicle Agv => StaStored.CurrentVechicle;
        [HttpPost("Execute")]
        public async Task<IActionResult> Execute([FromBody] object taskDto)
        {
            TaskDownloadRequestResponse task_download_feedback = new TaskDownloadRequestResponse();
            clsTaskDownloadData? data = JsonConvert.DeserializeObject<clsTaskDownloadData>(taskDto.ToString());
            TASK_DOWNLOAD_RETURN_CODES return_code = Agv.AGVSTaskDownloadConfirm(data);
            task_download_feedback.ReturnCode = return_code;
            if (return_code == TASK_DOWNLOAD_RETURN_CODES.OK)
            {
                Agv.ExecuteAGVSTask(this, data);
            }
            return Ok(task_download_feedback);
        }
        [HttpPost("Cancel")]
        public async Task<IActionResult> CancelTask([FromBody] clsCancelTaskCmd cancelCmd)
        {
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
            return Ok(reply);
        }
    }
}
