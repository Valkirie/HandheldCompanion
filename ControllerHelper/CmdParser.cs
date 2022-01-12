using CommandLine;
using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using static ControllerHelper.Options;

namespace ControllerHelper
{
    public class CmdParser
    {
        private readonly PipeClient client;
        private readonly ControllerHelper helper;
        private readonly ILogger logger;
        private string[] args;

        public CmdParser(PipeClient client, ControllerHelper helper, ILogger logger)
        {
            this.client = client;
            this.helper = helper;
            this.logger = logger;
        }

        public void ParseArgs(string[] args)
        {
            this.args = args;

            if (args.Length == 0)
                return;

            logger?.LogInformation("Parsing command: {0}", String.Join(' ', args));

            Parser.Default.ParseArguments<ProfileOption, ProfileService>(args)
                .MapResult(
                    (ProfileOption option) => CmdProfile(option),
                    (ProfileService option) => CmdService(option),
                    errors => CmdError(errors));
        }

        #region cmd

        private bool CmdProfile(ProfileOption option)
        {
            string ProcessExec = Path.GetFileNameWithoutExtension(option.exe);

            Profile profile = new Profile(ProcessExec, option.exe);

            if (helper.ProfileManager.profiles.ContainsKey(ProcessExec))
                profile = helper.ProfileManager.profiles[ProcessExec];

            profile.fullpath = option.exe;

            switch (option.mode)
            {
                case ProfileOptionMode.neo:
                    profile.whitelisted = true;
                    break;
                case ProfileOptionMode.ds4:
                    profile.whitelisted = false;
                    helper.UpdateHID(HIDmode.DualShock4Controller);
                    break;
                case ProfileOptionMode.xbox:
                    profile.whitelisted = false;
                    helper.UpdateHID(HIDmode.Xbox360Controller);
                    break;
                default:
                    return false;
            }

            helper.ProfileManager.UpdateProfile(profile);
            helper.ProfileManager.SerializeProfile(profile);
            return true;
        }

        private bool CmdService(ProfileService option)
        {
            switch (option.action)
            {
                case ProfileServiceAction.create:
                    helper.ServiceManager.CreateService(helper.CurrentPathService);
                    break;
                case ProfileServiceAction.delete:
                    helper.ServiceManager.DeleteService();
                    break;
                case ProfileServiceAction.start:
                    helper.ServiceManager.StartService();
                    break;
                case ProfileServiceAction.stop:
                    helper.ServiceManager.StopService();
                    break;
                case ProfileServiceAction.install:
                    helper.ServiceManager.CreateService(helper.CurrentPathService);
                    helper.ServiceManager.SetStartType(System.ServiceProcess.ServiceStartMode.Automatic);
                    helper.ServiceManager.StartService();
                    break;
                case ProfileServiceAction.uninstall:
                    client.SendMessage(new PipeShutdown());
                    helper.ServiceManager.StopService();
                    helper.ServiceManager.DeleteService();
                    helper.ForceExit();
                    break;
                default:
                    return false;
            }

            return true;
        }

        private bool CmdError(IEnumerable<Error> errors)
        {
            logger?.LogError("Couldn't parse command. Errors: {0}", String.Join(", ", errors));
            return true;
        }

        #endregion
    }
}
