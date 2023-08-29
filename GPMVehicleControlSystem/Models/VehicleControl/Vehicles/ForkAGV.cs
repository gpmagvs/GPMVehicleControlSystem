using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 叉車AGV
    /// </summary>
    public partial class ForkAGV : SubmarinAGV
    {
        public override string WagoIOConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/IO_Wago_Fork_AGV.ini");
        /// <summary>
        /// Fork車控
        /// </summary>
        private ForkAGVController ForkAGVC => AGVC as ForkAGVController;
        public bool IsForkInitialized => ForkLifter.IsInitialized;
        public override clsWorkStationModel WorkStations { get; set; } = new clsWorkStationModel();
        public override clsForkLifter ForkLifter { get; set; } = new clsForkLifter();
        public ForkAGV()
        {
            ForkLifter = new clsForkLifter(this);
            ForkLifter.Driver = VerticalDriverState;
            ForkLifter.DIModule = WagoDI;
            ForkLifter.DOModule = WagoDO;
        }


        public override async Task<bool> ResetMotor()
        {
            try
            {
                await base.ResetMotor();
                await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, false);
                return true;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex.Message, ex);
                return false;
            }
        }

        protected override async Task<(bool, string)> PreActionBeforeInitialize()
        {
            (bool, string) baseInitiazedResutl = await base.PreActionBeforeInitialize();
            if (!baseInitiazedResutl.Item1)
                return baseInitiazedResutl;

            await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, false);
            await WagoDO.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);

            bool RightLaserAbnormal = !WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3);
            if (RightLaserAbnormal)
                return (false, "無法在障礙物入侵的狀態下進行初始化(右方障礙物檢出)");
            bool LeftLaserAbnormal = !WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3);
            if (LeftLaserAbnormal)
                return (false, "無法在障礙物入侵的狀態下進行初始化(左方障礙物檢出)");
            bool forkFrontendSensorAbnormal = !WagoDI.GetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor);
            if (forkFrontendSensorAbnormal)
                return (false, "無法在障礙物入侵的狀態下進行初始化(Fork 前端障礙物檢出)");
            else
            {
                return (true, "");
            }

        }
        protected override async Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation)
        {

            (bool confirm, string message) baseInitize = await base.InitializeActions(cancellation);
            if (!baseInitize.confirm)
                return baseInitize;
            if (ForkLifter.Enable)
            {
                if (ForkLifter.IsLoading)
                {
                    AlarmManager.AddWarning(AlarmCodes.Fork_Has_Cargo_But_Initialize_Running);
                }
                await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, true);

                (bool done, AlarmCodes alarm_code) forkInitizeResult = await ForkLifter.ForkInitialize(HasAnyCargoOnAGV() | ForkLifter.IsLoading ? 0.2 : 0.4);
                await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);

                return (forkInitizeResult.done, forkInitizeResult.alarm_code.ToString());
            }
            else
            {
                AlarmManager.AddWarning(AlarmCodes.Fork_Disabled);
                return (true, "Forklift disabled");
            }
        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new ForkAGVController(RosBridge_IP, RosBridge_Port);
            ForkLifter.fork_ros_controller = ForkAGVC;
        }


        protected override void EMOPushedHandler(object? sender, EventArgs e)
        {
            base.EMOPushedHandler(sender, e);
            Task.Factory.StartNew(() =>
            {
                ForkLifter.ForkARMStop();
                ForkAGVC.ZAxisStop();
            });
        }
        protected override async void DOSignalDefaultSetting()
        {
            base.DOSignalDefaultSetting();
            await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);
            await WagoDO.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, false);

        }
        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();
            WagoDI.SubsSignalStateChange(DI_ITEM.Vertical_Motor_Alarm, HandleWheelDriverStatusError);
        }

        protected override clsWorkStationModel DeserializeWorkStationJson(string json)
        {
            clsWorkStationModel? dat = JsonConvert.DeserializeObject<clsWorkStationModel>(json);
            foreach (KeyValuePair<int, clsWorkStationData> station in dat.Stations)
            {
                while (station.Value.LayerDatas.Count != 3)
                {
                    station.Value.LayerDatas.Add(station.Value.LayerDatas.Count, new clsStationLayerData
                    {
                        Down_Pose = 0,
                        Up_Pose = 0
                    });
                }
            }
            return dat;
        }
        internal override bool HasAnyCargoOnAGV()
        {
            try
            {
                return WagoDI.GetState(DI_ITEM.Fork_TRAY_Left_Exist_Sensor) | WagoDI.GetState(DI_ITEM.Fork_TRAY_Right_Exist_Sensor) | !WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor) | !WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor);
            }
            catch (Exception)
            {
                return false;
            }
        }


        protected override int GetCargoType()
        {
            var rack_sensor1 = WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
            var rack_sensor2 = WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor);
            if (rack_sensor2 | rack_sensor1)
                return 1;
            else return -1;
        }

        protected override async Task DOSettingWhenEmoTrigger()
        {
            await base.DOSettingWhenEmoTrigger();
            await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, true);
        }
    }
}
