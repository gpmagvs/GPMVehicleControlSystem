using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace OTAUpdateService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OTAController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public OTAController(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        [HttpGet]
        public async Task<IActionResult> UpdateVCSFromServer()
        {
            string downloadUrl = _configuration.GetValue<string>("OTASettings:DownloadUrl");
            string vcsShutDownUrl = _configuration.GetValue<string>("OTASettings:VCSShutDownUrl");
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string downloadedFileSaveFolderPath = Path.Combine(home, "gpm_vms/updates");
            string vcsAppPath = Path.Combine(home, "gpm_vms");
            string vcsAppfilename = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "GPM_VCS.exe" : "GPM_VCS";

            OtaUpdater Updater = new OtaUpdater(downloadUrl, vcsShutDownUrl, downloadedFileSaveFolderPath, vcsAppPath, Path.Combine(vcsAppPath, vcsAppfilename));
            try
            {
                (bool confirm, string message) response = await Updater.UpdateApplicationAsync();
                return Ok(new
                {
                    confirm = response.confirm,
                    message = response.message,
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    confirm = false,
                    message = ex.Message + ex.StackTrace
                });
            }
        }
    }
}
