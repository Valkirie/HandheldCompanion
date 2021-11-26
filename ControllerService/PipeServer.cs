using Microsoft.Extensions.Logging;
using NamedPipeWrapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    [Serializable]
    public class PipeMessage
    {
        public PipeCode Code;
        public Dictionary<string, string> args = new Dictionary<string, string>();
    }

    public enum PipeCode
    {
        SERVER_CONNECTED = 0,               // Sent to client during initialization
                                            // args: ...

        CLIENT_PROFILE = 1,                 // Sent to server each time a new process is in the foreground. Used to switch profiles
                                            // args: process id, process path

        SERVER_TOAST = 2,                   // Sent to client to display toast notification.
                                            // args: title, message

        CLIENT_CURSORUP = 3,                // Sent to server when mouse click is up
                                            // args: cursor x, cursor Y

        CLIENT_CURSORDOWN = 4,              // Sent to server when mouse click is down
                                            // args: cursor x, cursor Y

        CLIENT_CURSORMOVE = 5,              // Sent to server when mouse is moving
                                            // args: cursor x, cursor Y

        SERVER_SETTINGS = 6,                // Sent to client during initialization
                                            // args: ...

        SERVER_CONTROLLER = 7,              // Sent to client during initialization
                                            // args: ...

        CLIENT_SETTINGS = 8,                // Sent to server to update settings
                                            // args: ...

        CLIENT_HIDDER_REG = 9,              // Sent to server to register applications
                                            // args: ...

        CLIENT_HIDDER_UNREG = 10,           // Sent to server to unregister applications
                                            // args: ...

        CLIENT_SIZE_DETAILS = 11,           // Sent to server to update screen details
                                            // args: width, height
    }

    public class PipeServer
    {
        private NamedPipeServer<PipeMessage> server;
        private readonly ILogger<ControllerService> logger;
        private readonly ControllerService service;

        private ConcurrentQueue<PipeMessage> m_queue;
        private Timer m_timer;

        public bool connected;

        public PipeServer(string pipeName, ControllerService service, ILogger<ControllerService> logger)
        {
            this.service = service;
            this.logger = logger;

            m_queue = new ConcurrentQueue<PipeMessage>();

            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            PipeSecurity ps = new PipeSecurity();
            System.Security.Principal.SecurityIdentifier sid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null);
            PipeAccessRule par = new PipeAccessRule(sid, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            ps.AddAccessRule(par);

            server = new NamedPipeServer<PipeMessage>(pipeName, ps);
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientMessage += OnClientMessage;
            server.Error += OnError;
        }

        public void Start()
        {
            if (server == null)
                return;

            server.Start();
            logger.LogInformation($"Pipe Server has started");
        }

        public void Stop()
        {
            if (server == null)
                return;

            server = null;
            logger.LogInformation($"Pipe Server has halted");
        }

        private void OnClientConnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            connected = true;
            logger.LogInformation("Client {0} is now connected!", connection.Id);

            SendMessage(new PipeMessage()
            {
                Code = PipeCode.SERVER_CONNECTED
            });

            SendMessage(new PipeMessage()
            {
                Code = PipeCode.SERVER_CONTROLLER,
                args = service.PhysicalController.ToArgs()
            });

            SendMessage(new PipeMessage()
            {
                Code = PipeCode.SERVER_SETTINGS,
                args = service.GetSettings()
            });
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            connected = false;
            logger.LogInformation("Client {0} disconnected", connection.Id);
            
            service.PhysicalController.touch.OnMouseUp(-1, -1, 1048576 /* MouseButtons.Left */);
        }

        private void OnClientMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            logger.LogDebug("Client {0} opcode: {1} says: {2}", connection.Id, message.Code, string.Join(" ", message.args));

            switch (message.Code)
            {
                case PipeCode.CLIENT_PROFILE:
                    service.UpdateProfile(message.args);
                    break;

                case PipeCode.CLIENT_CURSORUP:
                    service.PhysicalController.touch.OnMouseUp(short.Parse(message.args["X"]), short.Parse(message.args["Y"]), int.Parse(message.args["Button"]));
                    break;

                case PipeCode.CLIENT_CURSORDOWN:
                    service.PhysicalController.touch.OnMouseDown(short.Parse(message.args["X"]), short.Parse(message.args["Y"]), int.Parse(message.args["Button"]));
                    break;

                case PipeCode.CLIENT_CURSORMOVE:
                    service.PhysicalController.touch.OnMouseMove(short.Parse(message.args["X"]), short.Parse(message.args["Y"]), int.Parse(message.args["Button"]));
                    break;

                case PipeCode.CLIENT_SIZE_DETAILS:
                    service.PhysicalController.touch.UpdateRatio(float.Parse(message.args["Bounds.Width"]), float.Parse(message.args["Bounds.Height"]));
                    break;

                case PipeCode.CLIENT_SETTINGS:
                    service.UpdateSettings(message.args);
                    break;

                case PipeCode.CLIENT_HIDDER_REG:
                    service.Hidder.RegisterApplication(message.args["path"]);
                    break;

                case PipeCode.CLIENT_HIDDER_UNREG:
                    service.Hidder.UnregisterApplication(message.args["path"]);
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            logger.LogError("PipeServer failed. {0}", exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (!connected)
            {
                m_queue.Enqueue(message);
                m_timer.Start();
                return;
            }

            server.PushMessage(message);
        }

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!connected)
                return;

            foreach (PipeMessage m in m_queue)
                server.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
