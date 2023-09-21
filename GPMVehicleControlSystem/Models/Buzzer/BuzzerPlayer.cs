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
        public static void Initialize()
        {
        }

        public static async void Alarm()
        {
            _ = Task.Run(async () =>
            {
                await Stop();
                await Play(SOUNDS.Alarm);
                IsAlarmPlaying = true;
            });
        }
        public static async void Action()
        {
            if (IsActionPlaying)
                return;
            await Stop();
            await Play(SOUNDS.Action);
            IsActionPlaying = true;
        }
        public static async void Move()
        {
            if (IsMovingPlaying)
                return;
            await Stop();
            await Play(SOUNDS.Move);
            IsMovingPlaying = true;
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
            await Stop();
            await Play(SOUNDS.Measure);
            IsMeasurePlaying = true;
        }

        internal static async void ExchangeBattery()
        {
            if (IsExchangeBatteryPlaying)
                return;
            await Stop();
            await Play(SOUNDS.ExchangeBattery);
            IsExchangeBatteryPlaying = true;
        }
        internal static async Task Stop()
        {
            await Play(SOUNDS.Stop);
            IsAlarmPlaying = IsActionPlaying = IsMovingPlaying = IsHandshakingPlaying = false;
        }

        public static async Task Play(SOUNDS sound)
        {
            if (rossocket == null)
                return;
            await Task.Delay(10);
            PlayMusicResponse response = await rossocket.CallServiceAndWait<PlayMusicRequest, PlayMusicResponse>("/play_music", new PlayMusicRequest
            {
                file_path = sound.ToString().ToLower()
            });
        }

    }
}
