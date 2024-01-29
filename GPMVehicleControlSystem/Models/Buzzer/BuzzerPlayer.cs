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

namespace GPMVehicleControlSystem.Models.Buzzer
{
    public class BuzzerPlayer
    {
        public static RosSocket rossocket;
        internal static bool IsAlarmPlaying = false;
        internal static bool IsActionPlaying = false;
        internal static bool IsMovingPlaying = false;
        internal static bool IsMeasurePlaying = false;
        internal static bool IsExchangeBatteryPlaying = false;
        internal static bool IsHandshakingPlaying = false;
        public delegate bool OnBuzzerPlayDelate();
        public static OnBuzzerPlayDelate OnBuzzerPlay;
        public delegate SOUNDS BuzzerMovePlayDelate();
        public static BuzzerMovePlayDelate BeforeBuzzerMovePlay;

        public static void Alarm()
        {
            if (IsAlarmPlaying)
                return;
            IsAlarmPlaying = true;
            Play(SOUNDS.Alarm);
        }
        public static void Action()
        {
            if (IsActionPlaying)
                return;
            IsActionPlaying = true;
            Play(SOUNDS.Action);
        }
        public static void Move()
        {
            if (IsMovingPlaying)
                return;
            IsMovingPlaying = true;
            var _sound = SOUNDS.Move;
            if (BeforeBuzzerMovePlay != null)
                _sound = BeforeBuzzerMovePlay();
            if (_sound == SOUNDS.GoToChargeStation)
                LOG.WARN($"Play Go to charge station sound because destine is front of charge station.");
            Play(_sound);
        }

        public static void Handshaking()
        {
            if (IsHandshakingPlaying)
                return;
            IsHandshakingPlaying = true;
            Play(SOUNDS.Handshaking);
        }

        internal static void Measure()
        {
            if (IsMeasurePlaying)
                return;
            IsMeasurePlaying = true;
            Play(SOUNDS.Measure);
        }

        internal static void ExchangeBattery()
        {
            if (IsExchangeBatteryPlaying)
                return;
            Play(SOUNDS.Exchange);
            IsExchangeBatteryPlaying = true;
        }
        internal static void Stop()
        {
            IsAlarmPlaying = IsActionPlaying = IsExchangeBatteryPlaying = IsMovingPlaying = IsMeasurePlaying = IsHandshakingPlaying = false;
            Play(SOUNDS.Stop);
        }

        public static void Play(SOUNDS sound)
        {
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

    }
}
