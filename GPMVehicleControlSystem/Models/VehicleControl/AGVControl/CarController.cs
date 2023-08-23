using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Actions;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Actionlib;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{


    /// <summary>
    /// 使用ＲＯＳ與車控端通訊
    /// /sick_safetyscanners/output_paths =>訂閱sick 雷射輸出結果(有包含雷射設定組數)
    /// </summary>
    public abstract partial class CarController : Connection
    {
        public enum LOCALIZE_STATE : byte
        {
            //  1 byte LocalizationStatus [0...100, 10: OK, 20: Warning, 30: Not localized, 40: System error]
            OK = 10,
            Warning = 20,
            Not_Localized = 30,
            System_Error = 40
        }
        public enum ROBOT_CONTROL_CMD : byte
        {
            /// <summary>
            /// 速度恢復
            /// </summary>
            SPEED_Reconvery = 0,
            /// <summary>
            /// 減速
            /// </summary>
            DECELERATE = 1,
            /// <summary>
            /// 停止
            /// </summary>
            STOP = 2,
            /// <summary>
            /// 停止(停止計算道路封閉)
            /// </summary>
            STOP_CALCULATE_PATH_CLOSE = 3,
            /// <summary>
            /// 二次定位減速
            /// </summary>
            DECELERATE_SECONDARY_LOCALIZATION = 21,
            /// <summary>
            /// 請求停止或到目標點停止，清空當前車控軌跡任務，需附帶任務ID
            /// </summary>
            STOP_WHEN_REACH_GOAL = 100,
            /// <summary>
            /// 立即停止請求，需附帶任務 ID
            /// </summary>
            STOP_RIGHTNOW = 101,
            NONE = 090
        }
        public RosSocket? rosSocket;

        /// <summary>
        /// 地圖比對率
        /// </summary>
        public double MapRatio => LocalizationControllerResult.map_match_status / 100.0;
        public LOCALIZE_STATE Localize_State => (LOCALIZE_STATE)LocalizationControllerResult.loc_status;
        private LocalizationControllerResultMessage0502 LocalizationControllerResult = new LocalizationControllerResultMessage0502();

        public event EventHandler<ModuleInformation> OnModuleInformationUpdated;
        public event EventHandler<LocalizationControllerResultMessage0502> OnSickLocalicationDataUpdated;
        public event EventHandler<RawMicroScanDataMsg> OnSickRawDataUpdated;
        public event EventHandler<OutputPathsMsg> OnSickOutputPathsDataUpdated;
        public Action<ActionStatus> OnAGVCActionChanged;

        internal TaskCommandActionClient actionClient;

        private ActionStatus _ActionStatus = ActionStatus.PENDING;
        public ActionStatus ActionStatus
        {
            get => _ActionStatus;
            private set
            {
                if (_ActionStatus != value)
                {
                    _ActionStatus = value;
                    if (OnAGVCActionChanged != null)
                    {
                        OnAGVCActionChanged(_ActionStatus);
                    }
                }
            }
        }

        private ModuleInformation _module_info;
        private int CurrentTag => _module_info.nav_state.lastVisitedNode.data;
        public ModuleInformation module_info
        {
            get => _module_info;
            private set
            {
                if (value != null)
                    OnModuleInformationUpdated?.Invoke(this, value);
                _module_info = value;

            }
        }
        public int lastVisitedNode => module_info.nav_state.lastVisitedNode.data;

        public List<AlarmCodes> alarm_codes { get; private set; }

        public clsTaskDownloadData RunningTaskData { get; private set; } = new clsTaskDownloadData();
        /// <summary>
        /// 手動操作控制器
        /// </summary>
        public MoveControl ManualController { get; set; }
        public double CurrentSpeedLimit { get; internal set; }

        /// <summary>
        /// 車控是否在執行任務
        /// </summary>
        /// <value></value>
        public bool IsAGVExecutingTask { get; set; } = false;
        public CarController()
        {
        }

        public CarController(string IP, int Port) : base(IP, Port)
        {
        }

        public override bool Connect()
        {
            while (!IsConnected())
            {
                Thread.Sleep(1000);
                LOG.WARN($"Connect to ROSBridge Server (ws://{IP}:{Port}) Processing...");
                try
                {
                    rosSocket = new RosSocket(new RosSharp.RosBridgeClient.Protocols.WebSocketSharpProtocol($"ws://{IP}:{Port}"));
                    if (!rosSocket.protocol.IsAlive())
                    {
                        AlarmManager.AddWarning(AlarmCodes.ROS_Bridge_server_Disconnect);
                        rosSocket.protocol.Close();
                    }
                }
                catch (Exception ex)
                {
                    rosSocket = null;
                    AlarmManager.AddWarning(AlarmCodes.ROS_Bridge_server_Disconnect);
                    Console.WriteLine("ROS Bridge Server Connect Fail...Will Retry After 5 Secnonds...Error Message : " + ex.Message);
                    Thread.Sleep(5000);
                }
            }
            rosSocket.protocol.OnClosed += Protocol_OnClosed;
            LOG.INFO($"ROS Connected ! ws://{IP}:{Port}");
            SubscribeROSTopics();
            AdertiseROSServices();
            InitTaskCommandActionClient();
            ManualController = new MoveControl(rosSocket);
            return true;
        }


        /// <summary>
        /// 訂閱ROS主題
        /// </summary>
        public virtual void SubscribeROSTopics()
        {
            rosSocket.Subscribe<ModuleInformation>("/module_information", ModuleInformationCallback, queue_length: 50);
            rosSocket.Subscribe<LocalizationControllerResultMessage0502>("localizationcontroller/out/localizationcontroller_result_message_0502", SickStateCallback, queue_length: 50);
            rosSocket.Subscribe<RawMicroScanDataMsg>("/sick_safetyscanners/raw_data", SickSaftyScannerRawDataCallback, throttle_rate: 1, queue_length: 1);
            rosSocket.Subscribe<OutputPathsMsg>("/sick_safetyscanners/output_paths", SickSaftyScannerOutputDataCallback, throttle_rate: 1, queue_length: 1);
        }
        private void ModuleInformationCallback(ModuleInformation _ModuleInformation)
        {
            module_info = _ModuleInformation;
        }
        public OutputPathsMsg SickOutPutPaths { get; private set; } = new OutputPathsMsg();
        private void SickSaftyScannerOutputDataCallback(OutputPathsMsg sick_scanner_out_data)
        {
            OnSickOutputPathsDataUpdated?.Invoke(this, sick_scanner_out_data);
            SickOutPutPaths = sick_scanner_out_data;
        }

        private void SickSaftyScannerRawDataCallback(RawMicroScanDataMsg sick_scanner_raw_data)
        {
            Task.Factory.StartNew(() =>
            {
                // LogSickRawData(sick_scanner_raw_data);
                OnSickRawDataUpdated?.Invoke(this, sick_scanner_raw_data);
            });
        }

        private void LogSickRawData(RawMicroScanDataMsg sick_scanner_raw_data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(sick_scanner_raw_data, Formatting.Indented);
                string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), Path.Combine(AppSettingsHelper.LogFolder, "SickData"));
                Directory.CreateDirectory(LogFolder);
                var fileName = Path.Combine(LogFolder, "tmp_sick_data.json");
                using (StreamWriter writer = new StreamWriter(fileName))
                {
                    writer.Write(json);
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR($"紀錄sick data 的過程中發生錯誤 {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 建立車載端的ROS Service server 
        /// </summary>
        public abstract void AdertiseROSServices();

        private void Protocol_OnClosed(object? sender, EventArgs e)
        {
            rosSocket.protocol.OnClosed -= Protocol_OnClosed;
            LOG.WARN("Rosbridger Server On Closed...Retry connecting...");
            Connect();
        }

        public override void Disconnect()
        {
            rosSocket.Close();
            rosSocket = null;
        }


        internal void EMOHandler(object? sender, EventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                AbortTask();
                if (wait_agvc_execute_action_cts != null)
                    wait_agvc_execute_action_cts.Cancel();
            });
        }
        public override bool IsConnected()
        {
            return rosSocket != null && rosSocket.protocol.IsAlive();
        }


        private void InitTaskCommandActionClient()
        {
            ActionStatus = ActionStatus.NO_GOAL;
            if (actionClient != null)
            {
                actionClient.Terminate();
                actionClient.Dispose();
            }
            actionClient = new TaskCommandActionClient("/barcodemovebase", rosSocket);
            actionClient.OnActionStatusChanged += (status) =>
            {
                ActionStatus = status;
            };
            actionClient.Initialize();
        }

        internal void AbortTask(RESET_MODE mode)
        {
            if (mode == RESET_MODE.ABORT)
                AbortTask();
            else
                CycleStop();

        }

        private void CycleStop()
        {
            CarSpeedControl(ROBOT_CONTROL_CMD.STOP_WHEN_REACH_GOAL);
            IsAGVExecutingTask = false;
            //AbortTask();
        }

        internal void AbortTask()
        {
            if (actionClient != null)
            {
                actionClient.goal = new TaskCommandGoal();
                actionClient.SendGoal();
            }
            IsAGVExecutingTask = false;
        }

        internal bool NavPathExpandedFlag { get; private set; } = false;


        private void DisposeTaskCommandActionClient()
        {
            if (actionClient != null)
            {
                try
                {
                    actionClient.Terminate();
                    actionClient.Dispose();
                    actionClient = null;

                }
                catch (Exception ex)
                {
                    LOG.WARN("DisposeTaskCommandActionClient Exception Occur :　" + ex.Message);
                }
            }
        }

        private void SickStateCallback(LocalizationControllerResultMessage0502 _LocalizationControllerResult)
        {
            LocalizationControllerResult = _LocalizationControllerResult;
            OnSickLocalicationDataUpdated?.Invoke(this, _LocalizationControllerResult);
        }


        internal async Task CarSpeedControl(ROBOT_CONTROL_CMD cmd)
        {
            await Task.Delay(1);
            CarSpeedControl(cmd, RunningTaskData.Task_Name);
        }
        public bool CarSpeedControl(ROBOT_CONTROL_CMD cmd, string task_id)
        {
            ComplexRobotControlCmdRequest req = new ComplexRobotControlCmdRequest()
            {
                taskID = task_id == null ? "" : task_id,
                reqsrv = (byte)cmd
            };
            ComplexRobotControlCmdResponse? res = rosSocket?.CallServiceAndWait<ComplexRobotControlCmdRequest, ComplexRobotControlCmdResponse>("/complex_robot_control_cmd", req);
            if (res == null)
            {
                return false;
            }
            //LOG.TRACE($"要求車控 {cmd},Result: {(res.confirm ? "OK" : "NG")}");
            return res.confirm;
        }

        internal async Task<(bool confirm, string message)> AGVSTaskDownloadHandler(clsTaskDownloadData taskDownloadData)
        {
            NavPathExpandedFlag = false;
            RunningTaskData = taskDownloadData;
            return await SendGoal(RunningTaskData.RosTaskCommandGoal);
        }

        CancellationTokenSource wait_agvc_execute_action_cts;
        internal async Task<(bool confirm, string message)> SendGoal(TaskCommandGoal rosGoal)
        {
            string new_path = string.Join("->", rosGoal.planPath.poses.Select(p => p.header.seq));

            LOG.WARN($"====================Send Goal To AGVC===================" +
                $"\r\nTaskID        = {rosGoal.taskID}" +
                $"\r\nFinal Goal ID = {rosGoal.finalGoalID}:Theta:{rosGoal.planPath.poses.Last().pose.position}" +
                $"\r\nPlanPath      = {string.Join("->", rosGoal.planPath.poses.Select(pose => pose.header.seq).ToArray())}" +
                $"\r\nmobilityModes = {rosGoal.mobilityModes}" +
                $"\r\n==========================================================");

            actionClient.goal = rosGoal;
            actionClient.SendGoal();
            wait_agvc_execute_action_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (ActionStatus != ActionStatus.ACTIVE)
            {
                if (wait_agvc_execute_action_cts.IsCancellationRequested)
                {
                    LOG.Critical($"發送任務請求給車控但車控並未接收成功");
                    AlarmManager.AddAlarm(AlarmCodes.Can_not_Pass_Task_to_Motion_Control, false);
                    return (false, $"發送任務請求給車控但車控並未接收成功");
                }
                await Task.Delay(1);
            }
            LOG.INFO($"AGVC Accept Task and Start Executing：Path Tracking = {new_path}", false);
            return (true, "");

        }

        internal void Replan(clsTaskDownloadData taskDownloadData)
        {
            actionClient.goal = taskDownloadData.RosTaskCommandGoal;
            actionClient.SendGoal();
        }

        public abstract Task<(bool request_success, bool action_done)> TriggerCSTReader();
        public abstract Task<(bool request_success, bool action_done)> AbortCSTReader();

    }
}
