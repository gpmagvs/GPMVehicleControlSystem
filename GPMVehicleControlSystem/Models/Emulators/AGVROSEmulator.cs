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

namespace GPMVehicleControlSystem.Models.Emulators
{

    public class AGVROSEmulator
    {
        RosSocket? rosSocket;
        private ModuleInformation module_info = new ModuleInformation()
        {
            Battery = new BatteryState
            {
                state = 1,
                batteryLevel = 90,
                batteryID = 100
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
                tagID = 31
            }
        };
        private LocalizationControllerResultMessage0502 localizeResult = new LocalizationControllerResultMessage0502();
        private ManualResetEvent RobotStopMRE = new ManualResetEvent(true);
        public AGVROSEmulator()
        {
            var param = VehicleControl.Vehicles.Vehicle.LoadParameters();
            string RosBridge_IP = param.Connections["RosBridge"].IP;
            int RosBridge_Port = param.Connections["RosBridge"].Port;
            rosSocket = new RosSocket(new WebSocketSharpProtocol($"ws://{RosBridge_IP}:{RosBridge_Port}"));

            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                rosSocket.Advertise<ModuleInformation>("AGVC_Emu", "/module_information");
                rosSocket.AdvertiseService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action", CstReaderServiceCallack);
                rosSocket.Advertise<LocalizationControllerResultMessage0502>("SICK_Emu", "localizationcontroller/out/localizationcontroller_result_message_0502");
                rosSocket.AdvertiseService<ComplexRobotControlCmdRequest, ComplexRobotControlCmdResponse>("/complex_robot_control_cmd", ComplexRobotControlCallBack);
                InitNewTaskCommandActionServer();
                _ = PublishModuleInformation(rosSocket);
                // _ = PublishLocalizeResult(rosSocket);
            });
        }

        private void InitNewTaskCommandActionServer()
        {
            TaskCommandActionServer actionServer = new TaskCommandActionServer("/barcodemovebase", rosSocket);
            actionServer.Initialize();
            actionServer.OnNAVGoalReceived += NavGaolHandle;
            Console.WriteLine("ROS Enum Action Server Created!");
        }

        private void NavGaolHandle(object sender, TaskCommandGoal obj)
        {
            TaskCommandActionServer actionServer = (TaskCommandActionServer)sender;
            Console.WriteLine($"[ROS 車控模擬器] New Task , Task Name = {obj.taskID}, Tags Path = {string.Join("->", obj.planPath.poses.Select(p => p.header.seq))}");
            actionServer.AcceptedInvoke();
            RobotStopMRE = new ManualResetEvent(true);
            //模擬走型
            Task.Factory.StartNew(async () =>
            {
                foreach (RosSharp.RosBridgeClient.MessageTypes.Geometry.PoseStamped? item in obj.planPath.poses)
                {
                    uint tag = item.header.seq;
                    double tag_pose_x = item.pose.position.x;
                    double tag_pose_y = item.pose.position.y;
                    double tag_theta = item.pose.orientation.ToTheta();
                    module_info.nav_state.lastVisitedNode.data = (int)tag;
                    module_info.nav_state.robotPose.pose.position.x = tag_pose_x;
                    module_info.nav_state.robotPose.pose.position.y = tag_pose_y;
                    module_info.nav_state.robotPose.pose.orientation = tag_theta.ToQuaternion();
                    module_info.reader.tagID = tag;
                    module_info.reader.xValue = tag_pose_x;
                    module_info.reader.yValue = tag_pose_y;
                    module_info.reader.theta = tag_theta;
                    await Task.Delay(500);
                    RobotStopMRE.WaitOne();
                }
                actionServer.SucceedInvoke();
                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(500);
                    actionServer.Terminate();
                    actionServer = null;
                    InitNewTaskCommandActionServer();
                });
            });

        }

        private async Task PublishLocalizeResult(RosSocket rosSocket)
        {
            await Task.Delay(1);
            await Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(10);
                    try
                    {
                        rosSocket.Publish("SICK_Emu", localizeResult);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            });
        }

        private async Task PublishModuleInformation(RosSocket rosSocket)
        {
            await Task.Delay(1);
            await Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(10);
                    try
                    {
                        rosSocket.Publish("AGVC_Emu", module_info);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            });
        }

        private bool ComplexRobotControlCallBack(ComplexRobotControlCmdRequest req, out ComplexRobotControlCmdResponse res)
        {
            if (req.reqsrv == 2) //要求停止
            {
                EmuLog($"車載要求停止");
                RobotStopMRE.Reset();
            }
            else if (req.reqsrv == 0)
            {
                EmuLog($"車載要求速度恢復");
                RobotStopMRE.Set();
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

            Task.Factory.StartNew(async () =>
            {
                //模擬拍照
                await Task.Delay(1000);
                module_info.CSTReader.data = $"Try_ID_{DateTime.Now.ToString("yyyyMMddHHmmssffff")}";
                rosSocket.CallServiceAndWait<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_done_action", new CSTReaderCommandRequest
                {
                    model = tin.model,
                    command = "done",
                });

            });

            return true;
        }
        private void EmuLog(string msg)
        {
            LOG.WARN($"[車控模擬] {msg}");
        }

    }
}
