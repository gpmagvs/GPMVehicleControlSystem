using System;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Media;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Protocols;
using AGVSystemCommonNet6;
using NLog;
using GPMVehicleControlSystem.Tools;

namespace GPMVehicleControlSystem.Models.Buzzer
{
    public class BuzzerPlayer
    {
        static Logger logger => LogManager.GetCurrentClassLogger();

        public static bool IsPlaying => IsAlarmPlaying || IsActionPlaying || IsMovingPlaying || IsGotoChargeStationPlaying || IsMeasurePlaying || IsExchangeBatteryPlaying || IsHandshakingPlaying || IsWaitingCargoStatusCheckPlaying;


        private static SOUNDS _playing = SOUNDS.Stop;

        public static SOUNDS SoundPlaying
        {
            get => _playing;
            set
            {
                if (_playing != value)
                {
                    if (OnBuzzerPlay != null)
                    {
                        bool isBuzzerOff = !OnBuzzerPlay.Invoke();
                        if (isBuzzerOff)
                        {
                            _playing = SOUNDS.Stop;
                            APLAYER.PlayAudio(_playing, out _);
                            return;
                        }
                    }

                    if (value == SOUNDS.Move && BeforeBuzzerMovePlay != null)
                    {
                        var _toPlaying = BeforeBuzzerMovePlay.Invoke();
                        if (_playing == _toPlaying)
                        {
                            return;
                        }
                        else
                            _playing = _toPlaying;
                    }
                    else
                        _playing = value;

                    APLAYER.PlayAudio(_playing, out string errorMsg);
                }
            }
        }

        public static string PlayingAudio
        {
            get
            {
                if (!IsPlaying)
                    return "";
                return SoundPlaying.ToString();
            }
        }

        public static RosSocket rossocket;
        internal static bool IsAlarmPlaying = false;
        internal static bool IsActionPlaying = false;
        internal static bool IsMovingPlaying = false;
        internal static bool IsGotoChargeStationPlaying = false;
        internal static bool IsMeasurePlaying = false;
        internal static bool IsExchangeBatteryPlaying = false;
        internal static bool IsHandshakingPlaying = false;
        internal static bool IsWaitingCargoStatusCheckPlaying = false;
        internal static bool IsRotatingPlaying = false;
        internal static bool IsSlowDownPlaying = false;
        public delegate bool OnBuzzerPlayDelate();
        public static OnBuzzerPlayDelate OnBuzzerPlay;
        public delegate SOUNDS BuzzerMovePlayDelate();
        public static BuzzerMovePlayDelate BeforeBuzzerMovePlay;
        internal static APlayer APLAYER = null;
        static Debouncer playDebouncer = new Debouncer();
        public static void DeterminePlayerUse()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
                return;
            // 初始化 Process 对象
            Process process = new Process();
            process.StartInfo.FileName = "bash";
            process.StartInfo.Arguments = "-c \"which aplay\""; // 使用 bash -c 來執行命令
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!string.IsNullOrEmpty(result))
            {
                APLAYER = new APlayer(logger);
                Console.WriteLine("which aplay result: " + result);
                Console.WriteLine("Will use aplay to play audios!!");
            }
        }
        internal static void BackgroundStop()
        {
            IsRotatingPlaying = IsSlowDownPlaying = false;
            PlayInBackground(SOUNDS.Stop);
        }
        public static async Task<bool> UpdateMusicService(SOUNDS sound)
        {
            var request = new UpdateMusicRequest(sound.ToString().ToLower());
            logger.Info($"Call /update_music :{request.ToJson()}");
            UpdateMusicResponse response = await rossocket.CallServiceAndWait<UpdateMusicRequest, UpdateMusicResponse>("/update_music", request);
            return response != null ? response.success : false;
        }

        public static async Task PlayInBackground(SOUNDS sound)
        {
            if (APLAYER == null)
                return;
            await Task.Run(() =>
            {
                APLAYER.PlayAudioBackground(sound, out string errorMsg);
            });
        }
    }
}
