using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.RDTEST;
using GPMVehicleControlSystem.Models.TaskExecute;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.ViewModels.RDTEST;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class RDTESTController : ControllerBase
    {
        Vehicle agv;
        public RDTESTController()
        {
            agv = StaStored.CurrentVechicle;
        }
        [HttpPost("MoveTest")]
        public async Task<IActionResult> MoveTestStart(clsMoveTestModel options)
        {
            if (agv.Remote_Mode == REMOTE_MODE.ONLINE || agv.Operation_Mode == OPERATOR_MODE.AUTO || agv.GetSub_Status() != SUB_STATUS.IDLE)
            {
                return Ok(new { result = false, message = "AGV必須在Offline、手動模式且狀態為IDLE的狀態方可執行測試" });
            }
            StaRDTestManager.StartMoveTest(options);
            return Ok(new { result = true });
        }
        [HttpPost("MoveTest/Stop")]
        public async Task<IActionResult> MoveTestStop()
        {
            StaRDTestManager.StopMoveTest();
            return Ok();
        }

        [HttpPost("AlarmTriggerTest")]
        public async Task AlarmTriggerTest(AlarmCodes alarmCode)
        {
            agv.SoftwareEMO(alarmCode);
        }
        [HttpPost("CheckCargoTest")]
        public async Task CheckCargoTest(int tag)
        {
            await UnloadTask.WaitOperatorCheckCargoStatusProcess(tag);
        }

        [HttpPost("AGVSDisConnectedSimulation")]
        public async Task AGVSDisConnectedSimulation(bool simulationOn)
        {
            agv.AGVS.disConnectedSimulation = simulationOn;
        }


        [HttpGet("NavStateUpdateTimeoutSimulation")]
        public async Task NavStateUpdateTimeoutSimulation(bool simulationOn)
        {
            agv.Navigation.NavStateUpdateTimeoutSimulation = simulationOn;
        }
    }
}
