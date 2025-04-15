using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Actions;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Tools;
using MathNet.Numerics.Distributions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NLog;
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
            LASER_RECOVERY,
            IO_MODULE_DISCONNECTED,
            IO_MODULE_RECOVERY,
            CYCLE_STOP,
            BACK_TO_SECONDARY_POINT,
            NEW_TASK_START_EXECUTING,
            UltrasoundSensor,
            UltrasoundSensorRecovery,
            AGVS_REQUEST,
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
            /// 避災訊號清除(包括火災/地震)
            /// </summary>
            Disaster_Avoidance_SIGNAL_CLEAR = 97,
            /// <summary>
            /// 地震避災訊號
            /// </summary>
            Earthquake_Disaster_Avoidance_SIGNAL = 98,
            /// <summary>
            /// 火災避災訊號
            /// </summary>
            Fire_Disaster_Avoidance_SIGNAL = 99,
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

        public string IOListsTopicID = "";
        /// <summary>
        /// 地圖比對率
        /// </summary>
        public double MapRatio => LocalizationControllerResult.map_match_status / 100.0;
        public LOCALIZE_STATE Localize_State => (LOCALIZE_STATE)LocalizationControllerResult.loc_status;
        private LocalizationControllerResultMessage0502 LocalizationControllerResult = new LocalizationControllerResultMessage0502();
        public event EventHandler<ModuleInformation> OnModuleInformationUpdated;
        public event EventHandler<LocalizationControllerResultMessage0502> OnSickLocalicationDataUpdated;
        public event EventHandler<RawMicroScanDataMsg> OnSickRawDataUpdated;
        public event EventHandler OnAGVCCycleStopRequesting;
        public event EventHandler OnRosSocketReconnected;
        public event EventHandler OnRosSocketDisconnected;
        public event EventHandler OnSTOPCmdRequesting;
        public delegate CST_TYPE delgateOnCstTypeUnknown();
        public delgateOnCstTypeUnknown OnCstTriggerButTypeUnknown;
        public delegate (bool confirmed, string message) SpeedRecoveryRequestingDelegate();
        public SpeedRecoveryRequestingDelegate OnSpeedRecoveryRequesting;
        public delegate SendActionCheckResult BeforeSendActionToAGVCDelegate();
        public BeforeSendActionToAGVCDelegate OnActionSendToAGVCRaising;
        private Action<ActionStatus> _OnAGVCActionChanged;
        public event EventHandler OnAGVCActionActive;
        public event EventHandler OnAGVCActionSuccess;
        public event EventHandler<ROBOT_CONTROL_CMD> OnSpeedControlChanged;
        private ManualResetEvent pauseModuleInfoCallbackHandle = new ManualResetEvent(true);
        protected Logger logger;
        Debouncer speedControlDebuncer = new Debouncer();
        public Action<ActionStatus> OnAGVCActionChanged
        {
            get => _OnAGVCActionChanged;
            set
            {
                _OnAGVCActionChanged = value;
                logger.Info($"{(value == null ? "取消註冊" : "新增註冊")} Action Server Status 變化監視");
            }
        }
        internal bool CycleStopActionExecuting = false;
        internal TaskCommandActionClient actionClient;

        internal ActionStatus _ActionStatus = ActionStatus.PENDING;
        public ActionStatus ActionStatus
        {
            get => _ActionStatus;
            private set
            {
                if (_ActionStatus != value)
                {
                    _ActionStatus = value;
                    if (value == ActionStatus.SUCCEEDED)
                    {
                        OnAGVCActionSuccess?.Invoke(this, EventArgs.Empty);

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
                {
                    pauseModuleInfoCallbackHandle.WaitOne();
                    OnModuleInformationUpdated?.Invoke(this, value);
                }
                _module_info = value;

            }
        }

        public int lastVisitedNode => module_info.nav_state.lastVisitedNode.data;

        public List<AlarmCodes> alarm_codes { get; private set; }

        public clsTaskDownloadData RunningTaskData { get; protected set; } = new clsTaskDownloadData();
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
        internal bool _IsEmergencyStopFlag = false;

        internal SemaphoreSlim CSTReadServiceSemaphoreSlim { get; private set; } = new SemaphoreSlim(1, 1);

        public class SendActionCheckResult
        {
            public enum SEND_ACTION_GOAL_CONFIRM_RESULT
            {
                Accept,
                AGVS_CANCEL_TASK_REQ_RAISED,
                AGVC_CANNOT_EXECUTE_ACTION,
                LD_ULD_SIMULATION,
                Confirming = 499,
                Others = 999,
            }
            public bool Accept => IsAcceptCase();

            [JsonConverter(typeof(StringEnumConverter))]
            public SEND_ACTION_GOAL_CONFIRM_RESULT ResultCode { get; } = SEND_ACTION_GOAL_CONFIRM_RESULT.Accept;
            public SendActionCheckResult(SEND_ACTION_GOAL_CONFIRM_RESULT ResultCode)
            {
                this.ResultCode = ResultCode;
            }
            private bool IsAcceptCase()
            {
                return ResultCode == SEND_ACTION_GOAL_CONFIRM_RESULT.Accept || ResultCode == SEND_ACTION_GOAL_CONFIRM_RESULT.LD_ULD_SIMULATION;
            }
        }
        public CarController()
        {
        }

        public CarController(string IP, int Port) : base(IP, Port)
        {
            logger = LogManager.GetLogger("CarControl");
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
                if (rosSocket != null)
                {
                    rosSocket.Close();
                    rosSocket.protocol.Close();
                }
                logger.Warn($"Connect to ROSBridge Server (ws://{IP}:{VMSPort}) Processing...");
                try
                {
                    rosSocket = new RosSocket(new RosSharp.RosBridgeClient.Protocols.WebSocketSharpProtocol($"ws://{IP}:{VMSPort}"));
                    await Task.Delay(1000);
                    Connected = rosSocket.protocol.IsAlive();
                    if (!Connected)
                    {
                        rosSocket.protocol.Close();
                    }
                }
                catch (Exception ex)
                {
                    rosSocket = null;
                    Console.WriteLine("ROS Bridge Server Connect Fail...Will Retry After 5 Secnonds...Error Message : " + ex.Message);
                    await Task.Delay(5000);
                }
            }
            Connected = true;
            rosSocket.protocol.OnClosed += Protocol_OnClosed;
            logger.Info($"ROS Connected ! ws://{IP}:{VMSPort}");
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

        public void PauseModuleInfoCallBackInvoke()
        {
            pauseModuleInfoCallbackHandle.Reset();
        }

        public void ResumeModuleInfoCallBackkInvoe()
        {
            pauseModuleInfoCallbackHandle.Set();
        }

        /// <summary>
        /// 訂閱ROS主題
        /// </summary>
        public virtual void SubscribeROSTopics()
        {
            Task.Run(() =>
            {
                rosSocket.Subscribe<ModuleInformation>("/module_information", (module_information) =>
                {
                    module_info = module_information;
                }, throttle_rate: Throttle_rate_of_Topic_ModuleInfo, queue_length: QueueSize_of_Topic_ModuleInfo);
                rosSocket.Subscribe<LocalizationControllerResultMessage0502>("localizationcontroller/out/localizationcontroller_result_message_0502", SickLocalizationStateCallback, throttle_rate: 100, queue_length: 5);
                //rosSocket.Subscribe<RawMicroScanDataMsg>("/sick_safetyscanners/raw_data", SickSaftyScannerRawDataCallback, throttle_rate: 100, queue_length: 1);
            });
        }


        /// <summary>
        /// 建立車載端的ROS Service server 
        /// </summary>
        public virtual void AdertiseROSServices()
        {
            IOListsTopicID = rosSocket.Advertise<IOlistsMsg>("IOlists");
        }

        public virtual void IOListMsgPublisher(IOlistsMsg payload)
        {
            rosSocket.Publish(IOListsTopicID, payload);
        }
        private void Protocol_OnClosed(object? sender, EventArgs e)
        {
            OnRosSocketDisconnected?.Invoke(this, e);
            rosSocket.protocol.OnClosed -= Protocol_OnClosed;
            logger.Warn("Rosbridger Server On Closed...Retry connecting...");
            TryConnecting = true;
            Connect();
        }

        public override void Disconnect()
        {
            rosSocket.Close();
            rosSocket = null;
        }

        public override bool IsConnected()
        {
            return rosSocket != null && Connected;
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
                    logger.Error(ex.Message, ex);
                }
                actionClient.Dispose();
            }
            actionClient = new TaskCommandActionClient("/barcodemovebase", rosSocket);
            actionClient.OnActionStatusChanged += (sender, status) =>
            {
                logger.Info($"車控-ActionServer: Action Status Changed To : {status}");
                if (OnAGVCActionChanged != null)
                    OnAGVCActionChanged(status);
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
                logger.Warn($"AGV Is Navigating at working station, Cycle Stop is not nessary.");
                return true;
            }
            bool cycleStopAccept = CycleStopActionExecuting = await CarSpeedControl(ROBOT_CONTROL_CMD.STOP_WHEN_REACH_GOAL, actionClient.goal.taskID, SPEED_CONTROL_REQ_MOMENT.CYCLE_STOP);
            return cycleStopAccept;
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
                    logger.Warn("DisposeTaskCommandActionClient Exception Occur :　" + ex.Message);
                }
            }
        }

        private void SickLocalizationStateCallback(LocalizationControllerResultMessage0502 _LocalizationControllerResult)
        {
            LocalizationControllerResult = _LocalizationControllerResult;
            OnSickLocalicationDataUpdated?.Invoke(this, _LocalizationControllerResult);
        }

        internal async Task CarSpeedControl(ROBOT_CONTROL_CMD cmd)
        {
            await CarSpeedControl(cmd, RunningTaskData.Task_Name, SPEED_CONTROL_REQ_MOMENT.UNKNOWN, true);
        }
        internal async Task CarSpeedControl(ROBOT_CONTROL_CMD cmd, SPEED_CONTROL_REQ_MOMENT moment, bool CheckLaserStatus = true)
        {
            await CarSpeedControl(cmd, RunningTaskData.Task_Name, moment, CheckLaserStatus);
        }

        public static ROBOT_CONTROL_CMD AGVS_SPEED_CONTROL_REQUEST = ROBOT_CONTROL_CMD.NONE;

        private ROBOT_CONTROL_CMD _CurrentSpeedControlCmd = ROBOT_CONTROL_CMD.NONE;
        public ROBOT_CONTROL_CMD CurrentSpeedControlCmd
        {
            get => _CurrentSpeedControlCmd;
            set
            {
                if (_CurrentSpeedControlCmd != value)
                {
                    OnSpeedControlChanged?.Invoke(this, value);
                    _CurrentSpeedControlCmd = value;
                }
            }
        }
        public async Task<bool> CarSpeedControl(ROBOT_CONTROL_CMD cmd, string task_id, SPEED_CONTROL_REQ_MOMENT moment, bool CheckLaserStatus = true)
        {
            bool _confirmed = false;
            ManualResetEvent mre = new ManualResetEvent(false);
            speedControlDebuncer.OnActionCanceled += SpeedControlDebuncer_OnActionCanceled;
            speedControlDebuncer.Debounce(async () =>
            {
                try
                {
                    if (cmd == ROBOT_CONTROL_CMD.SPEED_Reconvery && OnSpeedRecoveryRequesting != null)
                    {

                        (bool confirmed, string message) = OnSpeedRecoveryRequesting();
                        if (!confirmed && CheckLaserStatus)
                        {
                            logger.Info($"[ROBOT_CONTROL_CMD] {message}");
                            _confirmed = false;
                            return;
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
                        logger.Warn($"[ROBOT_CONTROL_CMD] 車控無回復 {cmd}({moment}) 請求: {(res.confirm ? "OK" : "NG")} (Task ID={task_id})");
                        _confirmed = false;
                        return;
                    }
                    logger.Info($"[ROBOT_CONTROL_CMD] 車控回復 {cmd}({moment}) 請求: {(res.confirm ? "OK" : "NG")} (Task ID={task_id})");
                    if (cmd == ROBOT_CONTROL_CMD.STOP)
                    {
                        OnSTOPCmdRequesting?.Invoke(this, EventArgs.Empty);
                    }
                    CurrentSpeedControlCmd = res.confirm ? cmd : CurrentSpeedControlCmd;
                    _confirmed = res.confirm;
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    mre.Set();

                }
            }, cmd == ROBOT_CONTROL_CMD.DECELERATE || cmd == ROBOT_CONTROL_CMD.STOP ? 10 : 300);

            bool timeout = !mre.WaitOne(TimeSpan.FromSeconds(3));
            speedControlDebuncer.OnActionCanceled -= SpeedControlDebuncer_OnActionCanceled;
            if (timeout)
                return false;
            return _confirmed;

            void SpeedControlDebuncer_OnActionCanceled(object? sender, string e)
            {
                _confirmed = false;
                mre.Set();
            }
        }


        internal virtual async Task<SendActionCheckResult> ExecuteTaskDownloaded(clsTaskDownloadData taskDownloadData, double action_timeout = 5)
        {
            RunningTaskData = taskDownloadData;
            return await SendGoal(RunningTaskData.RosTaskCommandGoal, action_timeout);
        }

        CancellationTokenSource wait_agvc_execute_action_cts;

        internal bool IsRunning => ActionStatus == ActionStatus.PENDING || ActionStatus == ActionStatus.ACTIVE;

        internal async Task<SendActionCheckResult> SendGoal(TaskCommandGoal rosGoal, double timeout = 5)
        {
            (bool checkTaskConfirmed, TaskCommandGoal goalModified) = await CheckTaskCommandGoal(rosGoal);
            bool isEmptyPathPlan = rosGoal.planPath.poses.Length == 0;
            string new_path = isEmptyPathPlan ? "" : string.Join("->", rosGoal.planPath.poses.Select(p => p.header.seq));
            if (isEmptyPathPlan)
                logger.Warn("Empty Action Goal To AGVC To Emergency Stop AGV");
            else
                _IsEmergencyStopFlag = false;

            CycleStopActionExecuting = false;

            if (_ActionStatus != ActionStatus.PENDING && _ActionStatus != ActionStatus.ACTIVE)
            {
                logger.Warn($"[SendGoal] Action Status is not PENDING or ACTIVE. Current Status = {_ActionStatus}");
                _ActionStatus = ActionStatus.NO_GOAL;
            }
            SendActionCheckResult? preCheckWhenActionGoalSendToAGVC = OnActionSendToAGVCRaising?.Invoke();

            if (!preCheckWhenActionGoalSendToAGVC.Accept)
            {
                return preCheckWhenActionGoalSendToAGVC;
            }
            actionClient.goal = goalModified;
            logger.Info("Action Goal Will Send To AGVC:\r\n" + actionClient.goal.ToJson(Formatting.Indented));
            actionClient?.SendGoal();
            if (isEmptyPathPlan)
            {
                return new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.Accept);
            }
            wait_agvc_execute_action_cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            while (_ActionStatus != ActionStatus.PENDING && _ActionStatus != ActionStatus.ACTIVE)
            {
                logger.Info($"[SendGoal] Action Status Monitor .Status = {ActionStatus}", false);
                await Task.Delay(1);
                if (wait_agvc_execute_action_cts.IsCancellationRequested)
                {
                    string error_msg = $"發送任務請求給車控但車控並未接收成功-AGVC Status={ActionStatus}";
                    logger.Error(error_msg);
                    AbortTask();
                    return new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.AGVC_CANNOT_EXECUTE_ACTION);
                }
            }
            logger.Info($"AGVC Accept Task and Start Executing：Current_Status= {ActionStatus},Path Tracking = {new_path}(Destine={rosGoal.finalGoalID})");
            OnAGVCActionActive?.Invoke(this, EventArgs.Empty);

            if (!checkTaskConfirmed)
            {
                logger.Trace($"Wait AGVC ActionStatus Success");

                while (_ActionStatus != ActionStatus.SUCCEEDED && _ActionStatus != ActionStatus.NO_GOAL)
                {
                    await Task.Delay(1);
                }
                logger.Trace($"Wait AGVC ActionStatus now is Success,Resend Task");

                int indexOfCurrentTag = rosGoal.pathInfo.Select(p => p.tagid).ToList().FindIndex(p => p == this.lastVisitedNode); // 0 , 1 
                rosGoal.pathInfo = rosGoal.pathInfo.Skip(indexOfCurrentTag).ToArray();
                rosGoal.planPath.poses = rosGoal.planPath.poses.Skip(indexOfCurrentTag).ToArray();
                return await SendGoal(rosGoal, timeout);
            }

            return new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.Accept);
        }

        protected virtual async Task<(bool confirmed, TaskCommandGoal goalModified)> CheckTaskCommandGoal(TaskCommandGoal newGoal)
        {
            try
            {
                if (newGoal.planPath.poses.Length == 0)
                {
                    logger.Trace($"[CheckTaskCommandGoal] path of goal is empty. Return.");
                    return (true, newGoal);
                }

                TaskCommandGoal _previousGoal = actionClient.goal;

                bool _IsPathOfGoalSegment(TaskCommandGoal _taskCmdGoal)
                {
                    if (_taskCmdGoal.pathInfo?.Length == 0)
                        return false;

                    return _taskCmdGoal.pathInfo?.Last().tagid != _taskCmdGoal.finalGoalID;
                }

                if (_IsPathOfGoalSegment(_previousGoal))
                {
                    //如果上一個任務的最後一個點不是目標點，則新的任務的第一個點必須與上一個任務的PathInfo的第一點相同
                    bool IsPathStartPtNotMatched = _previousGoal.pathInfo[0].tagid != newGoal.pathInfo[0].tagid;
                    bool IsFinalGoalNotMatched = _previousGoal.finalGoalID != newGoal.finalGoalID;

                    if (IsPathStartPtNotMatched || IsFinalGoalNotMatched)
                    {
                        string _previousGoalTags = string.Join(",", _previousGoal.pathInfo.Select(p => p.tagid));
                        string _newGoalTags = string.Join(",", newGoal.pathInfo.Select(p => p.tagid));

                        logger.Warn($"Previous Goal Tags = {_previousGoalTags}");
                        logger.Warn($"New      Goal Tags = {_newGoalTags}");

                        logger.Warn($"[CheckTaskCommandGoal] The first point of the new goal is not the same as the last point of the previous goal.");
                        logger.Warn($"[CheckTaskCommandGoal] Cycle Stop Action is not executing, start cycle stop action first.");

                        return (false, new TaskCommandGoal());
                    }
                    else
                    {
                        return (true, newGoal);
                    }
                }
                else
                {
                    return (true, newGoal);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw ex;
            }

        }

        public abstract Task<(bool request_success, bool action_done)> TriggerCSTReader();
        public abstract Task<(bool request_success, bool action_done)> TriggerCSTReader(CST_TYPE cst_type);
        public abstract Task<(bool request_success, bool action_done)> AbortCSTReader();

        internal async Task EmergencyStop(bool bypass_stopped_check = false)
        {
            if (!bypass_stopped_check && _IsEmergencyStopFlag)
                return;

            logger.Error("發送空任務請求車控緊急停止");
            OnAGVCActionChanged = null;
            OnAGVCActionChanged += (status) =>
            {
                _IsEmergencyStopFlag = true;
                OnAGVCActionChanged = null;
            };
            await SendGoal(new TaskCommandGoal());//下空任務清空
            _ActionStatus = ActionStatus.NO_GOAL;
            _IsEmergencyStopFlag = true;
        }
        protected virtual string SetCurrentTagServiceName { get; set; } = "/request_initial_robot_pose_with_tag";
        /// <summary>
        ///  由車載畫面設定機器人目前位置。
        /// </summary>
        /// <param name="tagID"></param>
        /// <param name="map_name"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="theta"></param>
        /// <returns></returns>
        public async Task<(bool confrim, string message)> SetCurrentTagID(ushort tagID, string map_name, double x, double y, double theta)
        {
            logger.Trace($"Call Service= {SetCurrentTagServiceName}");
            SetcurrentTagIDResponse response = await rosSocket.CallServiceAndWait<SetcurrentTagIDRequest, SetcurrentTagIDResponse>(SetCurrentTagServiceName,

                new SetcurrentTagIDRequest
                {
                    tagID = tagID,
                    map = map_name,
                    X = x,
                    Y = y,
                    angle = theta
                }
                );

            return (response == null ? false : response.confirm, response == null ? "Call Service Error" : "");
        }

        public async Task<(bool confirm, string message)> ResetSickLaser()
        {
            logger.Trace($"Reset Sick Laser Method Invoke");
            SetcurrentTagIDResponse response = await rosSocket.CallServiceAndWait<SetcurrentTagIDRequest, SetcurrentTagIDResponse>(SetCurrentTagServiceName,

             new SetcurrentTagIDRequest
             {
                 tagID = 1000,
                 map = "map_name",
                 X = 0,
                 Y = 0,
                 angle = 0
             }
             );

            return (response == null ? false : response.confirm, response == null ? "Call Service Error" : "");
        }

        internal async Task<bool> HandleAlarm(AlarmCodes alarm)
        {
            if (StaSysControl.isAGVCRestarting)
                return false;
            logger.Error($"");
            return true;
        }
    }
}
