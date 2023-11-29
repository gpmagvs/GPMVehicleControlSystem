using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Actions;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VMS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Actionlib;
using System.Threading;

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

        /// <summary>
        /// 速度控制請求的時機
        /// </summary>
        public enum SPEED_CONTROL_REQ_MOMENT
        {
            UNKNOWN,
            FRONT_LASER_1_TRIGGER,
            FRONT_LASER_2_TRIGGER,
            FRONT_LASER_3_TRIGGER,
            FRONT_LASER_1_RECOVERY,
            FRONT_LASER_2_RECOVERY,
            FRONT_LASER_3_RECOVERY,

            BACK_LASER_1_TRIGGER,
            BACK_LASER_2_TRIGGER,
            BACK_LASER_3_TRIGGER,
            BACK_LASER_1_RECOVERY,
            BACK_LASER_2_RECOVERY,
            BACK_LASER_3_RECOVERY,

            RIGHT_LASER_TRIGGER,
            RIGHT_LASER_RECOVERY,
            LEFT_LASER_TRIGGER,
            LEFT_LASER_RECOVERY,
            IO_MODULE_DISCONNECTED,
            IO_MODULE_RECOVERY,
            CYCLE_STOP,
            BACK_TO_SECONDARY_POINT,
            NEW_TASK_START_EXECUTING,
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
        public RosSocket rosSocket;

        /// <summary>
        /// 地圖比對率
        /// </summary>
        public double MapRatio => LocalizationControllerResult.map_match_status / 100.0;
        public LOCALIZE_STATE Localize_State => (LOCALIZE_STATE)LocalizationControllerResult.loc_status;
        private LocalizationControllerResultMessage0502 LocalizationControllerResult = new LocalizationControllerResultMessage0502();

        public event EventHandler<ModuleInformation> OnModuleInformationUpdated;
        public event EventHandler<LocalizationControllerResultMessage0502> OnSickLocalicationDataUpdated;
        public event EventHandler<RawMicroScanDataMsg> OnSickRawDataUpdated;
        public event EventHandler<int> OnSickLaserModeSettingChanged;
        public event EventHandler OnAGVCCycleStopRequesting;
        public event EventHandler OnRosSocketReconnected;
        public event EventHandler OnSTOPCmdRequesting;
        public delegate bool SpeedRecoveryRequestingDelegate();
        public SpeedRecoveryRequestingDelegate OnSpeedRecoveryRequesting;

        public Action<ActionStatus> OnAGVCActionChanged;

        internal TaskCommandActionClient actionClient;

        internal ActionStatus _ActionStatus = ActionStatus.PENDING;
        public ActionStatus ActionStatus
        {
            get => _ActionStatus;
            private set
            {
                if (_ActionStatus != value)
                {
                    LOG.TRACE($"Action Status Changed To : {value}");
                    Task.Factory.StartNew(() =>
                    {
                        if (OnAGVCActionChanged != null)
                            OnAGVCActionChanged(value);
                    });
                    _ActionStatus = value;
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
        /// 訂閱/Module_Information接收處理數據的週期
        /// </summary>
        /// <remarks>
        /// 單位:毫秒(ms)
        /// </remarks>
        public int Throttle_rate_of_Topic_ModuleInfo { get; internal set; } = 100;
        public int QueueSize_of_Topic_ModuleInfo { get; internal set; } = 10;


        private bool TryConnecting = false;

        public CarController()
        {
        }

        public CarController(string IP, int Port) : base(IP, Port)
        {
        }
        private bool _Connected = true;
        public bool Connected
        {
            get => _Connected;
            set
            {
                if (_Connected != value)
                {
                    _Connected = value;
                    if (!value)
                    {
                        AlarmManager.AddWarning(AlarmCodes.ROS_Bridge_server_Disconnect);
                    }
                    else
                    {
                        AlarmManager.ClearAlarm(AlarmCodes.ROS_Bridge_server_Disconnect);
                    }
                }
            }
        }
        public override async Task<bool> Connect()
        {
            while (!IsConnected())
            {

                Connected = false;
                await Task.Delay(1000);
                LOG.WARN($"Connect to ROSBridge Server (ws://{IP}:{VMSPort}) Processing...");
                try
                {
                    rosSocket = new RosSocket(new RosSharp.RosBridgeClient.Protocols.WebSocketSharpProtocol($"ws://{IP}:{VMSPort}"));
                    if (!rosSocket.protocol.IsAlive())
                    {
                        rosSocket.protocol.Close();
                    }
                }
                catch (Exception ex)
                {
                    rosSocket = null;
                    Console.WriteLine("ROS Bridge Server Connect Fail...Will Retry After 5 Secnonds...Error Message : " + ex.Message);
                    Thread.Sleep(5000);
                }
            }
            Connected = true;
            rosSocket.protocol.OnClosed += Protocol_OnClosed;
            LOG.INFO($"ROS Connected ! ws://{IP}:{VMSPort}");
            SubscribeROSTopics();
            AdertiseROSServices();
            InitTaskCommandActionClient();
            ManualController = new MoveControl(rosSocket);
            if (TryConnecting)
            {
                TryConnecting = false;
                OnRosSocketReconnected?.Invoke(rosSocket, null);
            }
            return true;
        }


        /// <summary>
        /// 訂閱ROS主題
        /// </summary>
        public virtual void SubscribeROSTopics()
        {
            Task.Run(() =>
            {
                rosSocket.Subscribe<ModuleInformation>("/module_information", ModuleInformationCallback, throttle_rate: Throttle_rate_of_Topic_ModuleInfo, queue_length: QueueSize_of_Topic_ModuleInfo);
                rosSocket.Subscribe<LocalizationControllerResultMessage0502>("localizationcontroller/out/localizationcontroller_result_message_0502", SickLocalizationStateCallback, throttle_rate: 100, queue_length: 5);
                rosSocket.Subscribe<RawMicroScanDataMsg>("/sick_safetyscanners/raw_data", SickSaftyScannerRawDataCallback, throttle_rate: 100, queue_length: 1);
                rosSocket.Subscribe<OutputPathsMsg>("/sick_safetyscanners/output_paths", SickSaftyScannerOutputDataCallback, throttle_rate: 10, queue_length: 5);
            });
        }
        private void ModuleInformationCallback(ModuleInformation _ModuleInformation)
        {
            module_info = _ModuleInformation;
        }
        private int LaserModeSetting = -1;
        private void SickSaftyScannerOutputDataCallback(OutputPathsMsg sick_scanner_out_data)
        {
            if (LaserModeSetting != sick_scanner_out_data.active_monitoring_case)
            {
                OnSickLaserModeSettingChanged?.Invoke(this, sick_scanner_out_data.active_monitoring_case);
                LaserModeSetting = sick_scanner_out_data.active_monitoring_case;
            }
        }

        private void SickSaftyScannerRawDataCallback(RawMicroScanDataMsg sick_scanner_raw_data)
        {
            Task.Factory.StartNew(() =>
            {
                // LogSickRawData(sick_scanner_raw_data);
                OnSickRawDataUpdated?.Invoke(this, sick_scanner_raw_data);
            });
        }


        /// <summary>
        /// 建立車載端的ROS Service server 
        /// </summary>
        public abstract void AdertiseROSServices();

        private void Protocol_OnClosed(object? sender, EventArgs e)
        {
            rosSocket.protocol.OnClosed -= Protocol_OnClosed;
            LOG.WARN("Rosbridger Server On Closed...Retry connecting...");
            TryConnecting = true;
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
            if (actionClient != null)
            {
                try
                {
                    actionClient.CancelGoal();
                    actionClient.Terminate();
                }
                catch (Exception ex)
                {
                    LOG.ERROR(ex.Message, ex);
                }
                actionClient.Dispose();
            }
            actionClient = new TaskCommandActionClient("/barcodemovebase", rosSocket);
            actionClient.OnActionStatusChanged += (sender, status) =>
            {
                ActionStatus = status;
            };
            actionClient.Initialize();
        }

        internal async Task<bool> ResetTask(RESET_MODE mode)
        {
            if (mode == RESET_MODE.ABORT)
                return await AbortTask();
            else
                return await CycleStop();

        }

        private async Task<bool> CycleStop()
        {
            if (ActionStatus == ActionStatus.ACTIVE && actionClient.goal.mobilityModes != 0)
            {
                LOG.WARN($"AGV Is Navigating at working station, Cycle Stop is not nessary.");
                return true;
            }
            return await CarSpeedControl(ROBOT_CONTROL_CMD.STOP_WHEN_REACH_GOAL, actionClient.goal.taskID, SPEED_CONTROL_REQ_MOMENT.CYCLE_STOP);
        }

        /// <summary>
        /// 發送空的任務messag已達緊停的效果
        /// </summary>
        internal async Task<bool> AbortTask()
        {
            if (_ActionStatus != ActionStatus.ABORTED)
                SendGoal(new TaskCommandGoal());
            return true;
        }


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

        private void SickLocalizationStateCallback(LocalizationControllerResultMessage0502 _LocalizationControllerResult)
        {
            LocalizationControllerResult = _LocalizationControllerResult;
            OnSickLocalicationDataUpdated?.Invoke(this, _LocalizationControllerResult);
        }


        internal async Task CarSpeedControl(ROBOT_CONTROL_CMD cmd, SPEED_CONTROL_REQ_MOMENT moment, bool CheckLaserStatus = true)
        {
            await CarSpeedControl(cmd, RunningTaskData.Task_Name, moment, CheckLaserStatus);
        }
        public async Task<bool> CarSpeedControl(ROBOT_CONTROL_CMD cmd, string task_id, SPEED_CONTROL_REQ_MOMENT moment, bool CheckLaserStatus = true)
        {

            if (cmd == ROBOT_CONTROL_CMD.SPEED_Reconvery & OnSpeedRecoveryRequesting != null)
            {

                var speed_recoverable = OnSpeedRecoveryRequesting();
                if (!speed_recoverable && CheckLaserStatus)
                {
                    LOG.INFO($"[ROBOT_CONTROL_CMD] 要求車控速度恢復但尚有雷射觸發中");
                    return false;
                }
            }

            ComplexRobotControlCmdRequest req = new ComplexRobotControlCmdRequest()
            {
                taskID = task_id == null ? "" : task_id,
                reqsrv = (byte)cmd
            };
            ComplexRobotControlCmdResponse? res = await rosSocket?.CallServiceAndWait<ComplexRobotControlCmdRequest, ComplexRobotControlCmdResponse>("/complex_robot_control_cmd", req);
            if (res == null)
            {
                return false;
            }
            LOG.INFO($"[ROBOT_CONTROL_CMD] 車控回復 {cmd}({moment}) 請求: {(res.confirm ? "OK" : "NG")} (Task ID={task_id})");
            if (cmd == ROBOT_CONTROL_CMD.STOP)
            {
                OnSTOPCmdRequesting?.Invoke(this, EventArgs.Empty);
            }
            return res.confirm;
        }

        internal async Task<(bool confirm, string message)> ExecuteTaskDownloaded(clsTaskDownloadData taskDownloadData, double action_timeout = 5)
        {
            RunningTaskData = taskDownloadData;
            return await SendGoal(RunningTaskData.RosTaskCommandGoal, action_timeout);
        }

        CancellationTokenSource wait_agvc_execute_action_cts;
        internal async Task<(bool confirm, string message)> SendGoal(TaskCommandGoal rosGoal, double timeout = 5)
        {
            return await Task.Run(async () =>
            {
                bool isCancelTask = rosGoal.planPath.poses.Length == 0;
                string new_path = isCancelTask ? "" : string.Join("->", rosGoal.planPath.poses.Select(p => p.header.seq));
                if (isCancelTask)
                    LOG.WARN("Empty Action Goal To AGVC To Emergency Stop AGV", show_console: true, color: ConsoleColor.Red);
                else
                    LOG.TRACE("Action Goal To AGVC:\r\n" + rosGoal.ToJson(), show_console: false, color: ConsoleColor.Green);
                actionClient.goal = rosGoal;
                actionClient.SendGoal();
                if (isCancelTask)//取消任務
                {
                    return (true, "");
                }
                wait_agvc_execute_action_cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                await Task.Delay(500);
                while (ActionStatus != ActionStatus.ACTIVE && ActionStatus != ActionStatus.SUCCEEDED && ActionStatus != ActionStatus.PENDING)
                {
                    LOG.TRACE($"[SendGoal] Action Status Monitor .Status = {ActionStatus}");
                    await Task.Delay(1);
                    if (wait_agvc_execute_action_cts.IsCancellationRequested)
                    {
                        string error_msg = $"發送任務請求給車控但車控並未接收成功-AGVC Status={ActionStatus}";
                        LOG.Critical(error_msg);
                        AbortTask();
                        return (false, error_msg);
                    }
                }
                LOG.INFO($"AGVC Accept Task and Start Executing：Current_Status= {ActionStatus},Path Tracking = {new_path}", true);
                return (true, "");
            });
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
