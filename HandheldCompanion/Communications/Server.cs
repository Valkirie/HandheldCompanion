using HandheldCompanion.Shared;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Communications
{
    class Server
    {
        public const string PIPE_NAME = "HandheldCompanion";

        public JsonRpc Rpc { get; private set; }

        private readonly NamedPipeServerStream _server;
        private readonly PipeSecurity _security;
        private readonly string _pipeName;

        public Server(string packageSid)
        {
            _pipeName = $"Sessions\\{Process.GetCurrentProcess().SessionId}\\AppContainerNamedObjects\\{packageSid}\\{PIPE_NAME}";
            LogManager.LogInformation("Server: Pipe name: {0}", _pipeName);

            _security = GetPipeSecurity(packageSid);

            _server = NamedPipeServerStreamAcl.Create(
                _pipeName,
                PipeDirection.InOut, 1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous, 128, 128, _security);
            Rpc = new JsonRpc(_server);
        }

        private static PipeSecurity GetPipeSecurity(string packageSid)
        {
            var ps = new PipeSecurity();
            var clientRule = new PipeAccessRule(
                packageSid,
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow);
            var ownerRule = new PipeAccessRule(
                WindowsIdentity.GetCurrent().User ?? new SecurityIdentifier("S-1-1-0"),
                PipeAccessRights.FullControl,
                AccessControlType.Allow);
            ps.AddAccessRule(clientRule);
            ps.AddAccessRule(ownerRule);
            return ps;
        }

    }
}
