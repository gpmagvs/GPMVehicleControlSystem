using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using WebSocketSharp;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace GPMVehicleControlSystem.Models.WebsocketMiddleware
{
    public class WebsocketAgent
    {
        private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

        public enum WEBSOCKET_CLIENT_ACTION
        {
            GETConnectionStates,
            GETVMSStates,
            GETAGVCModuleInformation,
            GETDIOTable,
            GETFORKTestState,
            GETAGVSMSGIODATA,
            GETRDTestData
        }
        private static List<MessageSender> _clients = new List<MessageSender>();
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        public static async Task ClientRequest(HttpContext _HttpContext, WEBSOCKET_CLIENT_ACTION client_req)
        {
            if (_HttpContext.WebSockets.IsWebSocketRequest)
            {
                semaphore.Wait();
                WebSocket webSocket = await _HttpContext.WebSockets.AcceptWebSocketAsync();
                MessageSender msg_sender = new MessageSender(webSocket, client_req);
                _clients.Add(msg_sender);
                msg_sender.OnViewDataFetching += () => { return GetData(client_req); };
                msg_sender.OnClientDisconnect += (sender, entity) =>
                {
                    semaphore.Wait();
                    _clients.Remove(entity);
                    semaphore.Release();
                };
                semaphore.Release();
                await msg_sender.ListenConnection();
                msg_sender.Dispose();
            }
            else
            {
                _HttpContext.Response.StatusCode = 400;
            }
        }
        public class MessageSender : IDisposable
        {
            public WebSocket client { get; private set; }
            public WEBSOCKET_CLIENT_ACTION client_req { get; }

            internal delegate object OnViewDataFetchDelate();
            internal OnViewDataFetchDelate OnViewDataFetching;
            internal event EventHandler<MessageSender> OnClientDisconnect;
            private bool disposedValue;

            public MessageSender(WebSocket client, WEBSOCKET_CLIENT_ACTION client_req)
            {
                this.client = client;
                this.client_req = client_req;
            }
            public async Task PublishMesgOut(byte[] dataByte)
            {
                await client.SendAsync(new ArraySegment<byte>(dataByte), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            public async Task ListenConnection()
            {
                byte[] buff = new byte[2];
                while (true)
                {
                    try
                    {
                        await Task.Delay(100);
                        WebSocketReceiveResult result = await client.ReceiveAsync(buff, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        break;
                    }
                }
                OnClientDisconnect?.Invoke(this, this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // TODO: 處置受控狀態 (受控物件)
                    }

                    // TODO: 釋出非受控資源 (非受控物件) 並覆寫完成項
                    // TODO: 將大型欄位設為 Null
                    OnViewDataFetching = null;
                    client = null;
                    disposedValue = true;
                }
            }
            public void Dispose()
            {
                // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        private static object ConnectionStatesVM;
        private static object VMSStatesVM;
        private static object DIOTableVM;
        private static object RDTestData;

        internal static void StartViewDataCollect()
        {
            Task.Run(async () =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (true)
                {
                    await Task.Delay(10);
                    try
                    {
                        var ConnectionStatesVM = ViewModelFactory.GetConnectionStatesVM();
                        var VMSStatesVM = ViewModelFactory.GetVMSStatesVM();
                        var DIOTableVM = ViewModelFactory.GetDIOTableVM();
                        var RDTestData = ViewModelFactory.GetRDTestData();

                        if (_clients.Count > 0)
                        {
                            await semaphore.WaitAsync(); // 改用非阻塞等待

                            var data_GETConnectionStates = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ConnectionStatesVM));
                            var data_GETVMSStates = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(VMSStatesVM));
                            var data_GETDIOTable = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(DIOTableVM));
                            var data_GETRDTestData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(RDTestData));

                            // 直接在 foreach 中進行篩選，減少臨時變量的創建
                            foreach (var client in _clients.Where(cl => cl.client_req == WEBSOCKET_CLIENT_ACTION.GETConnectionStates))
                            {
                                await client.PublishMesgOut(data_GETConnectionStates);
                            }
                            foreach (var client in _clients.Where(cl => cl.client_req == WEBSOCKET_CLIENT_ACTION.GETVMSStates))
                            {
                                await client.PublishMesgOut(data_GETVMSStates);
                            }
                            foreach (var client in _clients.Where(cl => cl.client_req == WEBSOCKET_CLIENT_ACTION.GETDIOTable))
                            {
                                await client.PublishMesgOut(data_GETDIOTable);
                            }
                            foreach (var client in _clients.Where(cl => cl.client_req == WEBSOCKET_CLIENT_ACTION.GETRDTestData))
                            {
                                await client.PublishMesgOut(data_GETRDTestData);
                            }
                            semaphore.Release();
                        };

                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                    finally
                    {
                        //Console.WriteLine(stopwatch.Elapsed);
                        //stopwatch.Restart();
                    }
                }
            });
        }

        private static object GetData(WEBSOCKET_CLIENT_ACTION client_req)
        {
            object viewmodel = "";
            switch (client_req)
            {
                case WEBSOCKET_CLIENT_ACTION.GETConnectionStates:
                    viewmodel = ConnectionStatesVM;
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETVMSStates:
                    viewmodel = VMSStatesVM;
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETAGVCModuleInformation:
                    //viewmodel = AgvEntity.ModuleInformation;
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETDIOTable:
                    viewmodel = DIOTableVM;
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETFORKTestState:
                    // viewmodel Factory.GetForkTestStateVM();
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETAGVSMSGIODATA:
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETRDTestData:
                    viewmodel = RDTestData;
                    break;
                default:
                    break;
            }
            return viewmodel;
        }
    }
}
