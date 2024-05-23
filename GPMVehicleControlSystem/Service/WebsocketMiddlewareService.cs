using System.Net.WebSockets;

namespace GPMVehicleControlSystem.Service
{
    public class WebsocketMiddlewareService
    {

        public async Task ClientConnect(string user_id, WebSocket clientWs)
        {
            var taskCTS = new TaskCompletionSource<WebSocket>();
            WebsocketBrocastBackgroundService.ConnectIn(user_id, clientWs);

            try
            {
                _RecieveTskAsync();
            }
            catch (Exception)
            {
            }

            await taskCTS.Task;

            async Task _RecieveTskAsync()
            {
                byte[] buff = new byte[1024];
                try
                {
                    await clientWs.ReceiveAsync(new ArraySegment<byte>(buff), CancellationToken.None);
                    await _RecieveTskAsync();
                    await Task.Delay(100);
                }
                catch (Exception)
                {
                    await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    WebsocketBrocastBackgroundService.HandleClientDisconnect(user_id);
                    clientWs.Dispose();
                    taskCTS.SetCanceled();
                }

            }

        }
    }
}
