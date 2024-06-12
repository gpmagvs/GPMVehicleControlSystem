using Microsoft.AspNetCore.SignalR;

namespace GPMVehicleControlSystem.Service
{
    public class FrontendHub : Hub
    {
        ILogger<FrontendHub> logger;
        public FrontendHub(ILogger<FrontendHub> _logger)
        {
            logger = _logger;
        }

        public override Task OnConnectedAsync()
        {
            logger.LogInformation($"Hub Client Connected");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            logger.LogInformation($"Hub Client Disconnected");
            return base.OnDisconnectedAsync(exception);
        }
    }
}
