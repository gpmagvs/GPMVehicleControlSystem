using System;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Media;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Protocols;
using AGVSystemCommonNet6;

namespace GPMVehicleControlSystem.Models.Buzzer
{
    public class BuzzerPlayer
    {
        public static RosSocket rossocket;
        internal static bool IsAlarmPlaying = false;
        internal static bool IsActionPlaying = false;
        internal static bool IsMovingPlaying = false;
        internal static bool IsGotoChargeStationPlaying = false;
        internal static bool IsMeasurePlaying = false;
        internal static bool IsExchangeBatteryPlaying = false;
        internal static bool IsHandshakingPlaying = false;
        public delegate bool OnBuzzerPlayDelate();
        public static OnBuzzerPlayDelate OnBuzzerPlay;
        public delegate SOUNDS BuzzerMovePlayDelate();
        public static BuzzerMovePlayDelate BeforeBuzzerMovePlay;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
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

        public static void Handshaking()
        {
            if (IsHandshakingPlaying)
                return;
            Play(SOUNDS.Handshaking);
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
        internal static void Stop()
        {
            Play(SOUNDS.Stop);
        }

        public static async void Play(SOUNDS sound)
        {
            try
            {
                await semaphore.WaitAsync();

                if (sound == SOUNDS.Stop)
                {
                    IsGotoChargeStationPlaying = IsAlarmPlaying = IsActionPlaying = IsExchangeBatteryPlaying = IsMovingPlaying = IsMeasurePlaying = IsHandshakingPlaying = false;
                }
                else
                {
                    IsMovingPlaying = sound == SOUNDS.Move;
                    IsGotoChargeStationPlaying = sound == SOUNDS.GoToChargeStation;
                    IsActionPlaying = sound == SOUNDS.Action;
                    IsAlarmPlaying = sound == SOUNDS.Alarm;
                    IsExchangeBatteryPlaying = sound == SOUNDS.Exchange;
                    IsHandshakingPlaying = sound == SOUNDS.Handshaking;
                }

                LOG.WARN($"Playing Sound : {sound}");
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
                        LOG.ERROR(ex);
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
