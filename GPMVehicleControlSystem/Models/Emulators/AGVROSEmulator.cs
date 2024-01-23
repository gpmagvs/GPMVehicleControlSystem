using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Protocols;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using RosSharp.RosBridgeClient.Actionlib;
using AGVSystemCommonNet6.GPMRosMessageNet.Actions;
using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Log;
using System.Linq;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using System.Diagnostics;
using System.Drawing;

namespace GPMVehicleControlSystem.Models.Emulators
{

    public partial class AGVROSEmulator
    {
        protected RosSocket? rosSocket;
        private ModuleInformation module_info = new ModuleInformation()
        {
            Battery = new BatteryState
            {
                state = 1,
                batteryLevel = 90,
                batteryID = 0,
                Voltage = 2323,
                maxCellTemperature = 28,
                dischargeCurrent = 12444
            },
            CSTReader = new CSTReaderState
            {
                state = 1
            },
            nav_state = new NavigationState
            {
                lastVisitedNode = new RosSharp.RosBridgeClient.MessageTypes.Std.Int32(31)
            },
            reader = new BarcodeReaderState
            {
                tagID = 5
            },
            Wheel_Driver = new DriversState()
            {
                driversState = new DriverState[2]
                 {
                     new DriverState{ errorCode=21},
                     new DriverState{ errorCode=21}
                 }
            },
            IMU = new GpmImuMsg
            {
                imuData = new RosSharp.RosBridgeClient.MessageTypes.Sensor.Imu
                {
                    linear_acceleration = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3
                    {
                        z = 9.8
                    }
                }
            }
        };
        private LocalizationControllerResultMessage0502 localizeResult = new LocalizationControllerResultMessage0502();
        private ManualResetEvent RobotStopMRE = new ManualResetEvent(true);
        private ROBOT_CONTROL_CMD complex_cmd;
        private bool _CycleStopFlag = false;
        public List<ushort> ChargeStationTags = new List<ushort>() { 50, 52, 6, 10 };
        private bool IsCharge = false;
        private bool IsCSTTriggering = false;
        public delegate bool ChargeSimulateActiveCheckDelegate(int tag);
        public ChargeSimulateActiveCheckDelegate OnChargeSimulationRequesting;
        private VehicleControl.Vehicles.Vehicle Agv => StaStored.CurrentVechicle;
        public delegate PointF OnLastVisitedTagChangedDelegate(int tag);
        public OnLastVisitedTagChangedDelegate OnLastVisitedTagChanged;
        public clsEmulatorParams EmuParam => StaStored.CurrentVechicle.Parameters.Emulator;
        public AGVROSEmulator(clsEnums.AGV_TYPE agvType = clsEnums.AGV_TYPE.SUBMERGED_SHIELD)
        {
            this.agvType = agvType;
            var param = VehicleControl.Vehicles.Vehicle.LoadParameters();
            string RosBridge_IP = param.Connections[clsConnectionParam.CONNECTION_ITEM.RosBridge].IP;
            int RosBridge_Port = param.Connections[clsConnectionParam.CONNECTION_ITEM.RosBridge].Port;
            module_info.nav_state.lastVisitedNode = new RosSharp.RosBridgeClient.MessageTypes.Std.Int32(param.LastVisitedTag);

            bool connected = Init(RosBridge_IP, RosBridge_Port);
            if (!connected)
            {
                HandleRosDisconnected(null, EventArgs.Empty);

            }
            void HandleRosDisconnected(object sender, EventArgs e)
            {
                rosSocket.protocol.OnClosed -= HandleRosDisconnected;
                Task.Factory.StartNew(async () =>
                {
                    EmuLog("AGVC ROS Trying Reconnect...");
                    while (!Init(RosBridge_IP, RosBridge_Port))
                    {
                        await Task.Delay(1000);
                    }
                    EmuLog("AGVC ROS Reconnected");
                    rosSocket.protocol.OnClosed += HandleRosDisconnected;
                    PublishModuleInformation(rosSocket);
                });
            }
            rosSocket.protocol.OnClosed += HandleRosDisconnected;
        }

