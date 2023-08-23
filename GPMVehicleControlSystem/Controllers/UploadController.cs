using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers
{
    using AGVSystemCommonNet6.Log;
    using Microsoft.AspNetCore.Mvc;
    using System.IO;
    using System.Threading.Tasks;

    namespace FileUploadExample.Controllers
    {
        [Route("api/[controller]")]
        [ApiController]
        public class UploadController : ControllerBase
        {
            private readonly string _uploadFolderPath;

            public UploadController()
            {
                _uploadFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
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
                    LOG.INFO($"接收到{Request.Form.Files.Count}筆更新檔案");
                    return Ok("檔案上傳成功。");
                }

                return BadRequest("未接收到任何檔案。");
            }
        }
    }
}
