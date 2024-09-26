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

namespace GPMVehicleControlSystem.Models.Buzzer
{
    public class BuzzerPlayer
    {
        static Logger logger => LogManager.GetCurrentClassLogger();

        public static bool IsPlaying => IsAlarmPlaying || IsActionPlaying || IsMovingPlaying || IsGotoChargeStationPlaying || IsMeasurePlaying || IsExchangeBatteryPlaying || IsHandshakingPlaying || IsWaitingCargoStatusCheckPlaying;
        public static SOUNDS SoundPlaying { get; private set; } = SOUNDS.Stop;
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
        public delegate bool OnBuzzerPlayDelate();
        public static OnBuzzerPlayDelate OnBuzzerPlay;
        public delegate SOUNDS BuzzerMovePlayDelate();
        public static BuzzerMovePlayDelate BeforeBuzzerMovePlay;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        internal static APlayer APLAYER = null;
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
                APLAYER = new APlayer();
                Console.WriteLine("which aplay result: " + result);
                Console.WriteLine("Will use aplay to play audios!!");
            }
        }
        public static void Alarm()
        {
            if (IsAlarmPlaying)
                return;
            Play(SOUNDS.Alarm);
        }
        public static void Action()
        {
            if (IsActionPlaying)
                return;
            Play(SOUNDS.Action);
        }
        public static void Move()
        {
            if (IsMovingPlaying)
                return;
            var _sound = SOUNDS.Move;
            if (BeforeBuzzerMovePlay != null)
                _sound = BeforeBuzzerMovePlay();
            if (_sound == SOUNDS.GoToChargeStation && IsGotoChargeStationPlaying) return;

            Play(_sound);
        }

        internal static void Measure()
        {
            if (IsMeasurePlaying)
                return;
            Play(SOUNDS.Measure);
        }

        internal static void ExchangeBattery()
        {
            if (IsExchangeBatteryPlaying)
                return;
            Play(SOUNDS.Exchange);
        }
        internal static void WaitingCargoStatusCheck()
        {
            if (IsWaitingCargoStatusCheckPlaying)
                return;
            Play(SOUNDS.Exchange);
        }
        internal static void Stop()
        {
            Play(SOUNDS.Stop);
        }
        public static async Task<bool> UpdateMusicService(SOUNDS sound)
        {
            var request = new UpdateMusicRequest(sound.ToString().ToLower());
            logger.Info($"Call /update_music :{request.ToJson()}");
            UpdateMusicResponse response = await rossocket.CallServiceAndWait<UpdateMusicRequest, UpdateMusicResponse>("/update_music", request);
            return response != null ? response.success : false;
        }
        public static async void Play(SOUNDS sound)
        {
            try
            {
                SoundPlaying = sound;
                await semaphore.WaitAsync();

                if (sound == SOUNDS.Stop)
                {
                    IsGotoChargeStationPlaying = IsAlarmPlaying = IsActionPlaying = IsExchangeBatteryPlaying = IsMovingPlaying = IsMeasurePlaying = IsHandshakingPlaying = IsWaitingCargoStatusCheckPlaying = false;
                }
                else
                {
                    IsMovingPlaying = sound == SOUNDS.Move;
                    IsGotoChargeStationPlaying = sound == SOUNDS.GoToChargeStation;
                    IsActionPlaying = sound == SOUNDS.Action;
                    IsAlarmPlaying = sound == SOUNDS.Alarm;
                    IsExchangeBatteryPlaying = sound == SOUNDS.Exchange;
                    IsHandshakingPlaying = sound == SOUNDS.Handshaking;
                    IsWaitingCargoStatusCheckPlaying = sound == SOUNDS.WaitingCargoStatusCheck;
                }

                logger.Info($"Playing Sound : {sound}");

                if (APLAYER != null)
                {
                    logger.Info($"Playing with APLAYER : {sound}");
                    APLAYER.PlayAudio(sound, out string errorMsg);
                    return;
                }

                Thread playsound_thred = new Thread(() =>
                {
                    try
                    {
                        if (OnBuzzerPlay != null && sound != SOUNDS.Stop)
                        {
                            bool confirm = OnBuzzerPlay.Invoke();
                            if (!confirm)
                                return;
                        }
                        if (rossocket == null)
                            return;
                        PlayMusicResponse response = rossocket.CallServiceAndWait<PlayMusicRequest, PlayMusicResponse>("/play_music", new PlayMusicRequest
                        {
                            file_path = sound.ToString().ToLower()
                        }).Result;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                    }
                });
                playsound_thred.IsBackground = false;
                playsound_thred.Start();
            }
            catch (Exception)
            {
            }
            finally
            {
                semaphore.Release();
            }
        }

    }
}
