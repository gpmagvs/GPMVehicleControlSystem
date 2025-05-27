#define UseDebunce
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Tools;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private SUB_STATUS BeforeChargingSubStatus;

        private Debouncer subStatusChangeDebouncer = new Debouncer();

        private CancellationTokenSource delaySwitchDirectionLightsAsTrafficControllingCts = new CancellationTokenSource();

        /// <summary>
        /// 充電迴路是否已開啟
        /// </summary>
        public virtual bool IsChargeCircuitOpened => WagoDO.GetState(DO_ITEM.Recharge_Circuit);

        public SUB_STATUS GetSub_Status()
        {
            return _Sub_Status;
        }
        private SemaphoreSlim subStatusSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 設定AGV目前的子狀態
        /// </summary>
        /// <param name="value"></param>
        public async Task SetSub_Status(SUB_STATUS value, bool auto_stop_buzzer = true)
        {
#if UseDebunce
            subStatusChangeDebouncer.Debounce(() =>
            {
                try
                {
                    _Sub_Status = value;
                    if (_Sub_Status != SUB_STATUS.IDLE)
                    {
                        CancelSwitchToTrafficLightsCase();
                    }
                    //var _caller = GetCallerClassName();
                    if (_Sub_Status == SUB_STATUS.DOWN || _Sub_Status == SUB_STATUS.ALARM || _Sub_Status == SUB_STATUS.Initializing)
                    {
                        if (_Sub_Status == SUB_STATUS.DOWN)
                            HandshakeIOOff();
                        if (_Sub_Status != SUB_STATUS.Initializing && _Sub_Status != SUB_STATUS.Charging && Operation_Mode == OPERATOR_MODE.AUTO)
                            BuzzerPlayer.SoundPlaying = SOUNDS.Alarm;
                        if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                            DirectionLighter.CloseAll();
                        StatusLighter.DOWN();
                    }
                    else if (_Sub_Status == SUB_STATUS.IDLE)
                    {
                        guardVideoService.StopRecord();

                        if (auto_stop_buzzer)
                            BuzzerPlayer.SoundPlaying = SOUNDS.Stop;
                        StatusLighter.IDLE();
                        DirectionLighter.CloseAll();

                        if (Remote_Mode == AGVSystemCommonNet6.AGVDispatch.Messages.REMOTE_MODE.ONLINE && lastVisitedMapPoint.StationType == AGVSystemCommonNet6.MAP.MapPoint.STATION_TYPE.Normal)
                            SwitchDirectionLightAsWaitAGVSNextAction();
                    }
                    else if (_Sub_Status == SUB_STATUS.RUN)
                    {
                        StatusLighter.RUN();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[SetSub_Status] " + ex.StackTrace);
                }
                finally
                {
                    logger.LogTrace($"Sub_Status change to {value}");
                    StoreStatusToDataBase();
                }

            });

#else
            if (_Sub_Status != value)
            {
                await subStatusSemaphore.WaitAsync();
                try
                {
                    if (value != SUB_STATUS.IDLE)
                    {
                        try
                        {
                            delaySwitchDirectionLightsAsTrafficControllingCts?.Cancel();
                            delaySwitchDirectionLightsAsTrafficControllingCts?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[SetSub_Status::delaySwitchDirectionLightsAsTrafficControllingCts dispose prcess] " + ex.StackTrace);
                        }
                    }
                    //var _caller = GetCallerClassName();
                    if (value == SUB_STATUS.DOWN || value == SUB_STATUS.ALARM || value == SUB_STATUS.Initializing)
                    {
                        if (value == SUB_STATUS.DOWN)
                            HandshakeIOOff();
                        if (value != SUB_STATUS.Initializing && _Sub_Status != SUB_STATUS.Charging && Operation_Mode == OPERATOR_MODE.AUTO)
                            BuzzerPlayer.Alarm();
                        if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                            DirectionLighter.CloseAll();
                        StatusLighter.DOWN();
                    }
                    else if (value == SUB_STATUS.IDLE)
                    {
                        guardVideoService.StopRecord();

                        if (auto_stop_buzzer)
                            BuzzerPlayer.Stop("SetSub_Status Change to IDLE & auto_stop_buzzer");
                        StatusLighter.IDLE();
                        DirectionLighter.CloseAll();

                        if (Remote_Mode == AGVSystemCommonNet6.AGVDispatch.Messages.REMOTE_MODE.ONLINE && lastVisitedMapPoint.StationType == AGVSystemCommonNet6.MAP.MapPoint.STATION_TYPE.Normal)
                            SwitchDirectionLightAsWaitAGVSNextAction();
                    }
                    else if (value == SUB_STATUS.RUN)
                    {
                        StatusLighter.RUN();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[SetSub_Status] " + ex.StackTrace);
                }
                finally
                {
                    _Sub_Status = value;
                    logger.LogTrace($"Sub_Status change to {value}");
                    StoreStatusToDataBase();
                    subStatusSemaphore.Release();
                }
            }
#endif




            string GetCallerClassName()
            {
                var caller_class_declaring = new StackTrace().GetFrame(2).GetMethod().DeclaringType;
                if (caller_class_declaring == null || caller_class_declaring.DeclaringType == null)
                    return "ClassClass";
                return caller_class_declaring.Name;
            }
        }

        private async Task SwitchDirectionLightAsWaitAGVSNextAction()
        {
            try
            {
                delaySwitchDirectionLightsAsTrafficControllingCts = new CancellationTokenSource();
                await Task.Delay(TimeSpan.FromSeconds(5), delaySwitchDirectionLightsAsTrafficControllingCts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            DirectionLighter.TrafficControllingLightsFlash();
        }

        /// <summary>
        /// 上報給派車系統的主狀態
        /// </summary>
        public MAIN_STATUS Main_Status
        {
            get
            {
                if (!ModuleInformationUpdatedInitState)
                    return MAIN_STATUS.DOWN;
                switch (_Sub_Status)
                {
                    case SUB_STATUS.IDLE:
                        return MAIN_STATUS.IDLE;
                    case SUB_STATUS.RUN:
                        return MAIN_STATUS.RUN;
                    case SUB_STATUS.DOWN:
                        return MAIN_STATUS.DOWN;
                    case SUB_STATUS.Charging:
                        return MAIN_STATUS.Charging;
                    case SUB_STATUS.Initializing:
                        return MAIN_STATUS.Initializing;
                    case SUB_STATUS.ALARM:
                        if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                            return MAIN_STATUS.RUN;
                        else
                            return MAIN_STATUS.IDLE;
                    case SUB_STATUS.WARNING:
                        if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                            return MAIN_STATUS.RUN;
                        else
                            return MAIN_STATUS.IDLE;
                    case SUB_STATUS.STOP:
                        return MAIN_STATUS.IDLE;
                    default:
                        return MAIN_STATUS.DOWN;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool GetIsCharging()
        {
            return _IsCharging;
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetIsCharging(bool value)
        {
            if (_IsCharging != value)
            {
                if (value)
                {
                    BeforeChargingSubStatus = _Sub_Status;
                    _Sub_Status = SUB_STATUS.Charging;
                    StatusLighter.ActiveGreen();
                }
                else
                    SetSub_Status(IsInitialized ? AGVC.ActionStatus == ActionStatus.ACTIVE ? SUB_STATUS.RUN : SUB_STATUS.IDLE : SUB_STATUS.DOWN);
                _IsCharging = value;
            }
        }

    }
}
