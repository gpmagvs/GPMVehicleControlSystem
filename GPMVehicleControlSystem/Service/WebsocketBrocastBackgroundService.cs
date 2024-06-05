
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace GPMVehicleControlSystem.Service
{
    public class WebsocketBrocastBackgroundService : BackgroundService
    {
        //use hub to broadcast
        private readonly IHubContext<FrontendHub> _hubContext;
        public WebsocketBrocastBackgroundService(IHubContext<FrontendHub> hubContext)
        {
            _hubContext = hubContext;
        }

        private List<byte[]> CreateChunkData(byte[] datPublishOut)
        {
            int offset = 0;
            int chunkSize = 16384;
            List<byte[]> chunks = new List<byte[]>();
            var dataLen = datPublishOut.Length;

            while (offset < datPublishOut.Length)
            {
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
                await Task.Delay(180, stoppingToken);
                try
                {
                    var _ws_data_store = new Dictionary<string, object>();
                    _ws_data_store["ConnectionStatesVM"] = ViewModelFactory.GetConnectionStatesVM();
                    _ws_data_store["VMSStatesVM"] = ViewModelFactory.GetVMSStatesVM();
                    _ws_data_store["DIOTableVM"] = ViewModelFactory.GetDIOTableVM();
                    _ws_data_store["RDTestData"] = ViewModelFactory.GetRDTestData();
                    await _hubContext.Clients.All.SendAsync("ReceiveData", "VMS", _ws_data_store);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("CollectViewModelData Error" + ex.ToString());
                }
            }
        }

    }
}
