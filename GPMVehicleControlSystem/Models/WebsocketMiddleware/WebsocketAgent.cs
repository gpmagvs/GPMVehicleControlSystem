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
                await SendMessagesAsync(webSocket, client_req);
            }
            else
            {
                _HttpContext.Response.StatusCode = 400;
            }
        }

        private static async Task SendMessagesAsync(WebSocket webSocket, WEBSOCKET_CLIENT_ACTION client_req)
        {
            var delay = TimeSpan.FromSeconds(0.1);

            while (webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(delay);
                try
                {
                  var viewmodel=   GetData(client_req);
                   await  webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewmodel))), WebSocketMessageType.Text, true, CancellationToken.None);
                
                }
                catch (WebSocketException)
                {
                    // 客戶端已斷開連線，停止傳送訊息
                    break;
                }
            }

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);

        }
        private static object GetData(WEBSOCKET_CLIENT_ACTION client_req)
        {
            object viewmodel = "";
            switch (client_req)
            {
                case WEBSOCKET_CLIENT_ACTION.GETConnectionStates:
                    viewmodel = ViewModelFactory.GetConnectionStatesVM();

                    break;
                case WEBSOCKET_CLIENT_ACTION.GETVMSStates:
                    viewmodel = ViewModelFactory.GetVMSStatesVM();
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETAGVCModuleInformation:
                    //viewmodel = AgvEntity.ModuleInformation;
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETDIOTable:
                    viewmodel = ViewModelFactory.GetDIOTableVM();
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETFORKTestState:
                    // viewmodel = ViewModelFactory.GetForkTestStateVM();
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETAGVSMSGIODATA:
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETSystemMessages:
                    viewmodel = ViewModelFactory.GetSystemMessagesVM();
                    break;
                case WEBSOCKET_CLIENT_ACTION.GETRDTestData:
                    viewmodel = ViewModelFactory.GetRDTestData();
                    break;
                default:
                    break;
            }
            return viewmodel;
        }
    }
}
