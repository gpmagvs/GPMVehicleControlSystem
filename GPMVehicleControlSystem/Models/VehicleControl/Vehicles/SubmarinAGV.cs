using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Models.WorkStation;
using Newtonsoft.Json;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 潛盾
    /// </summary>
    public partial class SubmarinAGV : Vehicle
    {

        public SubmarinAGV() : base()
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
        public override CARGO_STATUS CargoStatus
        {
            get
            {
                return GetCargoStatus();
            }
        }
        protected override int GetCargoType()
        {
            return HasAnyCargoOnAGV() ? 200 : 0;
        }
        public override clsCSTReader CSTReader { get; set; } = new clsCSTReader();
        public override clsDirectionLighter DirectionLighter { get; set; } = new clsDirectionLighter();
        public override Dictionary<ushort, clsBattery> Batteries { get; set; } = new Dictionary<ushort, clsBattery>();

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
            LOG.TRACE($"使用者進行'移除卡匣'操作");
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
                LOG.INFO($"Handshake emulation mode, regist DO 0-6 ad PIO EQ Inputs ");
            }
        }
        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new SubmarinAGVControl(RosBridge_IP, RosBridge_Port);
            (AGVC as SubmarinAGVControl).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;
            LOG.TRACE($"(AGVC as SubmarinAGVControl).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;");

        }
        internal CARGO_STATUS simulation_cargo_status = CARGO_STATUS.NO_CARGO;
        protected virtual CARGO_STATUS GetCargoStatus()
        {
            if (Parameters.LDULD_Task_No_Entry)
            {
                return simulation_cargo_status;
            }

            CARGO_STATUS _tray_cargo_status = CARGO_STATUS.NO_CARGO;
            CARGO_STATUS _rack_cargo_status = CARGO_STATUS.NO_CARGO;

            CARGO_STATUS _GetCargoStatus(DI_ITEM sensor1, DI_ITEM sensor2, IO_CONEECTION_POINT_TYPE sensor1_connect_type, IO_CONEECTION_POINT_TYPE sensor2_connect_type)
            {
                bool existSensor_1 = sensor1_connect_type == IO_CONEECTION_POINT_TYPE.A ? WagoDI.GetState(sensor1) : !WagoDI.GetState(sensor1);
                bool existSensor_2 = sensor2_connect_type == IO_CONEECTION_POINT_TYPE.A ? WagoDI.GetState(sensor2) : !WagoDI.GetState(sensor2);
                if (existSensor_1 && existSensor_2)
                    return CARGO_STATUS.HAS_CARGO_NORMAL;
                if (!existSensor_1 && !existSensor_2)
                    return CARGO_STATUS.NO_CARGO;
                if ((!existSensor_1 && existSensor_2) || (existSensor_1 && !existSensor_2))
                    return CARGO_STATUS.HAS_CARGO_BUT_BIAS;
                else
                    return CARGO_STATUS.NO_CARGO;
            }

            if (Parameters.CargoExistSensorParams.TraySensorMounted)
            {
                var _connect_io_AB_type = Parameters.CargoExistSensorParams.TraySensorPointType;
                _tray_cargo_status = _GetCargoStatus(DI_ITEM.TRAY_Exist_Sensor_1, DI_ITEM.TRAY_Exist_Sensor_2, _connect_io_AB_type, _connect_io_AB_type);
            }
            if (Parameters.CargoExistSensorParams.RackSensorMounted)
            {
                var _connect_io_AB_type = Parameters.CargoExistSensorParams.RackSensorPointType;
                _rack_cargo_status = _GetCargoStatus(DI_ITEM.RACK_Exist_Sensor_2, DI_ITEM.RACK_Exist_Sensor_1, _connect_io_AB_type, _connect_io_AB_type);
            }
            CARGO_STATUS[] status_collection = new CARGO_STATUS[] { _tray_cargo_status, _rack_cargo_status };

            if (status_collection.All(status => status == CARGO_STATUS.NO_CARGO))
                return CARGO_STATUS.NO_CARGO;
            else
            {
                if (status_collection.Any(status => status == CARGO_STATUS.HAS_CARGO_BUT_BIAS))
                    return CARGO_STATUS.HAS_CARGO_BUT_BIAS;
                else
                    return CARGO_STATUS.HAS_CARGO_NORMAL;
            }
        }
    }
}
