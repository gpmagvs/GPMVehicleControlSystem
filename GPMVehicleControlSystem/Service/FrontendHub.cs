using GPMVehicleControlSystem.Tools.DiskUsage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace GPMVehicleControlSystem.Service
{
    public class FrontendHub : Hub
    {
        ILogger<FrontendHub> logger;
        IMemoryCache _memoryCache;
        public FrontendHub(ILogger<FrontendHub> _logger, IMemoryCache memoryCache)
        {
            logger = _logger;
            _memoryCache = memoryCache;
        }

        public override Task OnConnectedAsync()
        {
            logger.LogInformation($"Hub Client Connected");
            DiskUsageState _diskStatus = _memoryCache.Get<DiskUsageState>("DiskStatus");
            Clients.Caller.SendAsync("DiskStatus", _diskStatus);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            logger.LogInformation($"Hub Client Disconnected");
            return base.OnDisconnectedAsync(exception);
        }
    }
}
