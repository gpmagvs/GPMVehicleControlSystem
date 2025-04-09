using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using Microsoft.AspNetCore.Mvc;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{

    [Route("api/[controller]")]
    [ApiController]
    public partial class VMSController : ControllerBase
    {
        ILogger<VMSController> logger;
        public VMSController(ILogger<VMSController> _logger)
        {
            // help me 
            logger = _logger;
        }
        private Vehicle agv => StaStored.CurrentVechicle;

        [HttpGet("Where_r_u")]
        public async Task Where_r_u()
        {
            await Task.Delay(1);
            agv.SendNotifyierToFrontend("Where are u command send.");
            _ = Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < 2; i++)
                {
                    agv.DirectionLighter.OpenAll();
                    await Task.Delay(1000);
                    agv.DirectionLighter.CloseAll();
                    await Task.Delay(1000);
                }
            });
            return;
        }

        [HttpPost("ResetAlarm")]
        public async Task<IActionResult> ResetAlarm()
        {
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
            if (mode == OPERATOR_MODE.MANUAL && agv.GetSub_Status() == SUB_STATUS.RUN)
            {
                return Ok(new
                {
                    Success = false,
                    Message = "AGV執行任務中不可切為手動模式"
                });
            }
            if (mode == OPERATOR_MODE.MANUAL && agv.Remote_Mode == REMOTE_MODE.ONLINE)
            {
                return Ok(new
                {
                    Success = false,
                    Message = "AGV在Online模式下不可切換為手動模式(Please check 'Online Mode' to [Offline] before switch 'Auto Mode' to [Manual])"
                });
            }
            logger.LogTrace($"使用者進行嘗試切換為手/自動模式切換為 :{mode}模式");
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
                logger.LogTrace($"車載用戶請求AGV {mode}");
                (bool success, RETURN_CODE return_code) result = await agv.Online_Mode_Switch(mode);
                string _message = "";

                if (result.return_code == RETURN_CODE.AGV_Need_Park_Above_Tag)
                    _message = "AGV必須停在TAG上";
                else if (result.return_code == RETURN_CODE.Current_Tag_Cannot_Online)
                    _message = $"此位置(TAG {agv.BarcodeReader.CurrentTag})禁止AGV上線";
                else if (result.return_code == RETURN_CODE.Current_Tag_Cannot_Online_In_Equipment)
                    _message = $"AGV位於設備內(TAG {agv.BarcodeReader.CurrentTag})禁止AGV上線";
                else if (result.return_code == RETURN_CODE.Cannot_Switch_Remote_Mode_When_Task_Executing)
                    _message = "AGV執行任務中不可切換Online/Offline Mode";
                else if (result.return_code == RETURN_CODE.Current_Tag_Cannot_Online_At_Virtual_Point)
                    _message = "AGV位於虛擬點上不可上線";
                else if (result.return_code == RETURN_CODE.AGV_Not_Initialized)
                    _message = "AGV尚未完成初始化時不可上線";
                else if (result.return_code == RETURN_CODE.AGV_HasIDBut_No_Cargo)
                    _message = "有帳無料!請先進行'移除卡匣'";
                else
                    _message = result.return_code.ToString();
                return Ok(new
                {
                    Success = result.success,
                    Message = _message,
                    Code = result.success ? 0 : result.return_code
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OnlineModeSwitch Exception");
                return Ok(new
                {
                    Success = false,
                    Message = $"Code Error:{ex.Message}",
                    Code = 400,
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
                agv.SoftwareEMOFromUI();
            }
            catch (Exception ex)
            {
            }
            return Ok("OK");
        }

        [HttpPost("Initialize")]
        public async Task<IActionResult> Initialize()
        {
            logger.LogTrace($"User raise Initialize request.");

            try
            {
                if (agv.Operation_Mode != OPERATOR_MODE.MANUAL)
                    return Ok(new { confirm = false, message = "請確認 'Auto Mode' 已切換為[Manual] 後再嘗試初始化(Please confirm that 'Auto Mode' has been switched to [Manual] before attempting initialization.)" });
                if (agv.Remote_Mode == REMOTE_MODE.ONLINE)
                    return Ok(new { confirm = false, message = "請確認 'Online Mode'已切換為 [Offline] 後再嘗試初始化(Please confirm that 'Online Mode' has been switched to [Offline] before attempting initialization.)" });
            }
            finally
            {
                logger.LogTrace($"User raise Initialize");
            }
            var result = await agv.Initialize();
            logger.LogTrace($"User raise Initialize request. Result:{result}");
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
            if (agv.Parameters.AgvType != AGV_TYPE.INSPECTION_AGV)
                await agv.Laser.ModeSwitch(mode);
            else
            {
                await agv.Laser.ModeSwitch(mode);
                await (agv.Laser as clsAMCLaser).SideLaserModeSwitch(mode);
            }
            return Ok();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cargo_type">Tray=200,Rack=201</param>
        /// <returns></returns>
        [HttpGet("TriggerCSTReaderWithCargoType")]
        public async Task<IActionResult> TriggerCSTReaderWithCargoType(CST_TYPE cargo_type)
        {
            (bool request_success, bool action_done) ret = await agv.AGVC.TriggerCSTReader(cargo_type);
            try
            {
                await agv.AGVC.CSTReadServiceSemaphoreSlim.WaitAsync();
                string barcode = "ERROR";
                if (ret.action_done)
                {
                    barcode = agv.CSTReader.ValidCSTID;
                }
                return Ok(new { barcode });
            }
            catch (Exception ex)
            {
                return Ok(new { ex.Message });
            }
            finally
            {
                agv.AGVC.CSTReadServiceSemaphoreSlim.Release();
            }

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
            bool open = !agv.IsChargeCircuitOpened;
            bool success = await agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, open);
            return Ok(success);
        }


        [HttpGet("GetWorkstations")]
        public async Task<IActionResult> GetWorkstations()
        {
            return Ok(agv.WorkStations.Stations.Select(w => new { w.Value.Name, Tag = w.Key, w.Value.ModbusTcpPort }));
        }

        [HttpPost("WorkstationModbusIOTest")]
        public async Task<IActionResult> WorkstationModbusIOTest(int Tag)
        {
            if (agv.WorkStations.Stations.TryGetValue(Tag, out var data))
            {
                clsEQHandshakeModbusTcp.HandshakingModbusTcpProcessCancel?.Cancel();
                await Task.Delay(1000);
                var modbusTcp = new clsEQHandshakeModbusTcp(agv.Parameters.ModbusIO, Tag, data.ModbusTcpPort);
                if (!modbusTcp.Start(agv.AGVS, agv.AGVHsSignalStates, agv.EQHsSignalStates))
                    return Ok(new { confirm = false, message = $"無法連線({modbusTcp.IP}:{modbusTcp.Port})" });
                return Ok(new { confirm = true, message = "" });
            }
            else
            {
                return Ok(new { confirm = false, message = $"Tag-{Tag} 工位配置不存在!" });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd">0:速度恢復 1:減速 2:停止 3:STOP_CALCULATE_PATH_CLOSE  100:STOP_WHEN_REACH_GOAL</param>
        /// <returns></returns>
        [HttpGet("SpeedControl")]
        public async Task<IActionResult> CarSpeedControl(ROBOT_CONTROL_CMD cmd)
        {
            await agv.AGVC.CarSpeedControl(cmd, SPEED_CONTROL_REQ_MOMENT.UNKNOWN);
            return Ok();
        }

        [HttpGet("AGVStatusChangeToRunWhenLaserRecovery")]
        public async Task<IActionResult> AGVStatusChangeToRunWhenLaserRecovery()
        {
            agv.AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.UNKNOWN);
            return Ok();
        }

        [HttpGet("LDULDWithoutEntryControl")]
        public async Task<IActionResult> LDULDWithoutEntryControl(bool actived)
        {
            logger.LogTrace($"Remote user try change LDULD_Task_No_Entry to {actived}");
            if (!actived)
            {
                if (agv.Parameters.AgvType != AGV_TYPE.INSPECTION_AGV)
                {
                    var retcode = await (agv as SubmarinAGV).RemoveCstData();
                }
            }
            agv.Parameters.LDULD_Task_No_Entry = actived;
            logger.LogTrace($"Remote user change LDULD_Task_No_Entry to {actived}!!");
            return Ok(0);
        }
        [HttpGet("WorkStationData")]
        public async Task<IActionResult> GetWorkStationData()
        {
            return Ok(agv.WorkStations);
        }
        [HttpGet("SetSubStatus")]
        public async Task SetSubStatus(SUB_STATUS status)
        {
            agv.SetSub_Status(status);
        }

        [HttpGet("DownloadEQHsSettings")]
        public async Task<IActionResult> DownloadEQHsSettings()
        {
            return Ok(StaStored.CurrentVechicle.WorkStations.Stations);
            return Ok(StaStored.CurrentVechicle.Parameters);
        }

        [HttpPost("SaveEQHsSettings")]
        public async Task<IActionResult> SaveEQHsSettings([FromBody] List<clsWorkStationData> configurations)
        {
            try
            {
                Dictionary<int, Dictionary<int, clsStationLayerData>> odlLayoutDats = StaStored.CurrentVechicle.WorkStations.Stations.ToDictionary(opt => opt.Key, opt => opt.Value.LayerDatas);
                StaStored.CurrentVechicle.WorkStations.Stations = configurations.ToDictionary(c => c.Tag, c => c);

                foreach (var item in odlLayoutDats)
                {
                    if (StaStored.CurrentVechicle.WorkStations.Stations.ContainsKey(item.Key))
                    {
                        StaStored.CurrentVechicle.WorkStations.Stations[item.Key].LayerDatas = item.Value;
                    }
                }
                StaStored.CurrentVechicle.SaveTeachDAtaSettings();
                return Ok(new
                {
                    confirm = true,
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    confirm = false,
                    message = $"Save EQHs Settings Error({ex.Message})",
                });
            }
        }

        [HttpPost("CargoStatusManualCheckDone")]
        public async Task CargoStatusManualCheckDone(string userName = "")
        {
            agv.ManualCheckCargoStatusDone(userName);
        }

        [HttpPost("CargoStatusManualCheckDoneWhenUnloadFailure")]
        public async Task CargoStatusManualCheckDoneWhenUnloadFailure(string userName = "")
        {
            agv.CargoStatusManualCheckDoneWhenUnloadFailure(userName);
        }
    }
}
