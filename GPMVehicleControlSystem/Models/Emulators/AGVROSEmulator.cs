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

namespace GPMVehicleControlSystem.Models.Emulators
{

    public class AGVROSEmulator
    {
        protected RosSocket? rosSocket;
        private ModuleInformation module_info = new ModuleInformation()
        {
            Battery = new BatteryState
            {
                state = 1,
                batteryLevel = 90,
                batteryID = 0
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
        public List<ushort> ChargeStationTags = new List<ushort>() { 50, 52, 6, 10 };
        private bool IsCharge = false;
        private bool IsCSTTriggering = false;
        public clsEmulatorParams EmuParam => StaStored.CurrentVechicle.Parameters.Emulator;
        public AGVROSEmulator()
        {
            var param = VehicleControl.Vehicles.Vehicle.LoadParameters();
            string RosBridge_IP = param.Connections["RosBridge"].IP;
            int RosBridge_Port = param.Connections["RosBridge"].Port;
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

                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    TopicsAdvertise();
                    AdvertiseServices();
                    InitNewTaskCommandActionServer();
                    TopicsPublish();
                    // _ = PublishLocalizeResult(rosSocket);
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
        bool isPreviousMoveActionNotFinish
        {
            get => _isPreviousMoveActionNotFinish;
            set
            {
                _isPreviousMoveActionNotFinish = value;
                LOG.TRACE($"isPreviousMoveActionNotFinish={value}");
            }
        }
        private void NavGaolHandle(object sender, TaskCommandGoal obj)
        {
            Task.Factory.StartNew(() =>
            {
                TaskCommandActionServer actionServer = (TaskCommandActionServer)sender;

                emergency_stop = false;
                if (obj.planPath.poses.Length == 0) //急停效果
                {
                    actionServer.AcceptedInvoke();
                    emergency_stop = true;
                    EmuLog($"[ROS 車控模擬器] 空任務-緊急停止!!");
                    isPreviousMoveActionNotFinish = false;
                    previousTaskAction = null;
                    actionServer.SucceedInvoke();
                    return;
                }
                CancellationTokenSource cts = new CancellationTokenSource();
                var Task_Watch_StatusActive = new Task(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(1);
                        if (actionServer.GetStatus() != ActionStatus.ACTIVE)
                        {
                            cts.Cancel();
                            break;
                        }
                    }

                });
                EmuLog($"[ROS 車控模擬器] New Task , Task Name = {obj.taskID}, Tags Path = {string.Join("->", obj.planPath.poses.Select(p => p.header.seq))}");
                RobotStopMRE = new ManualResetEvent(true);
                //是否為分段任務
                //模擬走型
                Task.Run(async () =>
                {
                    var firstTag = obj.planPath.poses.First().header.seq;
                    IsCharge = false;
                    module_info.Battery.dischargeCurrent = 13200;
                    module_info.Battery.chargeCurrent = 0;

                    for (int i = 0; i < obj.planPath.poses.Length; i++)
                    {
                        while (StaStored.CurrentVechicle.WagoDO.GetState(DO_ITEM.Horizon_Motor_Stop))
                        {
                            Console.WriteLine($"[Move simulation] Motro Stop. wait...");
                            if (cts.IsCancellationRequested)
                                return;
                            await Task.Delay(1000);
                        }
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
                            //計算距離
                            if (current_position.x == 0 && current_position.y == 0)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(0.5));
                            }
                            else
                            {
                                if (EmuParam.Move_Time_Mode == clsEmulatorParams.MOVE_TIME_EMULATION.DISTANCE)
                                {
                                    var distance = Math.Sqrt(Math.Pow(tag_pose_x - current_position.x, 2) + Math.Pow(tag_pose_y - current_position.y, 2)); //m
                                    await Task.Delay(TimeSpan.FromSeconds(distance / 0.8), cts.Token);
                                }
                                else
                                    await Task.Delay(TimeSpan.FromSeconds(EmuParam.Move_Fixed_Time), cts.Token);
                            }
                            //module_info.Battery.batteryLevel -= 0x01;
                            module_info.nav_state.lastVisitedNode.data = (int)tag;
                            module_info.nav_state.robotPose.pose.position.x = tag_pose_x;
                            module_info.nav_state.robotPose.pose.position.y = tag_pose_y;
                            module_info.nav_state.robotPose.pose.orientation = tag_theta.ToQuaternion();

                            module_info.reader.tagID = tag;
                            module_info.reader.xValue = tag_pose_x;
                            module_info.reader.yValue = tag_pose_y;
                            module_info.reader.theta = tag_theta;
                            EmuLog($"Barcode data change to = {module_info.reader.ToJson()}");
                            if (complex_cmd == ROBOT_CONTROL_CMD.STOP_WHEN_REACH_GOAL)
                                break;
                            RobotStopMRE.WaitOne();
                        }
                        catch (Exception ex)
                        {
                            EmuLog(ex.Message + $"\r\n{ex.StackTrace}");
                        }
                        if (i == 0)
                            Task_Watch_StatusActive.Start();
                    }
                    if (ChargeStationTags.Contains(obj.finalGoalID))
                    {
                        IsCharge = true;
                        module_info.Battery.chargeCurrent = 23000;
                        module_info.Battery.dischargeCurrent = 0;
                        _ = Task.Factory.StartNew(async () =>
                        {
                            while (IsCharge)
                            {
                                await Task.Delay(1000);
                                module_info.Battery.batteryLevel += 0x05;
                                if (module_info.Battery.batteryLevel >= 100)
                                {
                                    IsCharge = false;
                                    module_info.Battery.batteryLevel = 100;
                                    module_info.Battery.chargeCurrent = 500;
                                    module_info.Battery.dischargeCurrent = 0;
                                }
                            }
                        });
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
                    await Task.Delay(1000);
                    actionServer.SucceedInvoke();
                });

            });
        }

        private async Task PublishModuleInformation(RosSocket rosSocket)
        {
            await Task.Delay(1);
            await Task.Factory.StartNew(async () =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (rosSocket.protocol.IsAlive())
                {
                    try
                    {
                        await Task.Delay(10);

                        if (stopwatch.ElapsedMilliseconds > 30000)
                        {
                            module_info.Battery.batteryLevel -= 1;
                            module_info.Battery.batteryLevel = (byte)(module_info.Battery.batteryLevel <= 1 ? 1 : module_info.Battery.batteryLevel);
                            stopwatch.Restart();
                        }

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
        private void EmuLog(string msg, bool show_console = true)
        {
            LOG.TRACE($"[車控模擬] {msg}", show_console);
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
            module_info.IMU.imuData.linear_acceleration.x = module_info.IMU.imuData.linear_acceleration.y = 9.81;
            await Task.Delay(100);
            module_info.IMU.imuData.linear_acceleration.x = module_info.IMU.imuData.linear_acceleration.y = 0;
        }
    }
}