        private bool Init(string RosBridge_IP, int RosBridge_Port)
        {
            bool isconnected = false;
            try
            {
                rosSocket = new RosSocket(new WebSocketSharpProtocol($"ws://{RosBridge_IP}:{RosBridge_Port}"));
                isconnected = rosSocket.protocol.IsAlive();
            }
            catch (Exception ex)
            {
                return false;
            }
            if (isconnected)
            {
                Task.Run(async () =>
                {
                    Thread.Sleep(100);
                    TopicsAdvertise();
                    AdvertiseServices();
                    InitNewTaskCommandActionServer();
                    TopicsPublish();
                });
            }
            return isconnected;
        }


        internal virtual void TopicsPublish()
        {
            _ = PublishModuleInformation(rosSocket);
        }

        /// <summary>
        /// 建立Topic
        /// </summary>
        internal virtual void TopicsAdvertise()
        {
            rosSocket.Advertise<ModuleInformation>("AGVC_Emu", "/module_information");
            rosSocket.Advertise<LocalizationControllerResultMessage0502>("SICK_Emu", "localizationcontroller/out/localizationcontroller_result_message_0502");
        }
        /// <summary>
        /// 建立服務
        /// </summary>
        internal virtual void AdvertiseServices()
        {
            rosSocket.AdvertiseService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action", CstReaderServiceCallack);
            rosSocket.AdvertiseService<ComplexRobotControlCmdRequest, ComplexRobotControlCmdResponse>("/complex_robot_control_cmd", ComplexRobotControlCallBack);
            rosSocket.AdvertiseService<VerticalCommandRequest, VerticalCommandResponse>("/command_action", VerticalActionCallback);
        }


        /// <summary>
        /// 建立 barcodemovebase server
        /// </summary>
        internal virtual void InitNewTaskCommandActionServer()
        {
            TaskCommandActionServer actionServer = new TaskCommandActionServer("/barcodemovebase", rosSocket);
            actionServer.Initialize();
            actionServer.OnNAVGoalReceived += NavGaolHandle;
            Console.WriteLine("ROS Enum Action Server Created!");
        }
        TaskCommandGoal previousTaskAction;
        bool _isPreviousMoveActionNotFinish = false;
        bool emergency_stop = false;
        CancellationTokenSource emergency_stop_canceltoken_source = new CancellationTokenSource();
        private readonly clsEnums.AGV_TYPE agvType;

