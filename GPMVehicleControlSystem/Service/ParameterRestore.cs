
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using Microsoft.AspNetCore.SignalR;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Service
{
    public class ParameterRestore
    {
        public ParameterRestore()
        {

        }
        IHubContext<FrontendHub> hubContext;
        public ParameterRestore(IHubContext<FrontendHub> hubContext)
        {
            this.hubContext = hubContext;
        }
        public string PARAM_FOLDER => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param");
        public string VCS_ParamsJsonFilePath => Path.Combine(PARAM_FOLDER, "VCS_Params.json");

        public string PARAM_BACKUP_FOLDER => Path.Combine(PARAM_FOLDER, "ParamBackup");
        public string VCSParamDownload_FOLDER => Path.Combine(PARAM_FOLDER, "VCSParamDownload");

        internal async Task<(bool confirm, string message)> RestoreVCSParam(IFormFile file)
        {
            string VCS_ParamsBackupFilePath = Path.Combine(PARAM_BACKUP_FOLDER, $"VCS_Params_{DateTime.Now.ToString("yyMMddHHmmss")}.json");
            //backup
            Directory.CreateDirectory(PARAM_BACKUP_FOLDER);
            Directory.CreateDirectory(VCSParamDownload_FOLDER);
            File.Copy(VCS_ParamsJsonFilePath, VCS_ParamsBackupFilePath, true);

            string tempDownloadFilePath = Path.Combine(VCSParamDownload_FOLDER, $"VCS_Params_Download_{DateTime.Now.ToString("yyMMddHHmmss")}.json");
            //save uploaded file
            using (FileStream stream = new FileStream(tempDownloadFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            await ChangeVCSParamFile(tempDownloadFilePath);

            return (true, "");
        }


        internal async Task ChangeVCSParamFile(string fromFilePath)
        {
            File.Copy(fromFilePath, VCS_ParamsJsonFilePath, true);
        }
    }
}
