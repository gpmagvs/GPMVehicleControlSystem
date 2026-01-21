using GPMVehicleControlSystem.VehicleControl.DIOModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.DIOModule
{
    public static class DOModuleExtension
    {
        public static clsIOSignal[] GetIOSignalOfModule(this DO_ITEM[] items)
        {
            try
            {
                return items.Select(item => item.GetIOSignalOfModule()).ToArray();
            }
            catch (Exception)
            {
                return new clsIOSignal[0];
            }
        }
        public static clsIOSignal GetIOSignalOfModule(this DO_ITEM item)
        {
            try
            {
                return StaStored.CurrentVechicle.WagoDO.VCSOutputs.FirstOrDefault(sig => sig.Output == item);
            }
            catch (Exception)
            {
                return new clsIOSignal("", "");
            }
        }
    }
    public class DOWriteRequest
    {
        public DOWriteRequest(IEnumerable<DOModifyWrapper> toModifyItems)
        {
            this.toModifyItems = toModifyItems.Where(item => item.signal != null).ToList();
        }
        public IEnumerable<DOModifyWrapper> toModifyItems = new List<DOModifyWrapper>();
        public List<DOModifyWrapper> sortedByAddress
        {
            get
            {
                return toModifyItems.Where(item => item.signal != null)
                                    .OrderBy(item => item.signal.index)
                                    .ToList();
            }
        }

        public bool isMultiModify => toModifyItems.Any() && toModifyItems.Count() > 1;

        public DOModifyWrapper? firstModify => sortedByAddress.FirstOrDefault();
        public DOModifyWrapper? lastModify => sortedByAddress.LastOrDefault();

        public int startIndex => firstModify == null ? -1 : firstModify.signal.index;
        public int endIndex => lastModify == null ? -1 : lastModify.signal.index;
    }

    public class DOModifyWrapper
    {
        public clsIOSignal signal;
        public bool state;

        public DOModifyWrapper(clsIOSignal signal, bool state)
        {
            this.signal = signal;
            this.state = state;
        }
    }
}
