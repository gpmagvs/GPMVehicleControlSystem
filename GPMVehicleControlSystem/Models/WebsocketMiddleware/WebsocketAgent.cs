using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.ViewModels;
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

        public static async Task ClientRequest(HttpContext _HttpContext, WEBSOCKET_CLIENT_ACTION client_req)
        {
            if (_HttpContext.WebSockets.IsWebSocketRequest)
            {
                WebSocket webSocket = await _HttpContext.WebSockets.AcceptWebSocketAsync();
                MessageSender msg_sender = new MessageSender(webSocket, client_req);
                msg_sender.OnViewDataFetching += () => { return GetData(client_req); };
                await msg_sender.SendMessage();
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
            private bool disposedValue;

            public MessageSender(WebSocket client, WEBSOCKET_CLIENT_ACTION client_req)
            {
                this.client = client;
                this.client_req = client_req;
            }

            public async Task SendMessage()
            {
                var buff = new ArraySegment<byte>(new byte[10]);
                bool closeFlag = false;
                _ = Task.Factory.StartNew(async () =>
                {
                    while (!closeFlag)
                    {
                        await Task.Delay(100);
                        var data = OnViewDataFetching();
                        if (data != null)
                        {
                            try
                            {

                                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data))), WebSocketMessageType.Text, true, CancellationToken.None);
                                data = null;
                            }
                            catch (Exception)
                            {
                                return;
                            }
                        }
                    }
                });

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
                closeFlag = true;
                client.Dispose();
                GC.Collect();

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

        internal static async Task StartViewDataCollect()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(100);
                    try
                    {
                        ConnectionStatesVM = ViewModelFactory.GetConnectionStatesVM();
                        VMSStatesVM = ViewModelFactory.GetVMSStatesVM();
                        DIOTableVM = ViewModelFactory.GetDIOTableVM();
                        RDTestData = ViewModelFactory.GetRDTestData();
                    }
                    catch (Exception ex)
                    {
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
