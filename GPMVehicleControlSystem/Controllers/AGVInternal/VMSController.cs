
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl;
using GPMVehicleControlSystem.Models.Buzzer;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using static AGVSystemCommonNet6.clsEnums;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using Microsoft.AspNetCore.Identity;
using GPMVehicleControlSystem.Models.VCSSystem;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static SQLite.SQLite3;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{

    [Route("api/[controller]")]
    [ApiController]
    public partial class VMSController : ControllerBase
    {

        private Vehicle agv => StaStored.CurrentVechicle;
      
        [HttpGet("Where_r_u")]
        public async Task Where_r_u()
        {
            await Task.Delay(1);
            _ = Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    agv.DirectionLighter.OpenAll();
                    Thread.Sleep(150);
                    agv.DirectionLighter.CloseAll();
                    Thread.Sleep(150);
                }
            });
            return;
        }

        [HttpPost("ResetAlarm")]
        public async Task<IActionResult> ResetAlarm()
        {
            BuzzerPlayer.Stop();
            await Task.Delay(1);
            await agv.ResetAlarmsAsync(false);
            return Ok("OK");
        }

        [HttpPost("ClearAlarm")]
        public async Task<IActionResult> ClearAlarm(int alarm_code)
        {
            AlarmManager.ClearAlarm(alarm_code);
            return Ok("OK");
        }
        [HttpGet("AutoMode")]
        public async Task<IActionResult> AutoModeSwitch(OPERATOR_MODE mode)
        {
            if (mode == OPERATOR_MODE.MANUAL && agv.Sub_Status == SUB_STATUS.RUN)
            {
                return Ok(new
                {
                    Success = false,
                    Message = "AGV執行任務中不可切為手動模式"
                });
            }
            bool confirm = await agv.Auto_Mode_Siwtch(mode);
            return Ok(new
            {
                Success = confirm,
                Message = ""
            });
        }

        [HttpGet("OnlineMode")]
        public async Task<IActionResult> OnlineModeSwitch(REMOTE_MODE mode)
        {
            try
            {
                LOG.WARN($"車載用戶請求AGV {mode}");
                (bool success, RETURN_CODE return_code) result = await agv.Online_Mode_Switch(mode);
                string _message = "";
                if (result.return_code == RETURN_CODE.AGV_Need_Park_Above_Tag)
                    _message = "AGV必須停在TAG上";
                else if (result.return_code == RETURN_CODE.Current_Tag_Cannot_Online)
                    _message = $"此位置(TAG {agv.BarcodeReader.CurrentTag})禁止AGV上線";
                else if (result.return_code == RETURN_CODE.Cannot_Switch_Remote_Mode_When_Task_Executing)
                    _message = "AGV執行任務中不可切換Online/Offline Mode";
                else
                    _message = result.return_code.ToString();
                return Ok(new
                {
                    Success = result.success,
                    Message = _message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Success = false,
                    Message = $"Code Error:{ex.Message}"
                });
            }
        }


        [HttpGet("ROSConnected")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ROSConnected()
        {
            await Task.Delay(1);
            return Ok(StaStored.CurrentVechicle.AGVC.IsConnected());
        }

        [HttpGet("Mileage")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Mileage()
        {
            await Task.Delay(1);
            return Ok(agv.Odometry);
        }
        [HttpGet("BateryState")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> BateryState()
        {
            await Task.Delay(1);
            if (agv.Batteries.Count == 0)
                return Ok(new BatteryState
                {
                    batteryLevel = 0,
                });
            return Ok(agv.Batteries.Values.First().Data);
        }

        [HttpPost("EMO")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> EMO()
        {
            try
            {
                agv.SoftwareEMO(AlarmCodes.SoftwareEMS);
                StaSysMessageManager.AddNewMessage("Software EMO !", 2);
            }
            catch (Exception ex)
            {
                StaSysMessageManager.AddNewMessage(ex.Message, 1);

            }
            return Ok("OK");
        }

        [HttpPost("Initialize")]
        public async Task<IActionResult> Initialize()
        {
            var result = await agv.Initialize();
            return Ok(new { confirm = result.confirm, message = result.message });
        }


        [HttpGet("CancelInitProcess")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> CancelInitProcess()
        {
            await Task.Delay(1);
            await agv.CancelInitialize();
            //bool setTagIDSuccess = await VMSEntity.Initializer.Initial_Robot_Pose_with_Tag();
            return Ok(true);
        }


        [HttpPost("BuzzerOff")]
        public async Task<IActionResult> BuzzerOFF()
        {
            await Task.Delay(1);
            BuzzerPlayer.Stop();
            return Ok("OK");
        }

        [HttpPost("RemoveCassette")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> RemoveCassette()
        {
            await Task.Delay(1);
            if (agv.Parameters.AgvType == AGV_TYPE.INSPECTION_AGV)
                return Ok(false);

            var retcode = await (agv as SubmarinAGV).RemoveCstData();
            return Ok(retcode == RETURN_CODE.OK);
        }




        [HttpGet("LaserMode")]
        public async Task<IActionResult> LaserMode(int mode)
        {
            await Task.Delay(1);
            await agv.Laser.ModeSwitch(mode);
            return Ok();
        }



        [HttpGet("TriggerCSTReader")]
        public async Task<IActionResult> TriggerCSTReader()
        {
            (bool request_success, bool action_done) ret = await agv.AGVC.TriggerCSTReader();
            string barcode = "ERROR";
            if (ret.action_done)
            {
                barcode = agv.CSTReader.Data.data;
            }
            return Ok(new { barcode });
        }


        [HttpGet("StopCSTReader")]
        public async Task<IActionResult> StopCSTReader()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                (bool request_success, bool action_done) ret = await agv.AGVC.AbortCSTReader();
            });
            return Ok();
        }

        [HttpGet("RunningStatus")]
        public async Task<IActionResult> GetRunningStatus()
        {
            return Ok(agv.HandleWebAPIProtocolGetRunningStatus());
        }

        [HttpGet("FindTagCenter")]
        public async Task<IActionResult> FindTagCenter()
        {
            (bool confirm, string message) response = await agv.TrackingTagCenter(90);
            return Ok(new
            {
                confirm = response.confirm,
                message = response.message
            });
        }


        [HttpGet("RechargeCircuit")]
        public async Task<IActionResult> RechargeCircuit()
        {
            bool open = !agv.WagoDO.GetState(DO_ITEM.Recharge_Circuit);
            bool success = await agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, open);
            if (success)
                StaSysMessageManager.AddNewMessage($"充電迴路已{(open ? "開啟" : "關閉")}");
            else
                StaSysMessageManager.AddNewMessage($"充電迴路{(open ? "開啟" : "關閉")}失敗", 1);
            return Ok(success);
        }
    }
}
