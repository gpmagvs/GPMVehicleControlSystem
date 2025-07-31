using AGVSystemCommonNet6;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Service;
using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.Tools.DiskUsage;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        private SystemUpdateService _sysUpdateService;
        private LinuxDiskUsageMonitor _diskUsageMonitor;
        private readonly IHubContext<FrontendHub> hubContext;
        private readonly ParameterRestore parameterRestore;
        private readonly SystemUpdateService systemUpdateService;
        private readonly VersionOptions version;
        IMemoryCache _memoryCache;
        public SystemController(SystemUpdateService sysUpdateService, ParameterRestore parameterRestore
                                , LinuxDiskUsageMonitor diskUsageMonitor, IHubContext<FrontendHub> hubContext, SystemUpdateService systemUpdateService, IMemoryCache memoryCache
                                , IOptions<VersionOptions> versionOptions)
        {
            _sysUpdateService = sysUpdateService;
            _diskUsageMonitor = diskUsageMonitor;
            this.hubContext = hubContext;
            this.parameterRestore = parameterRestore;
            this.systemUpdateService = systemUpdateService;
            _memoryCache = memoryCache;
            version = versionOptions.Value;
        }


        [HttpGet("VersionInfo")]
        public async Task<IActionResult> GetVersionInfo()
        {
            return Ok(version);
        }



        [HttpGet("Settings")]
        public async Task<IActionResult> GetParameters()
        {
            StaStored.CurrentVechicle.Parameters.EditKey = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            clsVehicelParam parameters = StaStored.CurrentVechicle.Parameters.Clone();
            return Ok(parameters);
        }

        [HttpPost("SaveParameters")]
        public async Task<IActionResult> SaveParameters([FromBody] clsVehicelParam param)
        {
            try
            {
                if (param.EditKey != StaStored.CurrentVechicle.Parameters.EditKey)
                    throw new Exception("修改的參數未與系統同步");
                if (param.IsUIDefault)
                    throw new Exception("無效參數!將可能會造成系統異常");
                //派車HOST同步 MapUrl
                param.VMSParam.MapUrl = $"http://{param.Connections[clsConnectionParam.CONNECTION_ITEM.AGVS].IP}:5216/api/Map";
                StaStored.CurrentVechicle.Parameters = param;
                (bool confirm, string errorMsg) = await Vehicle.SaveParameters(param, this.hubContext);
                return Ok(new { confirm = confirm, errorMsg = errorMsg });
            }
            catch (Exception ex)
            {
                return Ok(new { confirm = false, errorMsg = ex.Message });
            }
        }

        [HttpPost("CloseSystem")]
        public async Task<IActionResult> CloseSystem()
        {

            if (agv.GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN || agv.GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                return Ok(new { confirm = false, message = $"AGV當前狀態({agv.GetSub_Status()})禁止重新啟動系統!" });
            StaSysControl.SystemClose();
            return Ok(new { confirm = true, message = "" });
        }


        [HttpPost("RestartSystem")]
        public async Task<IActionResult> RestartSystem()
        {
            if (agv.GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN || agv.GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                return Ok(new { confirm = false, message = $"AGV當前狀態({agv.GetSub_Status()})禁止重新啟動系統!" });
            StaSysControl.SystemRestart();
            return Ok(new { confirm = true, message = "" });
        }

        [HttpPost("RestartAGVC")]
        public async Task<IActionResult> RestartAGVC()
        {
            if (agv.GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN || agv.GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                return Ok(new { confirm = false, message = $"AGV當前狀態({agv.GetSub_Status()})禁止重新啟動車控系統!" });
            await StaSysControl.RestartAGVCAsync();
            return Ok(new { confirm = true, message = "" });
        }


        private Vehicle agv => StaStored.CurrentVechicle;
        [HttpPost("GCCollection")]
        public async Task GCCollect()
        {
            GC.Collect();
        }

        [HttpPost("ShutDownPC")]
        public async Task<IActionResult> ShutDownPC()
        {
            _ = Task.Run(async () =>
            {
                PCShutDownHelper.ShutdownAsync();
            });
            return Ok($"PC Will Shutdown after {PCShutDownHelper.ShutdownDelayTimeSec} sec...");
        }

        [HttpPost("BackupSystem")]
        public async Task<IActionResult> BackupSystem()
        {
            bool backupSuccess = _sysUpdateService.BackupCurrentProgram(out string errMsg);
            if (backupSuccess)
                return Ok(new { confirm = true, message = errMsg });
            else
                return Ok(new { confirm = false, message = errMsg });
        }

        [HttpPost("RollbackSystem")]
        public async Task<IActionResult> RollbackSystem(string version)
        {
            (bool confirm, string message) = await _sysUpdateService.RollbackSystem(version);
            return Ok(new { confirm = confirm, message = message });
        }

        [HttpDelete("Version")]
        public async Task<IActionResult> DeleteVersionBackup(string version)
        {
            (bool confirm, string message) = await _sysUpdateService.DeleteVersionBackup(version);
            return Ok(new { confirm = confirm, message = message });
        }
        [HttpGet("GetBackupedVersion")]
        public async Task<List<VersionInfoViewModel>> GetBackupedVersion()
        {
            return _sysUpdateService.GetHistoryVersions();
        }

        [HttpPost("SaveManualCheckCargoConfiguration")]
        public async Task<IActionResult> SaveManualCheckCargoConfiguration([FromBody] clsManualCheckCargoStatusParams configs)
        {
            configs.CheckPoints = configs.CheckPoints.OrderBy(pt => pt.CheckPointTag).ToList();
            agv.Parameters.ManualCheckCargoStatus = configs;
            (bool confirm, string errorMsg) = await Vehicle.SaveParameters(agv.Parameters, this.hubContext);
            return Ok(new { confirm, errorMsg });
        }

        [HttpDelete("DeleteOldLogData")]
        public async Task DeleteOldLogData(DateTime timeLessThan)
        {
            await _diskUsageMonitor.DeleteOldVCSLogData(timeLessThan);
        }

        [HttpPost("RunShellCommand")]
        public async Task<IActionResult> RunShellCommand(string command)
        {
            Tools.LinuxTools.RunShellCommand(command, out string output, out string error);
            return Ok(new { output, error });
        }

        [HttpGet("ConnectionStatus")]
        public async Task<ConnectionStateVM> GetConnectionStateVM()
        {
            return ViewModelFactory.GetConnectionStatesVM();
        }

        [HttpPost("RestoreVCS_ParamFile")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> RestoreVCS_ParamFile()
        {
            var file = Request.Form.Files[0];
            if (file.Length > 100 * 1024 * 1024) // 100MB
            {
                return BadRequest("檔案大小超過 100MB。");
            }
            (bool confirm, string message) result = await parameterRestore.RestoreVCSParam(file);

            if (result.confirm)
            {
                _ = Task.Run(async () =>
                {
                    systemUpdateService.BrocastRestartSystemCountDownNotify("系統參數更新", 5);
                    await Task.Delay(5000);
                    StaSysControl.SystemRestart();
                });
            }

            return Ok(result);
        }


        [HttpGet("DiskStatus")]
        public async Task<IActionResult> GetDiskStatus()
        {
            return Ok(_memoryCache.Get<DiskUsageState>("DiskStatus"));
        }

        [HttpGet("GetDiskMonitorConfiguration")]
        public async Task<IActionResult> GetDiskMonitorConfiguration()
        {

            DiskStatusMonitorConfigruationService service = new DiskStatusMonitorConfigruationService();
            DiskMonitorParams? configruation = service.LoadDiskMonitorParam();
            return Ok(configruation);
        }

        [HttpPost("SaveDiskMonitorConfiguration")]
        public async Task<IActionResult> SaveDiskMonitorConfiguration([FromBody] DiskMonitorParams configruation)
        {

            DiskStatusMonitorConfigruationService service = new DiskStatusMonitorConfigruationService();
            service.SaveConfiguration(configruation);

            return Ok();
        }
    }
}
