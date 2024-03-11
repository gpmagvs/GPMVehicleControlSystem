using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using WebSocketSharp;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace GPMVehicleControlSystem.Models.WebsocketMiddleware
{
    public class WebsocketAgent : AGVSystemCommonNet6.HttpTools.WebsocketServerMiddleware
    {
        public static WebsocketAgent Middleware = new WebsocketAgent();
        public override List<string> channelMaps { get; set; } = new List<string>()
        {
            "/ws"
        };

        protected override async Task CollectViewModelData()
        {
            CurrentViewModelDataOfAllChannel[channelMaps[0]] = new
            {
                ConnectionStatesVM = ViewModelFactory.GetConnectionStatesVM(),
                VMSStatesVM = ViewModelFactory.GetVMSStatesVM(),
                DIOTableVM = ViewModelFactory.GetDIOTableVM(),
                RDTestData = ViewModelFactory.GetRDTestData(),
            };
        }
    }
}
