using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.ViewModels;
using Newtonsoft.Json;
using Polly;
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
            GETSystemMessages,
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
            }
            else
            {
                _HttpContext.Response.StatusCode = 400;
            }
        }
        public class MessageSender
        {
            public WebSocket client { get; }
            public WEBSOCKET_CLIENT_ACTION client_req { get; }

            internal delegate object OnViewDataFetchDelate();
            internal OnViewDataFetchDelate OnViewDataFetching;
            public MessageSender(WebSocket client, WEBSOCKET_CLIENT_ACTION client_req)
            {
                this.client = client;
                this.client_req = client_req;
            }

            public async Task SendMessage()
            {
                while (client.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    try
                    {
                        await Task.Delay(100);

                        _ = Task.Factory.StartNew(async () =>
                         {
                             var result = await client.ReceiveAsync(new ArraySegment<byte>(new byte[10]), CancellationToken.None);
                         });

                        if (OnViewDataFetching == null)
                            break;

                        var data = OnViewDataFetching();
                        if (data != null)
                        {
                            await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data))), WebSocketMessageType.Text, true, CancellationToken.None);
                            data = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        break;
                    }
                }
                Console.WriteLine($"Websocket channel {client_req}-closed");
                client.Dispose();

            }
        }

        private static object ConnectionStatesVM;
        private static object VMSStatesVM;
        private static object DIOTableVM;
        private static object SystemMessagesVM;
        private static object RDTestData;

        internal static async Task StartViewDataCollect()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(100);
                    ConnectionStatesVM = ViewModelFactory.GetConnectionStatesVM();
                    VMSStatesVM = ViewModelFactory.GetVMSStatesVM();
                    DIOTableVM = ViewModelFactory.GetDIOTableVM();
                    SystemMessagesVM = ViewModelFactory.GetSystemMessagesVM();
                    RDTestData = ViewModelFactory.GetRDTestData();
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
                case WEBSOCKET_CLIENT_ACTION.GETSystemMessages:
                    viewmodel = SystemMessagesVM;
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
