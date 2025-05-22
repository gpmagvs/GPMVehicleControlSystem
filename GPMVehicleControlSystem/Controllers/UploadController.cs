using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers
{
    using GPMVehicleControlSystem.Models;
    using GPMVehicleControlSystem.Models.Buzzer;
    using GPMVehicleControlSystem.Service;
    using Microsoft.AspNetCore.Mvc;
    using NLog;
    using System.IO;
    using System.Threading.Tasks;

    namespace FileUploadExample.Controllers
    {
        [Route("api/[controller]")]
        [ApiController]
        public class UploadController : ControllerBase
        {
            private readonly string _uploadFolderPath;
            private readonly string _frontendUploadFolderPath;
            private SystemUpdateService _updateService;
            Logger logger = LogManager.GetCurrentClassLogger();
            public UploadController(SystemUpdateService updateService)
            {
                _updateService = updateService;
                _uploadFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                _frontendUploadFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                Directory.CreateDirectory(_uploadFolderPath);
            }

            [HttpPost]
            [DisableRequestSizeLimit]
            public async Task<IActionResult> UploadFile()
            {
                var file = Request.Form.Files[0];
                if (file.Length > 100 * 1024 * 1024) // 100MB
                {
                    return BadRequest("檔案大小超過 100MB。");
                }

                if (file.Length > 0)
                {
                    var filePath = Path.Combine(_uploadFolderPath, file.FileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    logger.Info($"接收到{Request.Form.Files.Count}筆更新檔案");
                    return Ok("檔案上傳成功。");
                }

                return BadRequest("未接收到任何檔案。");
            }


            [HttpPost("UploadFrontendWebsiteFile")]
            [DisableRequestSizeLimit]
            public async Task<IActionResult> UploadFrontendWebsiteFile()
            {
                var file = Request.Form.Files[0];
                if (file.Length > 100 * 1024 * 1024) // 100MB
                {
                    return BadRequest("檔案大小超過 100MB。");
                }

                if (file.Length > 0)
                {
                    var filePath = Path.Combine(_frontendUploadFolderPath, file.FileName);

                    using (var stream = new FileStream(filePath, FileMode.Append))
                    {
                        await file.CopyToAsync(stream);
                    }
                    logger.Info($"接收到{Request.Form.Files.Count}筆前端更新檔案");
                    return Ok("檔案上傳成功。");
                }

                return BadRequest("未接收到任何檔案。");
            }


            [HttpPost("UploadSystemUpdateZipFile")]
            [DisableRequestSizeLimit]
            public async Task<IActionResult> UploadSystemUpdateZipFile()
            {
                var file = Request.Form.Files[0];
                if (file.Length > 100 * 1024 * 1024) // 100MB
                {
                    return BadRequest("檔案大小超過 100MB。");
                }
                (bool confirm, string message) result = await _updateService.SystemUpdateWithFileUpload(file);

                if (result.confirm)
                {
                    StaStored.CurrentVechicle.SetSub_Status(AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN);
                    _ = Task.Delay(1000).ContinueWith(async t =>
                    {
                        BuzzerPlayer.Stop();
                        await Task.Delay(500);
                        BuzzerPlayer.Alarm();
                    });

                    _ = _updateService.BrocastRestartSystemCountDownNotify("系統更新", 5);
                }

                return Ok(result);
            }
        }
    }
}
