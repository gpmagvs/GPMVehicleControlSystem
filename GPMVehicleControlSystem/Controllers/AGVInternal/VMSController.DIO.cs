using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController : ControllerBase
    {

        [HttpGet("DIO/DO_State")]
        [ApiExplorerSettings(IgnoreApi = false)]
        public async Task<IActionResult> DO_State(string address, bool state)
        {
            await Task.Delay(1);
            agv.WagoDO.SetState(address, state);
            return Ok(true);
        }

        [HttpGet("DIO/DI_State")]
        [ApiExplorerSettings(IgnoreApi = false)]
        public async Task<IActionResult> DI_State(string address, bool state)
        {
            await Task.Delay(1);
            agv.WagoDI.SetState(address, state);
            return Ok();
        }

        [HttpGet("DIO/SetHsSignalState")]
        [ApiExplorerSettings(IgnoreApi = false)]
        public async Task<IActionResult> SetHsSignalState(string signal_name, bool state)
        {
            await Task.Delay(1);
            string address = "";
            switch (signal_name)
            {
                case "EQ_READY":
                    address = agv.WagoDI.VCSInputs.First(INPUT => INPUT.Input == VehicleControl.DIOModule.clsDIModule.DI_ITEM.EQ_READY).Address;
                    break;
                case "EQ_BUSY":
                    address = agv.WagoDI.VCSInputs.First(INPUT => INPUT.Input == VehicleControl.DIOModule.clsDIModule.DI_ITEM.EQ_READY).Address;
                    break;
                case "EQ_L_REQ":
                    address = agv.WagoDI.VCSInputs.First(INPUT => INPUT.Input == VehicleControl.DIOModule.clsDIModule.DI_ITEM.EQ_READY).Address;
                    break;
                case "EQ_U_REQ":
                    address = agv.WagoDI.VCSInputs.First(INPUT => INPUT.Input == VehicleControl.DIOModule.clsDIModule.DI_ITEM.EQ_READY).Address;
                    break;

                #region agv
                case "AGV_VALID":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Output == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_VALID).Address;
                    break;
                case "AGV_READY":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Output == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_READY).Address;
                    break;
                case "AGV_TR_REQ":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Output == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_TR_REQ).Address;
                    break;
                case "AGV_BUSY":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Output == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_BUSY).Address;
                    break;
                case "AGV_COMPT":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Output == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_COMPT).Address;
                    break;

                #endregion

                default:
                    break;
            }
            agv.WagoDI.SetState(address, state);
            return Ok();
        }
    }
}
