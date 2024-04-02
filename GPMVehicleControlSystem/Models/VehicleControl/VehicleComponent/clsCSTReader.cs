using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsCSTReader : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.CST_READER;
        public new CSTReaderState Data => StateData == null ? new CSTReaderState() : (CSTReaderState)StateData;

        private int _State = -1;
        public int State
        {
            get => _State;
            set
            {
                if (_State != value)
                {
                    LOG.TRACE($"CST Reader State Change to {value}()");
                    _State = value;
                }
            }
        }

        public string _ValidCSTID = "";
        public string ValidCSTID
        {
            get => _ValidCSTID;
            set
            {
                if (_ValidCSTID != value)
                {
                    if (value.ToLower().Trim() == "error")
                    {
                        LOG.WARN($"CST ID is {value}_Valid ID Not Updated");
                        return;
                    }
                    LOG.TRACE($"CST ID CHANGED TO {value} (Old= {_ValidCSTID})");
                    SaveCSTIDToLocalStorage(value);
                    _ValidCSTID = value;
                }
            }
        }

        public override string alarm_locate_in_name => component_name.ToString();

        public override  bool CheckStateDataContent()
        {

            if (! base.CheckStateDataContent())
                return false;
            State = Data.state;
            return true;
        }

        internal void UpdateCSTIDDataHandler(object? sender, string cst_id)
        {
            ValidCSTID = cst_id;
        }
        private string CstIDStoreFileFullName => Path.Combine(Environment.CurrentDirectory, "cst_read_id.txt");
        private void SaveCSTIDToLocalStorage(string cst_id)
        {
            try
            {
                File.WriteAllText(CstIDStoreFileFullName, cst_id);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
        }
        internal void ReadCSTIDFromLocalStorage()
        {
            try
            {
                if (!File.Exists(CstIDStoreFileFullName))
                    return;
                ValidCSTID = File.ReadAllText(CstIDStoreFileFullName);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }

        }
    }


}
