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

        public static async void Alarm()
        {
            if (IsAlarmPlaying)
                return;
            _ = Task.Run(async () =>
            {
                await Stop();
                await Play(SOUNDS.Alarm);
                IsAlarmPlaying = true;
            });
        }
        public static async void Action(bool IsCharge = false)
        {
            if (IsActionPlaying)
                return;
            _ = Task.Run(async () =>
            {
                await Stop();
                await Play(IsCharge ? SOUNDS.GoToChargeStation : SOUNDS.Action);
                IsActionPlaying = true;
            });
        }
        public static async void Move()
        {
            if (IsMovingPlaying)
                return;
            _ = Task.Run(async () =>
            {
                await Stop();

                var _sound = SOUNDS.Move;
                if (BeforeBuzzerMovePlay != null)
                    _sound = BeforeBuzzerMovePlay();
                if (_sound == SOUNDS.GoToChargeStation)
                    LOG.WARN($"Play Go to charge station sound because destine is front of charge station.");

                await Play(_sound);
                IsMovingPlaying = true;
            });
        }

        public static async void Handshaking()
        {
            if (IsHandshakingPlaying)
                return;
            await Stop();
            await Play(SOUNDS.Handshaking);
            IsHandshakingPlaying = true;
        }

        internal static async void Measure()
        {
            if (IsMeasurePlaying)
                return;
            _ = Task.Run(async () =>
            {
                await Stop();
                await Play(SOUNDS.Measure);
                IsMeasurePlaying = true;
            });
        }

        internal static async void ExchangeBattery()
        {
            if (IsExchangeBatteryPlaying)
                return;
            _ = Task.Run(async () =>
            {
                await Stop();
                await Play(SOUNDS.Exchange);
                IsExchangeBatteryPlaying = true;
            });
        }
        internal static async Task Stop()
        {
            await Play(SOUNDS.Stop);
            IsAlarmPlaying = IsActionPlaying = IsExchangeBatteryPlaying = IsMovingPlaying = IsMeasurePlaying = IsHandshakingPlaying = false;
        }
        public static async Task Play(SOUNDS sound)
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
                await Task.Delay(10);

                PlayMusicResponse response = await rossocket.CallServiceAndWait<PlayMusicRequest, PlayMusicResponse>("/play_music", new PlayMusicRequest
                {
                    file_path = sound.ToString().ToLower()
                });
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
        }

    }
}
