
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params.clsSoundsParams.SlowDownAndRotatinSoundPlay;

namespace GPMVehicleControlSystem.Service
{
    /// <summary>
    /// 這是一個背景服務用於當AGV旋轉或減速時撥放對應的背景音效(與原撥放中音效同步撥放)
    /// </summary>
    public class RotaionAndSlowDownBGSoundBackgroundService : BackgroundService
    {

        public enum ROTATION_STATUS
        {
            NO_ROTATION,
            ROTATATE_START,
            ROTATING,
            ROTATING_BUT_STOP_BY_OBSTACLE,
        }

        public enum SLOW_DOWN_STATUS
        {
            NO_SLOW_DOWN,
            SLOW_DOWN_START,
            SLOW_DOWNING,
            SLOW_DOWN_BUT_STOP_BY_OBSTACLE,
        }


        private ROTATION_STATUS RotateStatus = ROTATION_STATUS.NO_ROTATION;
        private SLOW_DOWN_STATUS SlowDownStatus = SLOW_DOWN_STATUS.NO_SLOW_DOWN;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = WatchRotateState();
            _ = WatchSlowDownState();

        }

        private Vehicle AgvInstance => StaStored.CurrentVechicle;
        private async Task WatchRotateState()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);

                    if (AgvInstance == null || !AgvInstance.Parameters.SoundsParams.RotatingPlayAudio)
                    {
                        continue;
                    }

                    SUB_STATUS currentSubState = AgvInstance.GetSub_Status();
                    switch (RotateStatus)
                    {
                        case ROTATION_STATUS.NO_ROTATION:

                            if (AgvInstance == null || !AgvInstance.Parameters.SoundsParams.slowDownAndRotatinSoundPlay.Enable || AgvInstance.AGVC.ActionStatus != ActionStatus.ACTIVE)
                                break;

                            if (AgvInstance.Laser.CurrentLaserModeOfSick == 5 && currentSubState != SUB_STATUS.ALARM)
                            {
                                RotateStatus = ROTATION_STATUS.ROTATATE_START;
                                break;
                            }
                            break;
                        case ROTATION_STATUS.ROTATATE_START:

                            if (AgvInstance.Parameters.SoundsParams.slowDownAndRotatinSoundPlay.SoundPlayType == SOUND_TYPE.BG_VOICE)
                                BuzzerPlayer.PlayInBackground(SOUNDS.RotatingVoice);
                            else
                            {
                                BuzzerPlayer.Stop();//把本來撥放中的音效停止
                                BuzzerPlayer.PlayInBackground(SOUNDS.RotatingMusic);
                            }
                            RotateStatus = ROTATION_STATUS.ROTATING;
                            break;
                        case ROTATION_STATUS.ROTATING:

                            if (AgvInstance.Laser.CurrentLaserModeOfSick != 5)
                            {
                                RotateStatus = ROTATION_STATUS.NO_ROTATION;
                                BuzzerPlayer.BackgroundStop();
                                if (AgvInstance.Parameters.SoundsParams.slowDownAndRotatinSoundPlay.SoundPlayType == SOUND_TYPE.MUSIC_AUDIO)
                                {
                                    BuzzerPlayer.Move();
                                }
                                break;
                            }

                            if (currentSubState == SUB_STATUS.ALARM)
                            {
                                RotateStatus = ROTATION_STATUS.ROTATING_BUT_STOP_BY_OBSTACLE;
                                BuzzerPlayer.BackgroundStop();
                                break;
                            }

                            if (currentSubState == SUB_STATUS.DOWN)
                            {
                                RotateStatus = ROTATION_STATUS.NO_ROTATION;
                                BuzzerPlayer.BackgroundStop();
                            }

                            break;
                        case ROTATION_STATUS.ROTATING_BUT_STOP_BY_OBSTACLE:
                            if (currentSubState == SUB_STATUS.RUN)
                            {
                                RotateStatus = ROTATION_STATUS.ROTATATE_START;
                                break;
                            }
                            if (currentSubState == SUB_STATUS.DOWN)
                            {
                                RotateStatus = ROTATION_STATUS.NO_ROTATION;
                                BuzzerPlayer.BackgroundStop();
                            }
                            break;
                        default:
                            break;
                    }

                }
            });
        }

        private async Task WatchSlowDownState()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);
                    if (AgvInstance == null || !AgvInstance.Parameters.SoundsParams.SlowDownPlayAudio)
                    {
                        continue;
                    }
                    SUB_STATUS currentSubState = AgvInstance.GetSub_Status();
                    ROBOT_CONTROL_CMD currentRoboControlCmd = AgvInstance.AGVC.CurrentSpeedControlCmd;

                    switch (SlowDownStatus)
                    {
                        case SLOW_DOWN_STATUS.NO_SLOW_DOWN:
                            if (AgvInstance == null || !AgvInstance.Parameters.SoundsParams.slowDownAndRotatinSoundPlay.Enable || AgvInstance.AGVC.ActionStatus != ActionStatus.ACTIVE || RotateStatus == ROTATION_STATUS.ROTATING)
                                break;

                            if (currentRoboControlCmd == ROBOT_CONTROL_CMD.DECELERATE && currentSubState != SUB_STATUS.ALARM)
                            {
                                SlowDownStatus = SLOW_DOWN_STATUS.SLOW_DOWN_START;
                                break;
                            }

                            break;
                        case SLOW_DOWN_STATUS.SLOW_DOWN_START:

                            if (AgvInstance.Parameters.SoundsParams.slowDownAndRotatinSoundPlay.SoundPlayType == SOUND_TYPE.BG_VOICE)
                                BuzzerPlayer.PlayInBackground(SOUNDS.SlowDownVoice);
                            else
                            {
                                BuzzerPlayer.Stop();//把本來撥放中的音效停止
                                BuzzerPlayer.PlayInBackground(SOUNDS.SlowDownMusic);
                            }
                            SlowDownStatus = SLOW_DOWN_STATUS.SLOW_DOWNING;
                            break;
                        case SLOW_DOWN_STATUS.SLOW_DOWNING:

                            if (currentRoboControlCmd == ROBOT_CONTROL_CMD.SPEED_Reconvery || RotateStatus == ROTATION_STATUS.ROTATING)
                            {
                                BuzzerPlayer.BackgroundStop();
                                SlowDownStatus = SLOW_DOWN_STATUS.NO_SLOW_DOWN;
                                break;
                            }
                            if (currentRoboControlCmd == ROBOT_CONTROL_CMD.STOP)
                            {
                                BuzzerPlayer.BackgroundStop();
                                SlowDownStatus = SLOW_DOWN_STATUS.SLOW_DOWN_BUT_STOP_BY_OBSTACLE;
                            }

                            break;
                        case SLOW_DOWN_STATUS.SLOW_DOWN_BUT_STOP_BY_OBSTACLE:
                            if (currentRoboControlCmd != ROBOT_CONTROL_CMD.STOP)
                            {
                                SlowDownStatus = SLOW_DOWN_STATUS.SLOW_DOWN_START;
                                break;
                            }
                            if (currentSubState == SUB_STATUS.DOWN)
                            {
                                SlowDownStatus = SLOW_DOWN_STATUS.NO_SLOW_DOWN;
                                BuzzerPlayer.BackgroundStop();
                            }
                            break;
                        default:
                            break;
                    }
                }
            });
        }
    }
}
