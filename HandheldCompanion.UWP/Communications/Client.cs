using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Principal;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Media.Protection.PlayReady;
using Windows.Security.Authentication.Web;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace HandheldCompanion.UWP.Communications
{
    class Client
    {
        public const string PIPE_NAME = "HandheldCompanion";

        public static Client Instance => _instance ?? (_instance = new Client());

        public event EventHandler<string> MessageReceivedEvent;
        public event EventHandler ClosedOrFailedEvent;

        public bool IsConnected => _client.IsConnected;
        public JsonRpc Rpc { get; private set; }

        private static Client _instance;
        private NamedPipeClientStream _client;

        private Client()
        {
            Connect();
        }

        private void Connect()
        {
            Rpc = new JsonRpc(new NamedPipeClientStream(".", $"LOCAL\\{PIPE_NAME}",
                PipeDirection.InOut, PipeOptions.Asynchronous));
        }
    }
}
