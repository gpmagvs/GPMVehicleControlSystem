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
        private object lock_obj = new object();

        /// <summary>
        /// 設定AGV目前的子狀態
        /// </summary>
        /// <param name="value"></param>
        public void SetSub_Status(SUB_STATUS value)
        {
            lock (lock_obj)
            {

                if (_Sub_Status != value)
                {
                    var _caller = GetCallerClassName();
                    try
                    {
                        if (value == SUB_STATUS.DOWN || value == SUB_STATUS.ALARM || value == SUB_STATUS.Initializing)
                        {
                            if (value == SUB_STATUS.DOWN)
                                HandshakeIOOff();
                            if (value != SUB_STATUS.Initializing && _Sub_Status != SUB_STATUS.Charging && Operation_Mode == OPERATOR_MODE.AUTO)
                                BuzzerPlayer.Alarm();
                            DirectionLighter.CloseAll(1000);
                            StatusLighter.DOWN();
                        }
                        else if (value == SUB_STATUS.IDLE)
                        {
                            BuzzerPlayer.Stop();
                            StatusLighter.IDLE();
                            DirectionLighter.CloseAll(1000);
                        }
                        else if (value == SUB_STATUS.RUN)
                        {
                            StatusLighter.RUN();
                        }

                    }
                    catch (Exception ex)
                    {
                    }

                    _Sub_Status = value;
                    LOG.TRACE($"Sub_Status change to {value} (caller:{_caller})");
                    StoreStatusToDataBase();
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
                if (!IsInitialized)
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
            if (IsChargeCircuitOpened && Batteries.Any(bat => bat.Value.Data.Voltage >= Parameters.BatteryModule.CutOffChargeRelayVoltageThreshodlval))
            {
                LOG.WARN($"Battery voltage  lower than threshold ({Parameters.BatteryModule.CutOffChargeRelayVoltageThreshodlval}) mV, cut off recharge circuit ! ");
                WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
                SetSub_Status(IsInitialized ? AGVC.ActionStatus == ActionStatus.ACTIVE ? SUB_STATUS.RUN : SUB_STATUS.IDLE : SUB_STATUS.DOWN);
                _IsCharging = false;
                return;
            }
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
