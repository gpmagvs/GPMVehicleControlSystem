using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsCSTReader : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.CST_READER;

        private string _realTimeCSTIDRecievedFromModuleInfo = "";
        public string realTimeCSTIDRecievedFromModuleInfo
        {
            get => _realTimeCSTIDRecievedFromModuleInfo;
            private set
            {
                if (_realTimeCSTIDRecievedFromModuleInfo != value)
                {
                    logger.Info($"CST Data From AGVC ModuleInfo.CST Changed => From {_realTimeCSTIDRecievedFromModuleInfo} To {value}");
                    _realTimeCSTIDRecievedFromModuleInfo = value;
                }
            }
        }

        public new CSTReaderState Data => StateData == null ? new CSTReaderState() : (CSTReaderState)StateData;

        private int _State = -1;
        public int State
        {
            get => _State;
            set
            {
                if (_State != value)
                {
                    logger.Info($"CST Reader State Change to {value}()");
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
                    bool isDataError = value.ToLower().Trim() == "error";
                    string _DataToStore = isDataError ? "ERROR" : value;
                    if (isDataError)
                    {
                        logger.Warn($"CST ID is {value}!");
                    }
                    logger.Info($"CST ID CHANGED TO {_DataToStore} (Old= {_ValidCSTID})");
                    SaveCSTIDToLocalStorage(_DataToStore);
                    _ValidCSTID = _DataToStore;
                }
            }
        }

        public override string alarm_locate_in_name => component_name.ToString();


        public override Message StateData
        {
            get => base.StateData;
            set
            {
                base.StateData = value;
                realTimeCSTIDRecievedFromModuleInfo = (value as CSTReaderState).data.Trim();
            }
        }

        public override bool CheckStateDataContent()
        {

            if (!base.CheckStateDataContent())
                return false;
            State = Data.state;
            return true;
        }

        internal void UpdateCSTIDDataHandler(object? sender, string cst_id)
        {
            CarController _agvc = (sender as CarController);
            logger.Trace($"Inovke CSTReaderAction Done event with CST ID = {cst_id}");
            if (cst_id.ToUpper() != "ERROR")
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    ValidCSTID = _realTimeCSTIDRecievedFromModuleInfo;
                    _agvc?.CSTReadServiceSemaphoreSlim.Release();
                });
            }
            else
            {
                ValidCSTID = cst_id;
                _agvc?.CSTReadServiceSemaphoreSlim.Release();
            }
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
                logger.Error(ex);
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
                logger.Error(ex);
            }

        }
    }


}
