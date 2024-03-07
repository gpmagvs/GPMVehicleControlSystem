using AGVSystemCommonNet6.Log;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
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
        }
        public event EventHandler OnSignalON;
        public event EventHandler OnSignalOFF;
        public event EventHandler<bool> OnStateChanged;

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
                return Enum.GetValues(typeof(DO_ITEM)).Cast<DO_ITEM>().First(di => di.ToString() == Name);
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
                    Task.Factory.StartNew(() =>
                    {
                        _ = LOG.INFO($"[IO]-[{Address}]-{Name} Changed to : {(value ? 1 : 0)}", color: ConsoleColor.Magenta); OnStateChanged?.Invoke(this, value);
                        if (_State)
                            OnSignalON?.Invoke(this, EventArgs.Empty);
                        else
                            OnSignalOFF?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }


        private bool _State;
        internal ushort index;
    }
}
