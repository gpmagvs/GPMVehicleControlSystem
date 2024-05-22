using AGVSystemCommonNet6.HttpTools;
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
        public WebsocketAgent(int duration) : base(duration)
        {
        }
        public override List<string> channelMaps { get; set; } = new List<string>()
        {
            "/ws"
        };
        public override void Initialize()
        {
            ClientsOfAllChannel = channelMaps.ToDictionary(str => str, str => new ConcurrentDictionary<string, clsWebsocktClientHandler>());
            CurrentViewModelDataOfAllChannel = channelMaps.ToDictionary(str => str, str => new object());
            CurrentViewModelDataOfAllChannel[channelMaps[0]] = new Dictionary<string, object>()
            {
                {"ConnectionStatesVM",new object() },
                {"VMSStatesVM",new object() },
                {"DIOTableVM",new object() },
                {"RDTestData",new object() },
            };
            StartCollectViewModelDataAndPublishOutAsync();
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
                Console.WriteLine("CollectViewModelData Error" + ex.ToString());
                if (CurrentViewModelDataOfAllChannel.TryGetValue(channelMaps[0], out var val))
                {
                    CurrentViewModelDataOfAllChannel[channelMaps[0]] = null;
                }
            }
        }
    }
}