        bool isPreviousMoveActionNotFinish
        {
            get => _isPreviousMoveActionNotFinish;
            set
            {
                _isPreviousMoveActionNotFinish = value;
            }
        }
        private void NavGaolHandle(object sender, TaskCommandGoal obj)
        {
            TaskCommandActionServer actionServer = null;
            actionServer = (TaskCommandActionServer)sender;

            if (obj.planPath.poses.Length == 0) //急停效果
            {
                EmuLog($"[ROS 車控模擬器] 空任務-緊急停止!!");
                try
                {
                    actionServer?.SucceedInvoke();
                }
                catch (Exception)
                {
                }
                actionServer?.AcceptedInvoke();
                emergency_stop = true;
                emergency_stop_canceltoken_source?.Cancel();
                isPreviousMoveActionNotFinish = false;
                previousTaskAction = null;
                Thread.Sleep(100);
                actionServer?.SucceedInvoke();
                return;
            }
            EmuLog($"[ROS 車控模擬器] New Task , Task Name = {obj.taskID}, Tags Path = {string.Join("->", obj.planPath.poses.Select(p => p.header.seq))}");
            actionServer.SetPeddingInvoke();
            Thread.Sleep(100);
            actionServer.SetActiveInvoke();
            Task.Factory.StartNew(async () =>
             {
                 try
                 {
                     CancellationTokenSource cts = new CancellationTokenSource();
                     emergency_stop_canceltoken_source = new CancellationTokenSource();
                     emergency_stop = false;
                     RobotStopMRE = new ManualResetEvent(true);
                     _CycleStopFlag = false;
                     complex_cmd = ROBOT_CONTROL_CMD.SPEED_Reconvery;
                     var firstTag = obj.planPath.poses.First().header.seq;
                     IsCharge = false;
                     module_info.Battery.dischargeCurrent = 13200;
                     module_info.Battery.chargeCurrent = 0;

                     for (int i = 0; i < obj.planPath.poses.Length; i++)
                     {
                         if (cts.IsCancellationRequested)
                         {
                             isPreviousMoveActionNotFinish = false;
                             previousTaskAction = null;
                             actionServer.SucceedInvoke();
                             return;
                         }
                         try
                         {
                             var pose = obj.planPath.poses[i];
                             uint tag = pose.header.seq;
                             if (isPreviousMoveActionNotFinish && previousTaskAction.planPath.poses.Length > i)
                             {
                                 if (tag == previousTaskAction.planPath.poses[i].header.seq)
                                 {
                                     LOG.TRACE($"Tag {tag} already pass.");
                                     continue;
                                 }
                             }
                             double tag_pose_x = pose.pose.position.x;
                             double tag_pose_y = pose.pose.position.y;
                             double tag_theta = pose.pose.orientation.ToTheta();
                             var current_position = module_info.nav_state.robotPose.pose.position;
                             var delay_time = EmuParam.Move_Fixed_Time;
                             //計算距離
                             if (current_position.x == 0 && current_position.y == 0)
                             {
                                 await Task.Delay(TimeSpan.FromSeconds(delay_time));
                             }

                             double distanceFromDestine(RosSharp.RosBridgeClient.MessageTypes.Geometry.Point current, RosSharp.RosBridgeClient.MessageTypes.Geometry.Point destine)
                             {
                                 return Math.Sqrt(Math.Pow(destine.x - current.x, 2) + Math.Pow(destine.y - current.y, 2)); //m
                             }

                             var _speed = 2; //m/s
                             var destine_position = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Point(tag_pose_x, tag_pose_y, 0);
                             double _distance_to_next_point = distanceFromDestine(module_info.nav_state.robotPose.pose.position, destine_position);
                             double _timespend = _distance_to_next_point / _speed;
                             var _x_delta = tag_pose_x - module_info.nav_state.robotPose.pose.position.x; //x方向距離  m/s
                             var _y_delta = tag_pose_y - module_info.nav_state.robotPose.pose.position.y;

                             var _x_speed = _x_delta / _timespend;
                             var _y_speed = _y_delta / _timespend;

                             EmuLog($"next potin time spend...{_timespend}");
                             Stopwatch stopwatch = Stopwatch.StartNew();
                             while (stopwatch.ElapsedMilliseconds <= _timespend * 1000)
                             {
                                 if (emergency_stop_canceltoken_source.Token.IsCancellationRequested)
                                     throw new TaskCanceledException();
                                 await Task.Delay(TimeSpan.FromMilliseconds(100), emergency_stop_canceltoken_source.Token);
                                 module_info.nav_state.robotPose.pose.position.x += _x_speed / 10.0;
                                 module_info.nav_state.robotPose.pose.position.y += _y_speed / 10.0;
                                 module_info.Mileage += _distance_to_next_point / 1000.0 * 0.1;
                             }
                             EmuLog($"Reach...{tag}");
                             module_info.nav_state.lastVisitedNode.data = (int)tag;
                             module_info.nav_state.robotPose.pose.position.x = tag_pose_x;
                             module_info.nav_state.robotPose.pose.position.y = tag_pose_y;
                             module_info.nav_state.robotPose.pose.orientation = tag_theta.ToQuaternion();

                             module_info.reader.tagID = tag;
                             module_info.reader.xValue = tag_pose_x;
                             module_info.reader.yValue = tag_pose_y;
                             module_info.reader.theta = tag_theta;
                             module_info.IMU.imuData.linear_acceleration.x = 0.02 + DateTime.Now.Second / 100.0;

                             module_info.IMU.imuData.linear_acceleration.x = 0.0001;
                             module_info.Battery.batteryLevel -= 1;

                             if (_CycleStopFlag || emergency_stop)
                             {
                                 EmuLog("Cycle Stop Flag ON, Stop move.");
                                 break;
                             }

                             if (tag == 18 & i == obj.planPath.poses.Length - 1 & Debugger.IsAttached)
                             {
                                 module_info.nav_state.errorCode = 4;
                                 break;
                             }
                             RobotStopMRE.WaitOne();
                         }
                         catch (TaskCanceledException ex)
                         {
                             EmuLog("Task Canceled. " + ex.Message + $"\r\n{ex.StackTrace}");
                             actionServer?.SucceedInvoke();
                             return;
                         }
                         catch (Exception ex)
                         {
                             EmuLog(ex.Message + $"\r\n{ex.StackTrace}");
                             actionServer?.SucceedInvoke();
                             return;
                         }

                     }

                     if (OnChargeSimulationRequesting != null)
                     {
                         bool charging_simulation = OnChargeSimulationRequesting(obj.finalGoalID);
                         if (charging_simulation)
                             StartChargeSimulation();
                     }
                     previousTaskAction = obj;
                     EmuLog($"Final GoalID => {obj.finalGoalID} , Trajectory Final = {obj.planPath.poses.Last().header.seq}");
                     if (complex_cmd != ROBOT_CONTROL_CMD.STOP_WHEN_REACH_GOAL)
                     {
                         isPreviousMoveActionNotFinish = obj.finalGoalID != obj.planPath.poses.Last().header.seq;
                     }
                     else
                     {
                         isPreviousMoveActionNotFinish = false;
                         previousTaskAction = null;
                     }
                     actionServer?.SucceedInvoke();
                 }
                 catch (Exception ex)
                 {
                     EmuLog($"WTF-{ex.Message}-{ex.StackTrace}");
                 }
             });
        }

