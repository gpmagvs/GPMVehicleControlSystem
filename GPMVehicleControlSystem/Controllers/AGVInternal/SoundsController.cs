using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params.clsSoundsParams;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class SoundsController : ControllerBase
    {
        [HttpGet("Alarm")]
        public async Task<IActionResult> Alarm()
        {
            BuzzerPlayer.Alarm();
            return Ok();
        }
        [HttpGet("Moving")]
        public async Task<IActionResult> Moving()
        {
            BuzzerPlayer.Move();
            return Ok();
        }
        [HttpGet("Action")]
        public async Task<IActionResult> Action()
        {
            BuzzerPlayer.Action();
            return Ok();
        }
        [HttpGet("Stop")]
        public async Task<IActionResult> Stop()
        {
            BuzzerPlayer.Stop();
            return Ok();
        }

        /// <summary>
        /// 0:Alarm,1:Move,2:Action,3:Stop,4:Measuer,5:Exchange,6:Handshaking,7:GoToChargeStation
        /// </summary>
        /// <param name="sound"></param>
        /// <returns></returns>
        [HttpPost("UploadMusicFile")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadMusicFile(SOUNDS sound = SOUNDS.Move)
        {
            var file = Request.Form.Files[0];
            if (file.Length > 100 * 1024 * 1024) // 100MB
            {
                return BadRequest("檔案大小超過 100MB。");
            }

            if (file.Length > 0)
            {
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/sounds/{sound.ToString().ToLower()}.wav");

                using (var stream = new FileStream(filePath, FileMode.OpenOrCreate))
                {
                    await file.CopyToAsync(stream);
                }

                bool updateSuccess = await BuzzerPlayer.UpdateMusicService(sound);

                return Ok(new
                {
                    success = updateSuccess,
                    message = updateSuccess ? "音檔上傳更新成功。" : "音檔上傳更新失敗"
                });
            }

            return BadRequest("未接收到任何檔案。");
        }

        [HttpGet("VolumeIncrease")]
        public async Task<IActionResult> VolumeIncrease(int percentage)
        {
            StaStored.VolumnAdjuster.VolumeControl(Tools.SystemVolumnAdjuster.ADJUST_ACTION.INCREASE, percentage);
            return Ok();
        }
        [HttpGet("VolumeDecrease")]
        public async Task<IActionResult> VolumeDecrease(int percentage)
        {
            StaStored.VolumnAdjuster.VolumeControl(Tools.SystemVolumnAdjuster.ADJUST_ACTION.DECREASE, percentage);
            return Ok();
        }

        [HttpGet("VolumeSet")]
        public async Task<IActionResult> VolumeSet(int percentage)
        {
            StaStored.VolumnAdjuster.VolumeControl(Tools.SystemVolumnAdjuster.ADJUST_ACTION.SET, percentage);
            return Ok();
        }
        [HttpPost("StopAplay")]
        public async Task<IActionResult> StopAplay()
        {
            BuzzerPlayer.APLAYER.Stop();
            return Ok();
        }
        [HttpPost("aplaytest")]
        public async Task<IActionResult> aplaytest(string audioPath = "/home/gpm/param/sounds/alarm.wav")
        {
            try
            {
                bool success = BuzzerPlayer.APLAYER.PlayAudio(audioPath, out string error);
                return Ok(new
                {
                    result = success,
                    message = error
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    result = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost("SaveAudioPath")]
        public async Task<IActionResult> SaveAudioPath([FromBody] AudioPathes pathes)
        {
            StaStored.CurrentVechicle.Parameters.SoundsParams.audioPathes = pathes;
            (bool confirm, string errorMsg) = await Vehicle.SaveParameters(StaStored.CurrentVechicle.Parameters);
            return Ok(new { confirm = confirm, errorMsg = errorMsg });
        }

        [HttpGet("GetUsableAudios")]
        public async Task<IActionResult> GetAudiosFromParamSoundsFolder()
        {
            return Ok(AudioManager.GetAudiosInDisk());
        }
        [HttpDelete]
        public async Task<IActionResult> DeleteAudioFile(string audioName)
        {
            AudioManager.Delete(audioName);
            return Ok();
        }
        /// <summary>
        /// 0:Alarm,1:Move,2:Action,3:Stop,4:Measuer,5:Exchange,6:Handshaking,7:GoToChargeStation
        /// </summary>
        /// <param name="sound"></param>
        /// <returns></returns>
        [HttpPost("UploadAudio")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadAudio()
        {
            var file = Request.Form.Files[0];
            if (file.Length > 100 * 1024 * 1024) // 100MB
            {
                return BadRequest("檔案大小超過 100MB。");
            }

            if (file.Length > 0)
            {
                await AudioManager.HandleAudioUpload(file);
                return Ok(new
                {
                    success = true,
                    message = "音檔上傳更新成功。"
                });
            }

            return BadRequest("未接收到任何檔案。");
        }


    }
}
