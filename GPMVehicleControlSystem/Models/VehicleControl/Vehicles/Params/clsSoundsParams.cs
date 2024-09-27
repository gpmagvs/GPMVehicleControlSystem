
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsSoundsParams
    {

        public SlowDownAndRotatinSoundPlay slowDownAndRotatinSoundPlay { get; set; } = new SlowDownAndRotatinSoundPlay();

        public AudioPathes audioPathes { get; set; } = new AudioPathes();
        public class AudioPathes
        {
            public string move { get; set; } = "~/param/sounds/move.wav";
            public string alarm { get; set; } = "~/param/sounds/alarm.wav";
            public string action { get; set; } = "~/param/sounds/action.wav";
            public string batteryExchange { get; set; } = "~/param/sounds/exchange.mp3";
            public string goToCharge { get; set; } = "~/param/sounds/goto_charge.wav";
            public string measure { get; set; } = "~/param/sounds/measure.wav";
            public string rotating { get; set; } = "~/param/sounds/vehicle_rotating2.wav";
            public string slowDown { get; set; } = "~/param/sounds/slow_down_2.wav";
            public string rotating_voice { get; set; } = "~/param/sounds/vehicle_rotating.wav";
            public string slowDown_voice { get; set; } = "~/param/sounds/slow_down.wav";
            public string waitingCargoStatusCheck { get; set; } = "~/param/sounds/waiting_cargo_status_check.wav";
        }

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
