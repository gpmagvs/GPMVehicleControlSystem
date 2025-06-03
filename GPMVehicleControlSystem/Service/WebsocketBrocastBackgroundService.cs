
using GPMVehicleControlSystem.Tools.DiskUsage;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System.Net.WebSockets;

namespace GPMVehicleControlSystem.Service
{
    public class WebsocketBrocastBackgroundService : BackgroundService
    {
        //use hub to broadcast
        private readonly IHubContext<FrontendHub> _hubContext;
        IMemoryCache _memoryCache;
        public WebsocketBrocastBackgroundService(IHubContext<FrontendHub> hubContext, IMemoryCache memoryCache)
        {
            _hubContext = hubContext;
            _memoryCache = memoryCache;
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
            List<Task> _taskList = new List<Task>();
            _taskList.Add(Task.Run(async () =>
            {
                await SendOutVehicleData(stoppingToken);
            }));
            _taskList.Add(Task.Run(async () =>
            {
                await SendOutDIOStatus(stoppingToken);
            }));
            _taskList.Add(Task.Run(async () =>
            {
                await SendOutVDiskStatus(stoppingToken);
            }));

            await Task.WhenAll(_taskList);
        }

        private async Task SendOutVehicleData(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(250, stoppingToken);
                if (ViewModelFactory.VehicleInstanceCreateFailException != null)
                {
                    await _hubContext.Clients.All.SendAsync("VehicleError", ViewModelFactory.VehicleInstanceCreateFailException.Message);
                    continue;
                }
                try
                {
                    Dictionary<string, object> _ws_data_store = new Dictionary<string, object>();
                    ConnectionStateVM conn_data = ViewModelFactory.GetConnectionStatesVM();
                    AGVCStatusVM state_data = ViewModelFactory.GetVMSStatesVM();
                    object rd_data = ViewModelFactory.GetRDTestData();
                    _ws_data_store["ConnectionStatesVM"] = conn_data;
                    _ws_data_store["VMSStatesVM"] = state_data;
                    _ws_data_store["RDTestData"] = rd_data;
                    await _hubContext.Clients.All.SendAsync("ReceiveData", "VMS", _ws_data_store);

                    state_data.Dispose();
                    conn_data = null;
                    state_data = null;
                    rd_data = null;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("CollectViewModelData Error" + ex.ToString());
                }
            }
        }


        private async Task SendOutDIOStatus(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
                try
                {
                    DIOTableVM dio_data = ViewModelFactory.GetDIOTableVM();
                    await _hubContext.Clients.All.SendAsync("DIOStatus", dio_data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("CollectViewModelData Error" + ex.ToString());
                }
            }
        }

        private async Task SendOutVDiskStatus(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                try
                {
                    //_memoryCache.Set<DiskUsageState>("DiskStatus", homeDiskUsage);
                    DiskUsageState _diskStatus = _memoryCache.Get<DiskUsageState>("DiskStatus");
                    await _hubContext.Clients.All.SendAsync("DiskStatus", _diskStatus);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("CollectViewModelData Error" + ex.ToString());
                }
            }
        }
    }
}
