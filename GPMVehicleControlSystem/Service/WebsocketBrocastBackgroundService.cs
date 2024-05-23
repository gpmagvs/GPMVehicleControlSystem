
using GPMVehicleControlSystem.ViewModels;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace GPMVehicleControlSystem.Service
{
    public class WebsocketBrocastBackgroundService : BackgroundService
    {

        private static ConcurrentDictionary<string, WebSocket> clients = new ConcurrentDictionary<string, WebSocket>();
        public static void ConnectIn(string user_id, WebSocket clientWs)
        {
            clients.TryAdd(user_id, clientWs);
            PrintOnlineClientNum();
        }

        internal static void HandleClientDisconnect(string user_id)
        {
            clients.TryRemove(user_id, out WebSocket clientWs);
            clientWs?.Dispose();
            PrintOnlineClientNum();
        }
        private static void PrintOnlineClientNum()
        {
            Console.WriteLine($"線上人數 = {clients.Count()}");
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(100);
                try
                {
                    var _ws_data_store = new Dictionary<string, object>();
                    _ws_data_store["ConnectionStatesVM"] = ViewModelFactory.GetConnectionStatesVM();
                    _ws_data_store["VMSStatesVM"] = ViewModelFactory.GetVMSStatesVM();
                    _ws_data_store["DIOTableVM"] = ViewModelFactory.GetDIOTableVM();
                    _ws_data_store["RDTestData"] = ViewModelFactory.GetRDTestData();
                    var dataBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_ws_data_store));
                    ArraySegment<byte> buffer = new ArraySegment<byte>(dataBytes);
                    var tasks = new List<Task>();
                    foreach (var _client in clients)
                    {
                        if (_client.Value.State == WebSocketState.Open)
                        {
                            //await _client.Value.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                            tasks.Add(_client.Value.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None));
                        }
                    }
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("CollectViewModelData Error" + ex.ToString());
                }
            }
        }
    }
}
