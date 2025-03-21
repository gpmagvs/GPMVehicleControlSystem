using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
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
        NLog.Logger logger;
        clsSoundsParams.AudioPathes AudioPathes
        {
            get
            {
                if (StaStored.CurrentVechicle == null)
                    return new();
                return StaStored.CurrentVechicle.Parameters.SoundsParams.audioPathes;
            }
        }

        public APlayer(NLog.Logger logger)
        {
            this.logger = logger;
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
                        }
                        catch (Exception ex)
                        {
                            logger?.Error($"exception happen when kill aplay process-{process.Id}(${ex.Message})");
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
                    return AudioPathes.alarm;
                case SOUNDS.Move:
                    return AudioPathes.move;
                case SOUNDS.Action:
                    return AudioPathes.action;
                case SOUNDS.Stop:
                    return "";
                case SOUNDS.Measure:
                    return AudioPathes.measure;
                case SOUNDS.Exchange:
                    return AudioPathes.batteryExchange;
                case SOUNDS.Handshaking:
                    return AudioPathes.action;
                case SOUNDS.GoToChargeStation:
                    return AudioPathes.goToCharge;
                case SOUNDS.WaitingCargoStatusCheck:
                    return AudioPathes.waitingCargoStatusCheck;
                case SOUNDS.SlowDownVoice:
                    return AudioPathes.slowDown_voice;
                case SOUNDS.SlowDownMusic:
                    return AudioPathes.slowDown;
                case SOUNDS.RotatingVoice:
                    return AudioPathes.rotating_voice;
                case SOUNDS.RotatingMusic:
                    return AudioPathes.rotating;
                case SOUNDS.Backward:
                    return AudioPathes.backward;
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
                            }
                            catch (Exception ex)
                            {
                                logger?.Error($"exception occur when playing while loop :${ex.Message}.{playingProcess?.Id}");
                            }
                            if (stopFlag)
                                break;
                        }

                    }
                    catch (Exception ex)
                    {
                        logger?.Error($"exception occur when playing while loop :${ex.Message}.{playingProcess?.Id}");
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
                    catch (Exception ex)
                    {
                        logger?.Error($"ss${ex.Message}.{playingProcess?.Id}");
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
                        logger?.Error($"exception occur when playing while loop :${ex.Message}.{playingProcess?.Id}");
                    }
                    if (BGStopFlag)
                        break;
                    await Task.Delay(100);
                }
            });
        }
    }
}
