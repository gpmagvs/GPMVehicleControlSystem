using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private SUB_STATUS BeforeChargingSubStatus;

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
        public async void SetSub_Status(SUB_STATUS value, bool auto_stop_buzzer = true)
        {
            if (_Sub_Status != value)
            {
                await subStatusSemaphore.WaitAsync();
                try
                {
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
                        if (auto_stop_buzzer)
                            BuzzerPlayer.Stop();
                        StatusLighter.IDLE();
                        DirectionLighter.CloseAll();
                    }
                    else if (value == SUB_STATUS.RUN)
                    {
                        StatusLighter.RUN();
                    }
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    _Sub_Status = value;
                    LOG.TRACE($"Sub_Status change to {value}");
                    StoreStatusToDataBase();
                    subStatusSemaphore.Release();
                }
            }

            string GetCallerClassName()
            {
                var caller_class_declaring = new StackTrace().GetFrame(2).GetMethod().DeclaringType;
                if (caller_class_declaring == null || caller_class_declaring.DeclaringType == null)
                    return "ClassClass";
                return caller_class_declaring.Name;
            }
        }

        /// <summary>
        /// 上報給派車系統的主狀態
        /// </summary>
        public MAIN_STATUS Main_Status
        {
            get
            {
                if (!IsInitialized || !ModuleInformationUpdatedInitState)
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
                        return MAIN_STATUS.DOWN;
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
