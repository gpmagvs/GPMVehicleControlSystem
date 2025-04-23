using AGVSystemCommonNet6.AGVDispatch;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace GPMVehicleControlSystem.Service
{
    public class VehicleServiceAggregator
    {


        public readonly ILogger<VehicleFactoryService> logger;
        public readonly ILogger<Vehicle> vehicleLogger;
        public readonly ILogger<clsAGVSConnection> agvsLogger;
        public readonly IHubContext<FrontendHub> hubContext;
        public readonly IMemoryCache memoryCache;


        public VehicleServiceAggregator(ILogger<VehicleFactoryService> _logger,
                                                     ILogger<Vehicle> _vehicleLogger,
                                                     ILogger<clsAGVSConnection> _agvsLogger,
                                                     IHubContext<FrontendHub> _hubContext,
                                                     IMemoryCache _memoryCache)
        {
            logger = _logger;
            vehicleLogger = _vehicleLogger;
            agvsLogger = _agvsLogger;
            hubContext = _hubContext;
            memoryCache = _memoryCache;
        }
    }
}
