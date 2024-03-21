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
        public override void Initialize()
        {
            base.Initialize();
            CurrentViewModelDataOfAllChannel[channelMaps[0]] = new Dictionary<string, object>()
            {
                {"ConnectionStatesVM",new object() },
                {"VMSStatesVM",new object() },
                {"DIOTableVM",new object() },
                {"RDTestData",new object() },
            };
            Console.WriteLine("Websocket Agent init done.");
        }
        protected override async Task CollectViewModelData()
        {
            try
            {
                var _ws_data_store = CurrentViewModelDataOfAllChannel[channelMaps[0]] as Dictionary<string, object>;
                _ws_data_store["ConnectionStatesVM"] = ViewModelFactory.GetConnectionStatesVM();
                _ws_data_store["VMSStatesVM"] = ViewModelFactory.GetVMSStatesVM();
                _ws_data_store["DIOTableVM"] = ViewModelFactory.GetDIOTableVM();
                _ws_data_store["RDTestData"] = ViewModelFactory.GetRDTestData();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                return;
            }
        }
    }
}
