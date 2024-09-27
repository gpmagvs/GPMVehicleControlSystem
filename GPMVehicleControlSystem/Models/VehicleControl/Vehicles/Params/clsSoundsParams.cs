
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsSoundsParams
    {

        public SlowDownAndRotatinSoundPlay slowDownAndRotatinSoundPlay { get; set; } = new SlowDownAndRotatinSoundPlay();

        public class SlowDownAndRotatinSoundPlay
        {
            public bool Enable { get; set; } = true;

            [JsonConverter(typeof(StringEnumConverter))]
            public SOUND_TYPE SoundPlayType { get; set; } = SOUND_TYPE.MUSIC_AUDIO;
            public enum SOUND_TYPE
            {
                BG_VOICE,
                MUSIC_AUDIO
            }
        }
    }
}
