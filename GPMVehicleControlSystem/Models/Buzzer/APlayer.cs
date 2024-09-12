using System.Diagnostics;

namespace GPMVehicleControlSystem.Models.Buzzer
{
    public class APlayer
    {
        List<Process> playingProcesses = new List<Process>();
        Process playingProcess = new();
        SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        bool stopFlag = false;
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
            string audiosFolder = "/home/gpm/param/sounds";
            Console.WriteLine($"APlay PlayAudio Method Invoked.({sound})");
            switch (sound)
            {
                case SOUNDS.Alarm:
                    return PlayAudio($"{audiosFolder}/alarm.wav", out errorMessage);
                case SOUNDS.Move:
                    return PlayAudio($"{audiosFolder}/move.wav", out errorMessage);
                case SOUNDS.Action:
                    return PlayAudio($"{audiosFolder}/action.wav", out errorMessage);
                case SOUNDS.Stop:
                    Stop().GetAwaiter().GetResult();
                    return true;
                case SOUNDS.Measure:
                    return PlayAudio($"{audiosFolder}/measure.wav", out errorMessage);
                case SOUNDS.Exchange:
                    return PlayAudio($"{audiosFolder}/exchange.mp3", out errorMessage);
                case SOUNDS.Handshaking:
                    return PlayAudio($"{audiosFolder}/action.wav", out errorMessage);
                case SOUNDS.GoToChargeStation:
                    return PlayAudio($"{audiosFolder}/goto_charge.wav", out errorMessage);
                default:
                    return false;
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

    }
}
