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

        static bool IsAlarmPlaying = false;
        static bool IsActionPlaying = false;
        static bool IsMovingPlaying = false;
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
        internal static async Task Stop()
        {
            await Play(SOUNDS.Stop);
            IsAlarmPlaying = IsActionPlaying = IsMovingPlaying = false;
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
