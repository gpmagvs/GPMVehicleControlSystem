using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Log;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        public class clsDIO_STATUS
        {
            public bool Load_Request { get; set; } = false;
            public bool Unload_Request { get; set; } = false;
            public bool PortExist { get; set; } = false;
            public bool Up_Pose { get; set; } = false;
            public bool Down_Pose { get; set; } = false;
            public bool EQ_Status_Run { get; set; } = false;
        }
        private Socket CIMSocket;
        private Socket AGVSDataBaseSocket;
        public Dictionary<int, clsDIO_STATUS> EQDIOStates = new Dictionary<int, clsDIO_STATUS>();
        internal async Task<bool> CIMConnectionInitialize()
        {
            if (!Parameters.CIMConn)
                return false;
            return await Task.Factory.StartNew(() =>
            {
                try
                {
                    CIMSocket = CreateSocketCIM();
                    StartFetchEQDIOStatusFromCIM();
                    return true;
                }
                catch (Exception ex)
                {
                    LOG.ERROR(ex);
                    CIMSocket = null;
                    CreateSocketCIM();
                    return false;
                }
            });
        }

        private Socket CreateSocketCIM()
        {
            var cimIP = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.AGVS].IP;
            try
            {
                var CIMSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                CIMSocket.Connect(cimIP, 6100);
                LOG.INFO($"成功與CIM建立Socket通訊({cimIP}:6100) !!!", color: ConsoleColor.Green);
                return CIMSocket;
            }
            catch (Exception ex)
            {
                Task.Factory.StartNew(async () =>
                {
                    LOG.ERROR($"無法與CIM建立Socket通訊({cimIP}:6100)...Rerty");
                    await Task.Delay(10000);
                    CIMSocket = CreateSocketCIM();

                }
                );
                return null;
            }
        }

        private Socket CreateAGVsDataBaseSocket()
        {
            var cimIP = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.AGVS].IP;
            try
            {
                var _CIMSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _CIMSocket.Connect(cimIP, 5200);
                LOG.INFO($"成功與CIM建立Socket通訊({cimIP}:5200) !!!", color: ConsoleColor.Green);
                return _CIMSocket;
            }
            catch (Exception ex)
            {
                LOG.ERROR($"無法與CIM建立Socket通訊({cimIP}:5200)...Rerty");
                return null;
            }
        }
        private void StartFetchEQDIOStatusFromCIM()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    Thread.Sleep(10);
                    if (CIMSocket == null)
                        continue;
                    try
                    {
                        CIMSocket.Send(Encoding.ASCII.GetBytes("GETDIOSTATUS"));
                        CancellationTokenSource cts = new CancellationTokenSource();
                        cts.CancelAfter(TimeSpan.FromSeconds(10));
                        bool isJsonContent = false;
                        string jsonStr = string.Empty;
                        while (!isJsonContent)
                        {
                            while (CIMSocket.Available == 0)
                            {
                                Thread.Sleep(1);
                                if (cts.IsCancellationRequested)
                                    throw new Exception();
                            }
                            byte[] buffer = new byte[CIMSocket.Available];
                            CIMSocket.Receive(buffer, buffer.Length, SocketFlags.None);
                            string str = Encoding.ASCII.GetString(buffer);
                            jsonStr += str;
                            try
                            {
                                EQDIOStates = JsonConvert.DeserializeObject<Dictionary<int, clsDIO_STATUS>>(jsonStr);
                                isJsonContent = true;
                            }
                            catch (Exception)
                            {
                                isJsonContent = false;
                                EQDIOStates = null;
                            }
                        }
                        //LOG.INFO(EQDIOStates.ToJson());
                    }
                    catch (Exception ex)
                    {
                        EQDIOStates = null;
                        if (CIMSocket != null)
                        {
                            try
                            {
                                CIMSocket.Dispose();
                            }
                            finally
                            {
                                CIMSocket = null;
                                Task.Factory.StartNew(async () =>
                                {
                                    await Task.Delay(100);
                                    CIMSocket = CreateSocketCIM();
                                });
                            }
                        }
                    }

                }
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="taskName"></param>
        /// <returns></returns>
        internal async Task<TransferData> QueryTaskInfoFromCIM(string taskName)
        {
            LOG.WARN($"Try Get Task-{taskName} transfer info from CIM");
            TransferData transferData = new TransferData();
            var socket = CreateAGVsDataBaseSocket();
            if (socket != null)
            {
                string cmd = "QueryTaskInfo:" + taskName;
                LOG.INFO($"Send {cmd}");
                int i = socket.Send(Encoding.ASCII.GetBytes(cmd), SocketFlags.None);
                LOG.INFO($"Send {cmd} _{i}");
                bool isJsonDataRecieved = false;
                string stringRev = "";
                CancellationTokenSource cancelReq = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                while (!isJsonDataRecieved)
                {
                    await Task.Delay(1);
                    if (cancelReq.IsCancellationRequested)
                    {
                        LOG.ERROR($"TIMEOUT=> Download Transfer Infomation From CIM TIMEOUT!");
                        break;
                    }
                    int ava = socket.Available;
                    LOG.TRACE($"Available :{ava}");
                    if (ava > 0)
                    {
                        byte[] buffer = new byte[ava];
                        socket.Receive(buffer, ava, SocketFlags.None);
                        stringRev += Encoding.ASCII.GetString(buffer, 0, ava);
                        LOG.TRACE($"{stringRev}");
                        if (stringRev.ToLower().Contains("error:"))
                        {
                            break;
                        }
                        else
                        {
                            try
                            {
                                transferData = JsonConvert.DeserializeObject<TransferData>(stringRev);
                                isJsonDataRecieved = transferData != null && stringRev.Contains(taskName);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }

                    }
                }
                socket.Close();
                return transferData;
            }
            else
            {
                return transferData;
            }
        }


    }

    public class TransferData
    {
        public string Name { get; set; }
        public int Status { get; set; }
        public DateTime Receive_Time { get; set; }
        public int FromStationId { get; set; }
        public int ToStationId { get; set; }
        public string FromStation { get; set; }
        public string ToStation { get; set; }
        public string FromStationName { get; set; }
        public string ToStationName { get; set; }
        public string ActionType { get; set; }
        public int AGVID { get; set; }
        public string CSTID { get; set; }
        public int Priority { get; set; }
        public int RepeatTime { get; set; }
        public int ExeVehicleID { get; set; }
        public DateTime StartTime { get; set; }
        public double Distance { get; set; }
        public DateTime? AcquireTime { get; set; }
        public DateTime? DepositTime { get; set; }
        public string AssignUserName { get; set; }
        public int CSTType { get; set; }
        public int FromStationPortNo { get; set; }
        public int ToStationPortNo { get; set; }
        public int ExeVehiclePos { get; set; }
    }

}
