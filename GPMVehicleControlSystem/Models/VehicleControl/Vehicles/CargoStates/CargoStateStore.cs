
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
        private readonly bool simulationExistByHaseCstID;
        private readonly clsCSTReader reader;

        public CargoStateStore(List<clsIOSignal> DigitalInputState, IHubContext<FrontendHub> hubContext = null, bool simulationExistByHaseCstID = false, clsCSTReader reader = null)
        {
            digitalInputState = DigitalInputState;
            this.hubContext = hubContext;
            this.simulationExistByHaseCstID = simulationExistByHaseCstID;
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
            existSensorDebouncer.Debounce(() => { DetermineCargoState(); }, 300);
        }

        internal bool HasAnyCargoOnAGV(bool isLDULDNoEntryNow)
        {
            if (simulationExistByHaseCstID && !string.IsNullOrEmpty(reader?.ValidCSTID))
                return true;
            if (IsCargoDetectedByInteruptSensor())
                return true;
            var currentCargoStatus = GetCargoStatus(isLDULDNoEntryNow, out CST_TYPE cargoType);
            return currentCargoStatus != CARGO_STATUS.NO_CARGO;
        }
        private bool IsCargoDetectedByInteruptSensor()
        {
            bool _IsSensorMounted = digitalInputState.Any(item => item.Input == DI_ITEM.Carrier_Exist_Interupt_Sensor);
            if (!_IsSensorMounted)
                return false;
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
        internal CARGO_STATUS GetCargoStatus(bool isLDULDNoEntryNow)
        {
            return GetCargoStatus(isLDULDNoEntryNow, out _);
        }
        internal virtual CARGO_STATUS GetCargoStatus(bool isLDULDNoEntryNow, out CST_TYPE cargoType)
        {
            cargoType = CST_TYPE.None;

            if (simulationExistByHaseCstID && !string.IsNullOrEmpty(reader?.ValidCSTID))
            {
                cargoType = CST_TYPE.Tray;
                return CARGO_STATUS.HAS_CARGO_NORMAL;
            }

            if (isLDULDNoEntryNow)
            {
                return simulation_cargo_status;
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
    }
}
