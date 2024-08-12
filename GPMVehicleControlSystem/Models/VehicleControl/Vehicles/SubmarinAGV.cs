using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Service;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Microsoft.AspNetCore.SignalR;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 潛盾
    /// </summary>
    public partial class SubmarinAGV : Vehicle
    {

        public SubmarinAGV(ILogger<Vehicle> logger, ILogger<clsAGVSConnection> agvsLogger, IHubContext<FrontendHub> frontendHubContext) : base(logger, agvsLogger, frontendHubContext)
        {

        }

        protected override List<CarComponent> CarComponents
        {
            get
            {
                var baseCompos = base.CarComponents;
                baseCompos.Add(CSTReader);
                return baseCompos;
            }
        }

        public override clsCSTReader CSTReader { get; set; } = new clsCSTReader();
        public override clsDirectionLighter DirectionLighter { get; set; } = new clsDirectionLighter();
        public override Dictionary<ushort, clsBattery> Batteries { get; set; } = new Dictionary<ushort, clsBattery>();
        public ILogger Logger { get; }

        private SemaphoreSlim _AutoResetHorizonMotorSemaphosre = new SemaphoreSlim(1, 1);

        protected override RunningStatus HandleTcpIPProtocolGetRunningStatus()
        {
            var status = base.HandleTcpIPProtocolGetRunningStatus();
            status.CSTID = new string[] { CSTReader.ValidCSTID };
            return status;
        }
        public override clsRunningStatus HandleWebAPIProtocolGetRunningStatus()
        {
            var status = base.HandleWebAPIProtocolGetRunningStatus();
            status.CSTID = new string[] { CSTReader.ValidCSTID };
            return status;
        }


        protected override void ModuleInformationHandler(object? sender, ModuleInformation _ModuleInformation)
        {
            CSTReader.StateData = _ModuleInformation.CSTReader;
            base.ModuleInformationHandler(sender, _ModuleInformation);
        }

        /// <summary>
        /// 移除卡夾 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<RETURN_CODE> RemoveCstData()
        {
            //向AGVS請求移除卡匣
            logger.LogTrace($"使用者進行'移除卡匣'操作");
            CSTReader.ValidCSTID = "";
            simulation_cargo_status = CARGO_STATUS.NO_CARGO;
            return RETURN_CODE.OK;
        }

        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_L_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ].State = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_U_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ].State = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_READY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_READY].State = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_BUSY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].State = state; });
            WagoDI.SubsSignalStateChange(DI_ITEM.EQ_GO, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].State = state; });

            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_1, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_2, HandleDriversStatusErrorAsync);

            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_1, AutoResetHorizonMotor);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Alarm_2, AutoResetHorizonMotor);


            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_VALID, (sender, state) => { IsHandshaking = state; AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_TR_REQ, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_READY, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_READY] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_BUSY, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY] = state; });
            WagoDO.SubsSignalStateChange(DO_ITEM.AGV_COMPT, (sender, state) => { AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT] = state; });
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_L_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ].State = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_U_REQ, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ].State = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_READY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_READY].State = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_BUSY, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].State = state; });
                WagoDO.SubsSignalStateChange(DO_ITEM.EMU_EQ_GO, (sender, state) => { EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].State = state; });
                logger.LogInformation($"Handshake emulation mode, regist DO 0-6 ad PIO EQ Inputs ");
            }
        }
        protected virtual async void AutoResetHorizonMotor(object? sender, bool alarm)
        {
            bool isEMO = !WagoDI.GetState(DI_ITEM.EMO);
            if (!alarm || isEMO || !IsMotorAutoRecoverable())
                return;
            clsIOSignal input = sender as clsIOSignal;
            AutoReset();
            async Task AutoReset()
            {
                try
                {
                    await _AutoResetHorizonMotorSemaphosre.WaitAsync();
                    if (!IsAnyHorizonMotorAlarm())
                    {
                        logger.LogInformation($"因馬達異常已清除,{input?.Name}異常自動復位取消");
                        return;
                    }
                    logger.LogWarning($"於{lastVisitedMapPoint.Graph.Display}中發生走行馬達異常({input?.Name})，進行自動復位");
                    AlarmManager.AddWarning(input.Input == DI_ITEM.Horizon_Motor_Alarm_1 ? AlarmCodes.Wheel_Motor_IO_Error_Right : AlarmCodes.Wheel_Motor_IO_Error_Left);
                    await Task.Delay(1000);
                    await Initialize();
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    _AutoResetHorizonMotorSemaphosre.Release();
                }
            }

            bool _IsMotorNoAlarm()
            {
                return !WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) && !WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2);
            }

        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new SubmarinAGVControl(RosBridge_IP, RosBridge_Port);
            (AGVC as SubmarinAGVControl).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;
            logger.LogTrace($"(AGVC as SubmarinAGVControl).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;");

        }
    }
}
