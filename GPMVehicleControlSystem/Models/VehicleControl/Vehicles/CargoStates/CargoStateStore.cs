
using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Service;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Microsoft.AspNetCore.SignalR;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.CargoStates
{
    public class CargoStateStore
    {
        private enum CARGO_TYPE
        {
            TRAY,
            RACK,
        }

        public static List<DI_ITEM> trayExistSensorItems = new List<DI_ITEM>()
        {
                DI_ITEM.TRAY_Exist_Sensor_1,
                DI_ITEM.TRAY_Exist_Sensor_2,
                DI_ITEM.TRAY_Exist_Sensor_3,
                DI_ITEM.TRAY_Exist_Sensor_4,
        };
        public static List<DI_ITEM> rackExistSensorItems = new List<DI_ITEM>()
        {
                DI_ITEM.RACK_Exist_Sensor_1,
                DI_ITEM.RACK_Exist_Sensor_2,
        };

        public CARGO_STATUS TrayCargoStatus { get; private set; } = CARGO_STATUS.NO_CARGO;
        public CARGO_STATUS RackCargoStatus { get; private set; } = CARGO_STATUS.NO_CARGO;

        internal CARGO_STATUS simulation_cargo_status = CARGO_STATUS.NO_CARGO;
        private Tools.Debouncer existSensorDebouncer = new Tools.Debouncer();
        private readonly List<clsIOSignal> digitalInputState = new List<clsIOSignal>();
        private readonly IHubContext<FrontendHub> hubContext;
        private readonly clsCSTReader reader;

        internal ManualResetEvent waitOperatorConfirmCargoStatus = new ManualResetEvent(false);

        internal CancellationTokenSource watchCargoExistStateCts = new CancellationTokenSource();

        internal IsUseCarrierIdExistToSimulationCargoExistDelegate OnUseCarrierIdExistToSimulationCargoExistInvoked;

        internal delegate bool IsUseCarrierIdExistToSimulationCargoExistDelegate();

        private bool _IsCarrier_Exist_Interupt_SensorMounted => digitalInputState.Any(item => item.Input == DI_ITEM.Carrier_Exist_Interupt_Sensor);

        public CargoStateStore(List<clsIOSignal> DigitalInputState, IHubContext<FrontendHub> hubContext = null, clsCSTReader reader = null)
        {
            digitalInputState = DigitalInputState;
            this.hubContext = hubContext;
            this.reader = reader;
        }

        public bool IsTraySensorMounted()
        {
            return digitalInputState.Any(item => CargoStateStore.trayExistSensorItems.Contains(item.Input));
        }
        public bool IsRackSensorMounted()
        {
            return digitalInputState.Any(item => CargoStateStore.rackExistSensorItems.Contains(item.Input));
        }

        internal void HandleCargoExistSensorStateChanged(object? sender, EventArgs eventArgs)
        {
            existSensorDebouncer.Debounce(() => { DetermineCargoState(); }, 100);
        }

        internal bool IsCargoMountedNormal(bool isLDULDNoEntryNow)
        {
            if (TryReturnCargoExistByCheckSimulationModeInvoke(out bool hasCargo))
                return hasCargo;

            var currentCargoStatus = GetCargoStatus(isLDULDNoEntryNow, out CST_TYPE cargoType);
            if (_IsCarrier_Exist_Interupt_SensorMounted)
                return currentCargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL && IsCargoDetectedByInteruptSensor();
            else
                return currentCargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL;

        }

        private bool TryReturnCargoExistByCheckSimulationModeInvoke(out bool hasCargo)
        {
            hasCargo = false;
            if (OnUseCarrierIdExistToSimulationCargoExistInvoked == null || !OnUseCarrierIdExistToSimulationCargoExistInvoked.Invoke())
                return false;
            hasCargo = !string.IsNullOrEmpty(reader?.ValidCSTID);
            return true;
        }

        internal bool HasAnyCargoOnAGV(bool isLDULDNoEntryNow)
        {

            if (TryReturnCargoExistByCheckSimulationModeInvoke(out bool hasCargo))
                return hasCargo;

            if (_IsCarrier_Exist_Interupt_SensorMounted && IsCargoDetectedByInteruptSensor())
                return true;
            var currentCargoStatus = GetCargoStatus(isLDULDNoEntryNow, out CST_TYPE cargoType);
            return currentCargoStatus != CARGO_STATUS.NO_CARGO;
        }
        private bool IsCargoDetectedByInteruptSensor()
        {
            return !digitalInputState.First(item => item.Input == DI_ITEM.Carrier_Exist_Interupt_Sensor).State;
        }
        /// <summary>
        /// 取得載物的類型 0:tray, 1:rack , 200:tray
        /// </summary>
        /// <returns></returns>
        internal CST_TYPE GetCargoType()
        {
            GetCargoStatus(false, out CST_TYPE cargoType);
            return cargoType;
        }

        internal CARGO_STATUS GetCargoStatus(bool isLDULDNoEntryNow, CST_TYPE cstTypeFromAGVSOrder)
        {
            return GetCargoStatus(isLDULDNoEntryNow, out _, cstTypeFromAGVSOrder);
        }

        internal CARGO_STATUS GetCargoStatus(bool isLDULDNoEntryNow)
        {
            return GetCargoStatus(isLDULDNoEntryNow, out _);
        }
        internal virtual CARGO_STATUS GetCargoStatus(bool isLDULDNoEntryNow, out CST_TYPE cargoType, CST_TYPE cstTypeFromAGVSOrder = CST_TYPE.Unknown)
        {
            cargoType = CST_TYPE.None;


            if (TryReturnCargoExistByCheckSimulationModeInvoke(out bool hasCargo))
            {
                if (hasCargo)
                {
                    cargoType = CST_TYPE.Tray;
                    return CARGO_STATUS.HAS_CARGO_NORMAL;
                }
                else
                {
                    cargoType = CST_TYPE.Unknown;
                    return CARGO_STATUS.NO_CARGO;
                }
            }

            if (isLDULDNoEntryNow)
            {
                return simulation_cargo_status;
            }

            bool isRackShouldBeMounted = cstTypeFromAGVSOrder == CST_TYPE.Rack;

            //這是特殊情況處理, 當派車命令指定Rack時, 只要 Rack有貨就視為有貨
            if (isRackShouldBeMounted && RackCargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL)
            {
                cargoType = CST_TYPE.Rack;
                return CARGO_STATUS.HAS_CARGO_NORMAL;
            }
            if (TrayCargoStatus != CARGO_STATUS.NO_CARGO && RackCargoStatus != CARGO_STATUS.NO_CARGO)
            {
                cargoType = CST_TYPE.Unknown;
                return CARGO_STATUS.HAS_CARGO_NORMAL;
            }
            if (TrayCargoStatus == CARGO_STATUS.NO_CARGO && RackCargoStatus == CARGO_STATUS.NO_CARGO)
            {
                cargoType = CST_TYPE.None;
                return CARGO_STATUS.NO_CARGO;
            }
            else if (TrayCargoStatus != CARGO_STATUS.NO_CARGO)
            {
                cargoType = CST_TYPE.Tray;
                return TrayCargoStatus;
            }
            else if (RackCargoStatus != CARGO_STATUS.NO_CARGO)
            {
                cargoType = CST_TYPE.Rack;
                return RackCargoStatus;
            }
            return CARGO_STATUS.NO_CARGO;
        }

        private void DetermineCargoState()
        {
            TrayCargoStatus = GetStatus(CARGO_TYPE.TRAY);
            RackCargoStatus = GetStatus(CARGO_TYPE.RACK);

            if (hubContext != null)
            {
                Task.Factory.StartNew(async () =>
                {
                    await BrocastCargoStatus();
                });
            }
        }

        private CARGO_STATUS GetStatus(CARGO_TYPE _type)
        {


            List<clsIOSignal> sensorStates = new List<clsIOSignal>();
            if (_type == CARGO_TYPE.TRAY)
            {
                if (!IsTraySensorMounted())
                    return CARGO_STATUS.NO_CARGO;
                sensorStates = digitalInputState.Where(item => CargoStateStore.trayExistSensorItems.Contains(item.Input))
                                                .ToList();
            }
            else
            {
                if (!IsRackSensorMounted())
                    return CARGO_STATUS.NO_CARGO;
                sensorStates = digitalInputState.Where(item => CargoStateStore.rackExistSensorItems.Contains(item.Input))
                                                .ToList();
            }
            if (sensorStates.All(sensor => !sensor.State))
                return CARGO_STATUS.HAS_CARGO_NORMAL;
            else if (sensorStates.All(sensor => sensor.State))
                return CARGO_STATUS.NO_CARGO;
            else
                return CARGO_STATUS.HAS_CARGO_BUT_BIAS;
        }


        private async Task BrocastCargoStatus()
        {
            CARGO_STATUS existStatus = GetCargoStatus(false, out CST_TYPE cargoType);
            hubContext?.Clients.All.SendAsync("CargoStatus", new
            {
                isCargoExist = existStatus != CARGO_STATUS.NO_CARGO,
                cargoType = cargoType
            });
        }

        internal void SetWaitOperatorConfirmCargoStatus()
        {
            waitOperatorConfirmCargoStatus.Set();
        }
    }
}
