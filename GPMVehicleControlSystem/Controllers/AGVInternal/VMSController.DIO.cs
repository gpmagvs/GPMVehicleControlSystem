using GPMVehicleControlSystem.Models.Emulators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

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

        [HttpGet("DIO/SetAGVHsSignalState")]
        [ApiExplorerSettings(IgnoreApi = false)]
        public async Task<IActionResult> SetAGVHsSignalState(string signal_name, bool state)
        {
            await Task.Delay(1);
            string address = "";
            switch (signal_name)
            {

                #region agv
                case "AGV_VALID":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_VALID.ToString()).Address;
                    break;
                case "AGV_READY":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_READY.ToString()).Address;
                    break;
                case "AGV_TR_REQ":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_TR_REQ.ToString()).Address;
                    break;
                case "AGV_BUSY":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_BUSY.ToString()).Address;
                    break;
                case "AGV_COMPT":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_COMPT.ToString()).Address;
                    break;

                #endregion

                default:
                    break;
            }
            agv.WagoDO.SetState(address, state);
            return Ok();
        }


        [HttpGet("DIO/SetEQHsSignalState")]
        [ApiExplorerSettings(IgnoreApi = false)]
        public async Task<IActionResult> SetEQHsSignalState(string signal_name, bool state)
        {
            if (agv.EQ_HS_Method != EQ_HS_METHOD.EMULATION)
            {
                return Ok("不可修改DI訊號");
            }

            await Task.Delay(1);
            string address = "";
            switch (signal_name)
            {
                case "EQ_READY":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.EMU_EQ_READY.ToString()).Address;
                    break;
                case "EQ_BUSY":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.EMU_EQ_BUSY.ToString()).Address;

                    break;
                case "EQ_L_REQ":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.EMU_EQ_L_REQ.ToString()).Address;

                    break;
                case "EQ_U_REQ":
                    address = agv.WagoDO.VCSOutputs.First(output => output.Name == VehicleControl.DIOModule.clsDOModule.DO_ITEM.EMU_EQ_U_REQ.ToString()).Address;

                    break;

                default:
                    break;
            }
            agv.WagoDO.SetState(address, state);
            return Ok();
        }
    }
}
