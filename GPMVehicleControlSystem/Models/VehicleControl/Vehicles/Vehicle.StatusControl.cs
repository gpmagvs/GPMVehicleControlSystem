#define UseDebunce
using AGVSystemCommonNet6;
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

        internal SUB_STATUS _Sub_Status = SUB_STATUS.DOWN;
        private SUB_STATUS BeforeChargingSubStatus;

        /// <summary>
        /// 上報給派車系統的主狀態
        /// </summary>
        public MAIN_STATUS Main_Status { get; private set; } = MAIN_STATUS.DOWN;

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
        public async Task SetSub_Status(SUB_STATUS value)
        {
#if UseDebunce

            UpdateMainStatus(value);
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
                        if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                            DirectionLighter.CloseAll();
                        StatusLighter.DOWN();
                    }
                    else if (_Sub_Status == SUB_STATUS.IDLE)
                    {
                        guardVideoService.StopRecord();

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

        private void UpdateMainStatus(SUB_STATUS sub_Status)
        {

            if (!ModuleInformationUpdatedInitState)
                Main_Status = MAIN_STATUS.DOWN;

            SUB_STATUS[] _ignoreChangeSubStatus = new SUB_STATUS[] { SUB_STATUS.ALARM, SUB_STATUS.WARNING, SUB_STATUS.STOP };

            Dictionary<SUB_STATUS, MAIN_STATUS> statusConvertMap = new Dictionary<SUB_STATUS, MAIN_STATUS>()
            {
                { SUB_STATUS.Initializing , MAIN_STATUS.Initializing },
                { SUB_STATUS.RUN, MAIN_STATUS.RUN},
                { SUB_STATUS.DOWN, MAIN_STATUS.DOWN},
                { SUB_STATUS.IDLE, MAIN_STATUS.IDLE},
                { SUB_STATUS.Charging, MAIN_STATUS.Charging},
                { SUB_STATUS.UNKNOWN, MAIN_STATUS.Unknown},
            };

            if (_ignoreChangeSubStatus.Contains(sub_Status))
                return;

            Main_Status = statusConvertMap[sub_Status];

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
            bool isAgvRunning = AGVC.ActionStatus == ActionStatus.ACTIVE || AGVC.ActionStatus == ActionStatus.PENDING;
            bool isVehicleAtChargstation = lastVisitedMapPoint.IsChargeAble();

            if (!isVehicleAtChargstation || isAgvRunning)
            {
                _IsCharging = false;
                return;
            }

            if (_Sub_Status == SUB_STATUS.DOWN || _Sub_Status == SUB_STATUS.Initializing || !IsInitialized)
            {
                _IsCharging = false;
                return;
            }

            if (_IsCharging != value)
            {
                if (value)
                {
                    BeforeChargingSubStatus = _Sub_Status;
                    SetSub_Status(SUB_STATUS.Charging);
                    StatusLighter.ActiveGreen();
                }
                else
                {
                    SetSub_Status(SUB_STATUS.IDLE);
                }
                _IsCharging = value;
            }
        }

    }
}
