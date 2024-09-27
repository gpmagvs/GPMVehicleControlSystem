using System.Diagnostics;

namespace GPMVehicleControlSystem.Models.Buzzer
{
    public class APlayer
    {
        List<Process> playingProcesses = new List<Process>();
        Dictionary<SOUNDS, Process> BGPlayingProcesses = new();
        Process playingProcess = new();
        SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        bool stopFlag = false;
        bool BGStopFlag = false;
        public APlayer()
        {

        }
        public async Task Stop()
        {
            try
            {
                playingProcess?.Kill();

                if (playingProcesses != null && playingProcesses.Any())
                {
                    foreach (Process process in playingProcesses.Where(p => p?.Id != playingProcess?.Id))
                        try
                        {
                            process.Kill();
                            Console.WriteLine($"aplay process-{process.Id} killed.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"exception happen when kill aplay process-{process.Id}(${ex.Message})");
                        }
                }
                stopFlag = true;
                playingProcesses.Clear();

                while (Process.GetProcesses("aplay").Any())
                {
                    await Task.Delay(1);
                }
                stopFlag = false;
            }
            catch (Exception ex)
            {
            }
            finally
            {
            }
        }

        internal bool PlayAudio(SOUNDS sound, out string errorMessage)
        {
            Stop().GetAwaiter().GetResult();
            errorMessage = "";
            Console.WriteLine($"APlay PlayAudio Method Invoked.({sound})");
            string audioPath = GetAudioPath(sound);
            if (sound == SOUNDS.Stop)
            {
                Stop().GetAwaiter().GetResult();
                return true;
            }
            return PlayAudio(audioPath, out errorMessage);
        }

        private string GetAudioPath(SOUNDS sound)
        {
            string audiosFolder = "/home/gpm/param/sounds";
            Console.WriteLine($"APlay PlayAudio Method Invoked.({sound})");
            switch (sound)
            {
                case SOUNDS.Alarm:
                    return $"{audiosFolder}/alarm.wav";
                case SOUNDS.Move:
                    return $"{audiosFolder}/move.wav";
                case SOUNDS.Action:
                    return $"{audiosFolder}/action.wav";
                case SOUNDS.Stop:
                    return "";
                case SOUNDS.Measure:
                    return $"{audiosFolder}/measure.wav";
                case SOUNDS.Exchange:
                    return $"{audiosFolder}/exchange.mp3";
                case SOUNDS.Handshaking:
                    return $"{audiosFolder}/action.wav";
                case SOUNDS.GoToChargeStation:
                    return $"{audiosFolder}/goto_charge.wav";
                case SOUNDS.WaitingCargoStatusCheck:
                    return $"{audiosFolder}/waiting_cargo_status_check.wav";
                case SOUNDS.SlowDownVoice:
                    return $"{audiosFolder}/speed_slow_down.wav";
                case SOUNDS.SlowDownMusic:
                    return $"{audiosFolder}/slow_down_2.wav";
                case SOUNDS.RotatingVoice:
                    return $"{audiosFolder}/vehicle_rotating.wav";
                case SOUNDS.RotatingMusic:
                    return $"{audiosFolder}/vehicle_rotating2.wav";
                default:
                    return "";
            }
        }

        internal bool PlayAudio(string audioPath, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                if (!File.Exists(audioPath))
                {
                    errorMessage = $"Audio File Not Exist({audioPath})";
                    return false;
                }

                Task.Run(async () =>
                {
                    try
                    {
                        await semaphoreSlim.WaitAsync();
                        stopFlag = false;

                        while (true)
                        {
                            try
                            {
                                playingProcess = Process.Start("aplay", audioPath);
                                playingProcesses.Add(playingProcess);
                                playingProcess.WaitForExit();
                                playingProcesses.Remove(playingProcess);
                                Console.WriteLine(playingProcess.StandardOutput);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"exception occur when playing while loop :${ex.Message}.{playingProcess?.Id}");
                            }
                            if (stopFlag)
                                break;
                        }
                        Console.WriteLine($"playing while loop out(stopFlag:${stopFlag}).{playingProcess?.Id}");

                    }
                    catch (Exception ex)
                    {
                    }
                    finally
                    {
                        semaphoreSlim.Release();
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        internal void PlayAudioBackground(SOUNDS sound, out string errorMsg)
        {
            errorMsg = string.Empty;
            string audioPath = GetAudioPath(sound);
            if (sound == SOUNDS.Stop)
            {

                BGStopFlag = true;
                foreach (var process in BGPlayingProcesses.Values)
                {
                    try
                    {
                        process?.Kill();
                    }
                    catch (Exception)
                    {
                    }
                }
                return;
            }
            BGStopFlag = false;
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        Process bgPlayingProcess = Process.Start("aplay", audioPath);
                        if (BGPlayingProcesses.TryGetValue(sound, out Process playingProcess))
                        {
                            playingProcess?.Kill();
                        }
                        BGPlayingProcesses.Add(sound, bgPlayingProcess);
                        bgPlayingProcess.WaitForExit();

                        if (BGPlayingProcesses.TryGetValue(sound, out var _playingProcess))
                        {
                            _playingProcess?.Kill();
                            BGPlayingProcesses.Remove(sound);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"exception occur when playing while loop :${ex.Message}.{playingProcess?.Id}");
                    }
                    if (BGStopFlag)
                        break;
                    await Task.Delay(100);
                }
            });
        }
    }
}
