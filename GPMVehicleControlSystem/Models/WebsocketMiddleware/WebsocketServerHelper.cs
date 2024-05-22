using AGVSystemCommonNet6.HttpTools;

namespace GPMVehicleControlSystem.Models.WebsocketMiddleware
{
    public static class WebsocketServerHelper
    {
        public static WebsocketServerMiddleware Middleware = new WebsocketAgent(200);
    }
}
