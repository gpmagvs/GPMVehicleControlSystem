using NLog;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.VehicleControl.DIOModule
{
    public class clsIOSignal
    {
        public clsIOSignal(string Name, string Address)
        {
            this.Name = Name;
            this.Address = Address;
            logger = LogManager.GetCurrentClassLogger();
        }



        private Logger logger;

        public event EventHandler OnSignalON;
        public event EventHandler OnSignalOFF;
        public event EventHandler<bool> OnStateChanged;

        static List<DI_ITEM> handshake_inputs = new List<DI_ITEM>()
        {
             DI_ITEM.EMO,
             DI_ITEM.Panel_Reset_PB,
             DI_ITEM.Horizon_Motor_Switch,
             DI_ITEM.EQ_GO,
             DI_ITEM.EQ_BUSY,
             DI_ITEM.EQ_VALID,
             DI_ITEM.EQ_READY,
             DI_ITEM.EQ_Check_Ready,
             DI_ITEM.EQ_Check_Result,
             DI_ITEM.EQ_U_REQ,
             DI_ITEM.EQ_L_REQ,
             DI_ITEM.EQ_COMPT,
             DI_ITEM.EQ_TR_REQ,
             DI_ITEM.EQ_UP_READY,
             DI_ITEM.EQ_U_REQ,
             DI_ITEM.EQ_VALID,
             DI_ITEM.TRAY_Exist_Sensor_1,
             DI_ITEM.TRAY_Exist_Sensor_2,
             DI_ITEM.TRAY_Exist_Sensor_3,
             DI_ITEM.TRAY_Exist_Sensor_4,
             DI_ITEM.RACK_Exist_Sensor_1,
             DI_ITEM.RACK_Exist_Sensor_2
        };
        public static List<DO_ITEM> handshake_outputs = new List<DO_ITEM>()
        {
            DO_ITEM.AGV_TR_REQ,
            DO_ITEM.AGV_BUSY,
            DO_ITEM.AGV_VALID,
            DO_ITEM.AGV_READY,
            DO_ITEM.AGV_Check_REQ,
            DO_ITEM.AGV_COMPT,
            DO_ITEM.AGV_CS_0,
            DO_ITEM.AGV_CS_1,
            DO_ITEM.AGV_L_REQ,
            DO_ITEM.AGV_U_REQ,
        };
        /// <summary>
        /// Input定義
        /// </summary>
        internal DI_ITEM Input
        {
            get
            {
                return Enum.GetValues(typeof(DI_ITEM)).Cast<DI_ITEM>().FirstOrDefault(di => di.ToString() == Name);
            }
        }

        /// <summary>
        /// Input定義
        /// </summary>
        internal DO_ITEM Output
        {
            get
            {
                return Enum.GetValues(typeof(DO_ITEM)).Cast<DO_ITEM>().FirstOrDefault(di => di.ToString() == Name);
            }
        }

        public string Name { get; }
        public string Address { get; }
        public bool State
        {
            get => _State;
            set
            {
                if (_State != value)
                {
                    _State = value;
                    logger.Info($"[IO]-[{Address}]-{Name} Changed to : {(value ? 1 : 0)}");

                    if (handshake_outputs.Any(i => i.ToString() == Name) || handshake_inputs.Any(i => i.ToString() == Name))
                    {
                        Logger hslogger = LogManager.GetLogger("HandshakeLog");
                        hslogger.Info($"[IO]-[{Address}]-{Name} Changed to : {(value ? 1 : 0)}");
                    }

                    OnStateChanged?.Invoke(this, value);
                    if (_State)
                        OnSignalON?.Invoke(this, EventArgs.Empty);
                    else
                        OnSignalOFF?.Invoke(this, EventArgs.Empty);
                }
            }
        }


        private bool _State;
        internal ushort index;
        internal void AddEvent(EventHandler<bool> handler)
        {
            void _HandleInputStateChanged(object? sender, bool e)
            {
                Task.Run(() =>
                {
                    handler.Invoke(sender, e);
                });
            }
            OnStateChanged += _HandleInputStateChanged;
            logger.Trace($"{Name} event registed.!");
        }
    }
}