        private void StartChargeSimulation()
        {
            IsCharge = true;
            module_info.Battery.chargeCurrent = 23000;
            module_info.Battery.dischargeCurrent = 0;
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                while (IsCharge)
                {
                    Thread.Sleep(1000);
                    module_info.Battery.batteryLevel += 0x04;
                    module_info.Battery.Voltage += 200;
                    if (module_info.Battery.batteryLevel >= 100)
                    {
                        IsCharge = false;
                        module_info.Battery.batteryLevel = 100;
                        module_info.Battery.chargeCurrent = 500;
                        module_info.Battery.dischargeCurrent = 12330;
                        module_info.Battery.Voltage = 2800;

                    }
                }
            });
        }

        private async Task PublishModuleInformation(RosSocket rosSocket)
        {
            await Task.Delay(1);
            await Task.Run(async () =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (rosSocket.protocol.IsAlive())
                {
                    try
                    {
                        Thread.Sleep(10);
                        module_info.Battery.maxCellTemperature = (byte)(24 + DateTime.Now.Second / 10.0);
                        if (stopwatch.ElapsedMilliseconds > 5000)
                        {
                            module_info.Battery.batteryLevel -= 1;
                            module_info.Battery.Voltage -= 100;
                            module_info.Battery.batteryLevel = (byte)(module_info.Battery.batteryLevel <= 5 ? 5 : module_info.Battery.batteryLevel);
                            module_info.Battery.batteryLevel = (byte)(module_info.Battery.batteryLevel > 100 ? 100 : module_info.Battery.batteryLevel);
                            module_info.Battery.Voltage = (ushort)(module_info.Battery.Voltage <= 2400 ? 2400 : module_info.Battery.Voltage);
                            LOGBatteryStatus(module_info.Battery);
                            stopwatch.Restart();
                        }
                        module_info.Action_Driver.position = (float)currentVerticalPosition;
                        rosSocket.Publish("AGVC_Emu", module_info);
                    }
                    catch (Exception ex)
                    {
                        EmuLog($"Ros socket error,{ex.Message}");
                    }
                }
                EmuLog($"AGVC ROS Emu module-information publish process end.");

            });
        }

        private void LOGBatteryStatus(BatteryState battery)
        {
            //I1130 12:47:19.313843  2141 INNERS.h:512] 0, 99, 26653, 0, 4560, 28
            //C:\Users\jinwei\Documents\GPM LOG\2023-12-04\batteryLog
            string logFolder = Path.Combine(StaStored.CurrentVechicle.Parameters.BatteryModule.BatteryLogFolder, $@"{DateTime.Now.ToString("yyyy-MM-dd")}\batteryLog");
            Directory.CreateDirectory(logFolder);
            string logFilePath = Path.Combine(logFolder, "batteryLog.INFO");
            using StreamWriter writer = new StreamWriter(logFilePath, true);
            writer.WriteLine($"I1130 {DateTime.Now.ToString("HH:mm:ss.ffffff")}  2141 INNERS.h:512] 0, {battery.batteryLevel}, {battery.Voltage}, {battery.chargeCurrent}, {battery.dischargeCurrent}, {battery.maxCellTemperature}");
        }

        private bool ComplexRobotControlCallBack(ComplexRobotControlCmdRequest req, out ComplexRobotControlCmdResponse res)
        {
            if (req.reqsrv == 2) //要求停止
            {
                complex_cmd = ROBOT_CONTROL_CMD.STOP;
                RobotStopMRE.Reset();
            }
            else if (req.reqsrv == 0)
            {
                complex_cmd = ROBOT_CONTROL_CMD.SPEED_Reconvery;
                RobotStopMRE.Set();
            }
            else if (req.reqsrv == 1)
            {
                complex_cmd = ROBOT_CONTROL_CMD.DECELERATE;
                RobotStopMRE.Set();
            }
            else if (req.reqsrv == 100)
            {
                isPreviousMoveActionNotFinish = false;
                _CycleStopFlag = true;
                complex_cmd = ROBOT_CONTROL_CMD.STOP_WHEN_REACH_GOAL;
            }
            res = new ComplexRobotControlCmdResponse
            {
                confirm = true
            };
            return true;
        }

        private bool CstReaderServiceCallack(CSTReaderCommandRequest tin, out CSTReaderCommandResponse tout)
        {
            tout = new CSTReaderCommandResponse
            {
                confirm = true,
            };
            module_info.CSTReader.state = 2;
            module_info.CSTReader.data = "";
            Task.Factory.StartNew(async () =>
                {
                    //模擬拍照
                    await Task.Delay(500);
                    module_info.CSTReader.state = 1;
                    module_info.CSTReader.data = $"TA030{DateTime.Now.ToString("HHmsf")}";
                    rosSocket.CallServiceAndWait<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_done_action", new CSTReaderCommandRequest
                    {
                        model = tin.model,
                        command = "done",
                    });

                });

            return true;
        }
        internal void EmuLog(string msg, bool show_console = true)
        {
            LOG.TRACE($"[車控模擬] {msg}", show_console, color: ConsoleColor.Magenta);
        }

        internal void SetInitTag(int lastVisitedTag)
        {
            module_info.reader.tagID = (uint)lastVisitedTag;
            module_info.nav_state.lastVisitedNode = new RosSharp.RosBridgeClient.MessageTypes.Std.Int32(lastVisitedTag);
        }

        internal void SetCoordination(double x, double y, int theta)
        {
            module_info.nav_state.robotPose.pose.position = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Point(x, y, theta);
        }

        internal void ClearDriversErrorCodes()
        {
            SetDriversAlarm(0);
        }

        internal void SetDriversAlarm(int errorCode)
        {
            foreach (var item in module_info.Wheel_Driver.driversState)
            {
                item.errorCode = (byte)errorCode;
            }
            module_info.Action_Driver.errorCode = (byte)errorCode;
        }

        internal async void ImpactingSimulation()
        {
            module_info.IMU.imuData.linear_acceleration.x = module_info.IMU.imuData.linear_acceleration.y = 0;
            await Task.Delay(100);
            module_info.IMU.imuData.linear_acceleration.x = module_info.IMU.imuData.linear_acceleration.y = 9.81 * 1.5;
            await Task.Delay(100);
            module_info.IMU.imuData.linear_acceleration.x = module_info.IMU.imuData.linear_acceleration.y = 0;
        }

        internal async void PitchErrorSimulation()
        {
            module_info.IMU.imuData.linear_acceleration = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(-0.81 * 9.8, 0.06 * 9.8, 0.45 * 9.8);
        }
        internal async void PitchNormalSimulation()
        {
            module_info.IMU.imuData.linear_acceleration = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(0, 0, 9.8);
        }

        internal async void ClearErrorCodes()
        {
            module_info.nav_state.errorCode = 0;
            module_info.AlarmCode = new AlarmCodeMsg[0];
            module_info.Battery.errorCode = 0;
        }

        internal void SetBatteryLevel(byte level, int batIndex)
        {
            module_info.Battery.batteryLevel = level;
        }

        internal void SetImuData(RosSharp.RosBridgeClient.MessageTypes.Sensor.Imu imu)
        {
            module_info.IMU.imuData = imu;
        }

        internal void SetLastVisitedTag(int tag)
        {
            module_info.nav_state.lastVisitedNode = new RosSharp.RosBridgeClient.MessageTypes.Std.Int32(tag);
            module_info.reader.tagID = (uint)tag;
            if (OnLastVisitedTagChanged != null)
            {
                var _pt_coordination = OnLastVisitedTagChanged(tag);
                module_info.nav_state.robotPose.pose.position = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Point(_pt_coordination.X, _pt_coordination.Y, 0);
            }
        }
    }
}
