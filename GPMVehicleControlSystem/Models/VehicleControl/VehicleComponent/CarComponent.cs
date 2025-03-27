using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using NLog;
using RosSharp.RosBridgeClient;
using System.Dynamic;
using System.Runtime.InteropServices;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    /// <summary>
    /// 車控發佈的 module_information 各元件狀態
    /// </summary>
    public abstract class CarComponent : AGVAlarmReportable, RosNodeParamImportAbstract
    {

        public virtual string RosParmYamlPath => "rosparam.yaml";

        public enum COMPOENT_NAME
        {
            BATTERY,
            DRIVER,
            IMU,
            BARCODE_READER,
            GUID_SENSOR,
            CST_READER,
            NAVIGATION,
            VERTIVAL_DRIVER,
            SICK,
            PIN
        }
        public enum STATE
        {
            NORMAL,
            WARNING,
            ABNORMAL
        }

        private void AGVAlarmReportable_OnAlarmResetAsNoneRequest(object? sender, EventArgs e)
        {
            OnAlarmResetHandle();
        }
        public virtual void OnAlarmResetHandle()
        {

        }
        protected Logger logger;
        public CarComponent() : base()
        {
            logger = LogManager.GetLogger($"CarComponents/{GetType().Name}");
            RosNodeSettingParam = ImportRosNodeParam();
            UpdateStateMonitor();
        }

        protected Message _StateData;
        public DateTime lastUpdateTime { get; set; } = DateTime.MaxValue;
        public abstract COMPOENT_NAME component_name { get; }

        public delegate Task<bool> AlarmHappendDelegate(AlarmCodes alarm);
        public AlarmHappendDelegate OnAlarmHappened { get; set; }

        public static event EventHandler<CarComponent> OnCommunicationError;
        public static event EventHandler<CarComponent> OnCommunicationRecovery;

        private bool _IsCommunicationError = false;
        public virtual bool IsCommunicationError
        {
            get => _IsCommunicationError;
            set
            {
                if (_IsCommunicationError != value)
                {
                    _IsCommunicationError = value;
                    if (_IsCommunicationError)
                    {
                        HandleCommunicationError();
                        logger.Warn($"[{component_name}] 數據狀態更新逾時");

                    }
                    else
                    {
                        logger.Info($"[{component_name}] 數據狀態更新已恢復");
                        HandleCommunicationRecovery();
                    }
                }
            }
        }

        public object Data { get; }

        /// <summary>
        /// 異常碼
        /// </summary>
        public Dictionary<AlarmCodes, DateTime> ErrorCodes = new Dictionary<AlarmCodes, DateTime>();
        public virtual Message StateData
        {
            get => _StateData;
            set
            {
                _StateData = value;
                if (CheckStateDataContent())
                    lastUpdateTime = DateTime.Now;
            }
        }


        public virtual bool CheckStateDataContent()
        {
            if (_StateData == null)
            {
                return false;
            }
            else
                return true;
        }


        private async Task UpdateStateMonitor()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);
                    _CommunicationErrorJudge();
                }
            });
        }

        protected virtual void _CommunicationErrorJudge()
        {
            double timeDiff = (DateTime.Now - lastUpdateTime).TotalSeconds;
            //logger.Info($"[{component_name}]{timeDiff} sec");
            IsCommunicationError = timeDiff > 10;
        }

        protected virtual void HandleCommunicationError()
        {
            //
        }
        protected virtual void HandleCommunicationRecovery()
        {

            OnCommunicationRecovery?.Invoke(this, null);
        }
        public dynamic RosNodeSettingParam { get; set; } = new ExpandoObject();
        public virtual dynamic ImportRosNodeParam()
        {
            if (!File.Exists(this.RosParmYamlPath))
                return new ExpandoObject();
            string fileContent = File.ReadAllText(this.RosParmYamlPath);
            IDeserializer deserializer = new DeserializerBuilder()
                                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                    .Build();
            dynamic yamlObject = deserializer.Deserialize<dynamic>(fileContent);
            return yamlObject;
        }
    }
}
