using ControllerService;
using NamedPipeWrapper;
using System;
using System.Windows.Forms;

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
            client.Stop();
            helper.Kill();
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
                case PipeCode.CODE_TOAST:
                    helper.SendToast(message.args[0], message.args[1]);
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            client.Stop();
            helper.Kill();
        }

        public void SendMessage(PipeMessage message)
        {
            client.PushMessage(message);
        }
    }
}
