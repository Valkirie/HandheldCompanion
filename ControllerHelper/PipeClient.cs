using ControllerService;
using NamedPipeWrapper;
using SharpDX.XInput;
using System;

namespace ControllerHelper
{
    public class PipeClient
    {
        private readonly NamedPipeClient<PipeMessage> client;
        private readonly ControllerHelper helper;

        public PipeClient(string pipeName, ControllerHelper helper)
        {
            this.helper = helper;

            client = new NamedPipeClient<PipeMessage>(pipeName);
            client.AutoReconnect = true;

            client.Disconnected += OnClientDisconnected;
            client.ServerMessage += OnServerMessage;
            client.Error += OnError;
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            helper.UpdateStatus(false);
        }

        public void Start()
        {
            if (client == null)
                return;

            client.Start();
        }

        public void Stop()
        {
            if (client == null)
                return;

            client.Stop();
        }

        private void OnServerMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            switch (message.Code)
            {
                case PipeCode.SERVER_CONNECTED:
                    helper.UpdateStatus(true);
                    break;

                case PipeCode.SERVER_TOAST:
                    Utils.SendToast(message.args["title"], message.args["content"]);
                    break;

                case PipeCode.SERVER_CONTROLLER:
                    helper.UpdateController(message.args);
                    break;

                case PipeCode.SERVER_SETTINGS:
                    helper.UpdateSettings(message.args);
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            // logger.LogError("PipClient failed. {0}", exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (client == null)
                return;

            client.PushMessage(message);
        }
    }
}
