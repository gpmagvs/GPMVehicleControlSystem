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
        public Dictionary<int, clsDIO_STATUS> EQDIOStates = new Dictionary<int, clsDIO_STATUS>();
        internal async Task<bool> CIMConnectionInitialize()
        {
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
            var cimIP = Parameters.Connections["AGVS"].IP;
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
    }
}
