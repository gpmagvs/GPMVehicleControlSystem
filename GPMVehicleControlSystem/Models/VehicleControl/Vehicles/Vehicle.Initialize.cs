using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using Microsoft.OpenApi.Extensions;
using NLog;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {/// <summary>
     /// 初始化AGV
     /// </summary>
     /// <returns></returns>
        public async Task<(bool confirm, string message, string message_eng)> Initialize()
        {

            if (Sub_Status == SUB_STATUS.RUN)
            {
                return (false, $"當前狀態不可進行初始化(任務執行中)", "Initialization cannot be performed in the current state(Task Running)");
            }

            if (Sub_Status != SUB_STATUS.DOWN && (AGVC.ActionStatus == ActionStatus.ACTIVE || Sub_Status == SUB_STATUS.Initializing))
            {
                string reason_string = Sub_Status != SUB_STATUS.RUN ? (Sub_Status == SUB_STATUS.Initializing ? "初始化程序執行中" : "任務進行中") : "AGV狀態為RUN";
                return (false, $"當前狀態不可進行初始化({reason_string})", "Initialization cannot be performed in the current state");
            }
            orderInfoViewModel.ActionName = ACTION_TYPE.NoAction;

            if ((Parameters.AgvType == AGV_TYPE.FORK || Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD))
            {
                var _currentCargoStatus = GetCargoStatus(out List<DI_ITEM> abnormalSignals);
                if (_currentCargoStatus == CARGO_STATUS.HAS_CARGO_BUT_BIAS)
                {
                    List<string> abnSensorDesLs = WagoDI.VCSInputs.Where(i => abnormalSignals.Contains(i.Input))
                                    .Select(i => i.Address + $"({i.Name})")
                                    .ToList();
                    AlarmManager.AddWarning(AlarmCodes.Cst_Slope_Error);
                    return (false, $"貨物傾斜異常！請立即檢查並重新擺放貨物，以確保安全。\n [{string.Join("、", abnSensorDesLs)} 未檢出]", "Cargo tilt detected! Please check and reposition the cargo immediately to ensure safety.");
                }
                if (_currentCargoStatus == CARGO_STATUS.NO_CARGO && CSTReader.ValidCSTID != "")
                {
                    AlarmManager.AddWarning(AlarmCodes.Has_Data_But_No_Cargo);
                    return (false, "有貨物帳籍但未偵測到貨物存在。請檢查貨物擺放情況或清除貨物帳籍(點擊左側 [移除卡匣] 按鈕)。", "Cargo is recorded but not detected. Please check the cargo placement or clear the cargo record (click the [Remove CST] button on the left).");
                }

                if (_currentCargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL && CSTReader.ValidCSTID == "")
                {
                    AlarmManager.AddWarning(AlarmCodes.Has_Cargo_But_No_Data);
                    return (false, "偵測到有貨物但無帳籍資料，請先移除貨物或建立帳籍資料", "");
                }

                if (Parameters.Auto_Cleaer_CST_ID_Data_When_Has_Data_But_NO_Cargo && !HasAnyCargoOnAGV() && CSTReader.ValidCSTID != "")
                {
                    CSTReader.ValidCSTID = "";
                    LOG.WARN($"偵測到AGV有帳無料，已完成自動清帳");
                }
                if (Parameters.Auto_Read_CST_ID_When_No_Data_But_Has_Cargo && HasAnyCargoOnAGV() && CSTReader.ValidCSTID == "")
                {
                    (bool request_success, bool action_done) result = await AGVC.TriggerCSTReader();
                    if (result.request_success)
                        LOG.WARN($"偵測到AGV無帳有料，已完成自動建帳");
                }
            }
            if (!this.ModuleInformationUpdatedInitState)
            {
                return (false, "與車控之間的通訊尚未完成，無法初始化", "Communication with the vehicle control system is not yet complete, initialization cannot be performed.\r\n");
            }
            IsHandshaking = AGVSResetCmdFlag = IsWaitForkNextSegmentTask = false;
            InitializeCancelTokenResourece = new CancellationTokenSource();
            AlarmManager.ClearAlarm();
            HandshakeStatusText = "";
            return await Task.Run(async () =>
            {
                StopAllHandshakeTimer();
                StatusLighter.FlashAsync(DO_ITEM.AGV_DiractionLight_Y, 600);
                try
                {
                    IsMotorReseting = false;
                    await ResetMotor(callerName: "AGV Initialize Process");
                    (bool confirm, string message, string message_eng) result = await PreActionBeforeInitialize();
                    if (!result.confirm)
                    {
                        StatusLighter.AbortFlash();
                        return result;
                    }
                    InitializingStatusText = "初始化開始";
                    Sub_Status = SUB_STATUS.Initializing;
                    await Task.Delay(500);
                    IsInitialized = false;

                    result = await InitializeActions(InitializeCancelTokenResourece);
                    if (!result.Item1)
                    {
                        Sub_Status = SUB_STATUS.STOP;
                        IsInitialized = false;
                        StatusLighter.AbortFlash();
                        return result;
                    }
                    InitializingStatusText = "雷射模式切換(Bypass)..";
                    await Task.Delay(200);
                    bool _laserModeBypass = await Laser.ModeSwitch(LASER_MODE.Bypass);
                    if (!_laserModeBypass)
                        throw new Exception("雷射設定失敗");
                    await Laser.AllLaserDisable();
                    await Task.Delay(Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD ? 500 : 1000);
                    StatusLighter.AbortFlash();
                    DirectionLighter.CloseAll();
                    InitializingStatusText = "初始化完成!";
                    await Task.Delay(500);
                    Sub_Status = SUB_STATUS.IDLE;
                    AGVC._ActionStatus = ActionStatus.NO_GOAL;
                    IsInitialized = true;
                    LOG.INFO("Init done, and Laser mode chaged to Bypass");
                    return (true, "", "");
                }
                catch (TaskCanceledException ex)
                {
                    StatusLighter.AbortFlash();
                    _Sub_Status = SUB_STATUS.DOWN;
                    IsInitialized = false;
                    LOG.Critical($"AGV Initizlize Task Canceled! : \r\n{ex.Message}", ex);
                    return (false, $"AGV Initizlize Task Canceled! : \r\n{ex.Message}", "AGV Initizlize Task Canceled");
                }
                catch (Exception ex)
                {
                    StatusLighter.AbortFlash();
                    _Sub_Status = SUB_STATUS.DOWN;
                    BuzzerPlayer.Alarm();
                    IsInitialized = false;
                    return (false, $"AGV Initizlize Fail ! : \r\n{ex.Message}", "AGV Initizlize Fail");
                }

            }, InitializeCancelTokenResourece.Token);
        }

        protected virtual async Task<(bool, string message, string message_eng)> PreActionBeforeInitialize()
        {
            if (ExecutingTaskModel != null)
            {
                ExecutingTaskModel.AGVCActionStatusChaged = null;
            }
            AGVC.OnAGVCActionChanged = null;
            IMU.OnAccelermeterDataChanged -= HandleIMUVibrationDataChanged;
            await AGVC.SendGoal(new AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal());
            ExecutingTaskModel = null;
            BuzzerPlayer.Stop();
            DirectionLighter.CloseAll();
            if (EQAlarmWhenEQBusyFlag && WagoDI.GetState(DI_ITEM.EQ_BUSY))
            {
                return (false, $"端點設備({lastVisitedMapPoint.Name})尚未進行復歸，AGV禁止復歸", "Endpoint device has not been reset, AGV reset is prohibited");
            }

            AGVAlarmWhenEQBusyFlag = false;
            EQAlarmWhenEQBusyFlag = false;
            ResetHandshakeSignals();
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
            var hardware_status_check_reuslt = CheckHardwareStatus();
            if (!hardware_status_check_reuslt.confirm)
                return (false, hardware_status_check_reuslt.message, hardware_status_check_reuslt.message_eng);
            //if (Sub_Status == SUB_STATUS.Charging)
            //    return (false, "無法在充電狀態下進行初始化");
            //bool forkRackExistAbnormal = !WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor) | !WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
            //if (forkRackExistAbnormal)
            //    return (false, "無法在有Rack的狀態下進行初始化");

            //if (lastVisitedMapPoint.StationType !=STATION_TYPE.Normal)
            //    return (false, $"無法在非一般點位下進行初始化({lastVisitedMapPoint.StationType})");
            AlarmCodeWhenHandshaking = AlarmCodes.None;
            if (!WagoDI.Connected)
                return (false, $"DIO 模組連線異常", "DIO module connection error.\r\n");
            return (true, "", "");
        }


        public virtual (bool confirm, string message, string message_eng) CheckHardwareStatus()
        {
            AlarmCodes alarmo_code = AlarmCodes.None;
            string error_message = "";
            string error_message_eng = "";
            if (CheckMotorIOError())
            {
                error_message = "走行軸馬達IO異常";
                error_message_eng = "Motor IO error";
                alarmo_code = AlarmCodes.Wheel_Motor_IO_Error;
            }

            if (CheckEMOButtonNoRelease())
            {
                error_message = "EMO 按鈕尚未復歸";
                error_message_eng = "The EMO button has not been reset";
                alarmo_code = AlarmCodes.EMO_Button;
            }

            if (!WagoDI.GetState(DI_ITEM.Horizon_Motor_Switch))
            {
                error_message = "解煞車旋鈕尚未復歸";
                error_message_eng = "The brake release knob has not been reset";
                alarmo_code = AlarmCodes.Switch_Type_Error;
            }
            if (IMU.PitchState != clsIMU.PITCH_STATES.NORMAL)
            {
                error_message = $"AGV姿態異常({(IMU.PitchState == clsIMU.PITCH_STATES.INCLINED ? "傾斜" : "側翻")})";
                error_message = $"AGV Current Posture abnormality";
                alarmo_code = AlarmCodes.IMU_Pitch_State_Error;
            }
            if (alarmo_code == AlarmCodes.None)
                return (true, "", "");
            else
            {
                AlarmManager.AddAlarm(alarmo_code, false);
                BuzzerPlayer.Alarm();
                return (false, error_message, error_message_eng);
            }
        }

        protected virtual bool CheckEMOButtonNoRelease()
        {
            return !WagoDI.GetState(DI_ITEM.EMO);
        }

        protected virtual bool CheckMotorIOError()
        {
            return WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2);
        }

        protected abstract Task<(bool confirm, string message, string message_eng)> InitializeActions(CancellationTokenSource cancellation);

        /// <summary>
        /// Reset交握訊號
        /// </summary>
        internal virtual async void ResetHandshakeSignals()
        {

            Logger logger = GetHsIOLogger();
            logger.Info($"Reset PIO Status of AGV.");

            await WagoDO.SetState(DO_ITEM.AGV_VALID, false);
            await WagoDO.SetState(DO_ITEM.AGV_COMPT, false);
            await WagoDO.SetState(DO_ITEM.AGV_BUSY, false);
            await WagoDO.SetState(DO_ITEM.AGV_READY, false);
            await WagoDO.SetState(DO_ITEM.AGV_TR_REQ, false);

            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                await WagoDO.SetState(DO_ITEM.EMU_EQ_BUSY, false);
                await WagoDO.SetState(DO_ITEM.EMU_EQ_L_REQ, false);
                await WagoDO.SetState(DO_ITEM.EMU_EQ_U_REQ, false);
                await WagoDO.SetState(DO_ITEM.EMU_EQ_READY, false);
            }
        }

    }
}
