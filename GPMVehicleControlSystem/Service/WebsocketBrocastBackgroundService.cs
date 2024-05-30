
using GPMVehicleControlSystem.ViewModels;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace GPMVehicleControlSystem.Service
{
    public class WebsocketBrocastBackgroundService : BackgroundService
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(5); // 设置并发限制

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
        private async Task<List<byte[]>> CreateChunkData(byte[] datPublishOut)
        {
            int offset = 0;
            int chunkSize = 16384;
            List<byte[]> chunks = new List<byte[]>();
            var dataLen = datPublishOut.Length;
            while (offset < datPublishOut.Length)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(0.1));
                try
                {
                    byte[] data = new byte[chunkSize];
                    int remainingBytes = dataLen - offset;
                    int bytesToSend = Math.Min(remainingBytes, chunkSize);
                    byte[] chunk = new byte[bytesToSend];
                    Array.Copy(datPublishOut, offset, chunk, 0, bytesToSend);
                    offset += bytesToSend;
                    chunks.Add(chunk);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return chunks;

        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(180);
                try
                {
                    var _ws_data_store = new Dictionary<string, object>();
                    _ws_data_store["ConnectionStatesVM"] = ViewModelFactory.GetConnectionStatesVM();
                    _ws_data_store["VMSStatesVM"] = ViewModelFactory.GetVMSStatesVM();
                    _ws_data_store["DIOTableVM"] = ViewModelFactory.GetDIOTableVM();
                    _ws_data_store["RDTestData"] = ViewModelFactory.GetRDTestData();
                    var dataBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_ws_data_store));
                    ArraySegment<byte> buffer = new ArraySegment<byte>(dataBytes);
                    List<byte[]> chunks = await CreateChunkData(dataBytes);
                    var tasks = new List<Task>();
                    foreach (var _client in clients)
                    {
                        if (_client.Value.State == WebSocketState.Open)
                        {
                            tasks.Add(SendDataWithSemaphore(_client.Value, chunks));
                        }
                        else
                        {
                            _client.Value.Dispose();
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

        private async Task SendDataWithSemaphore(WebSocket client, List<byte[]> chunks)
        {
            await _semaphore.WaitAsync();
            try
            {
                await SendData(client, chunks);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        async Task SendData(WebSocket ws, List<byte[]> _chunks)
        {
            try
            {
                for (int i = 0; i < _chunks.Count; i++)
                {
                    var chunk = _chunks[i];
                    await ws.SendAsync(new ArraySegment<byte>(chunk), WebSocketMessageType.Text, i == _chunks.Count - 1, CancellationToken.None);
                }
            }
            catch (Exception)
            {
                ws.Dispose();
            }
        }
    }
}
