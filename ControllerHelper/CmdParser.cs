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

            Parser.Default.ParseArguments<ProfileOption, ProfileService, DeviceOption>(args)
                .MapResult(
                    (ProfileOption option) => CmdProfile(option),
                    (ProfileService option) => CmdService(option),
                    (DeviceOption option) => CmdDevice(option),
                    errors => CmdError(errors));
        }

        #region cmd

        private bool CmdDevice(DeviceOption option)
        {
            helper.UpdateHID(option.mode);
            helper.UpdateCloak(option.cloak);
            return true;
        }

        private bool CmdProfile(ProfileOption option)
        {
            string ProcessExec = Path.GetFileNameWithoutExtension(option.exe);

            Profile profile = new Profile(ProcessExec, option.exe);

            if (helper.ProfileManager.profiles.ContainsKey(ProcessExec))
                profile = helper.ProfileManager.profiles[ProcessExec];

            profile.fullpath = option.exe;
            profile.umc_enabled = option.umc;
            profile.use_wrapper = option.wrapper;
            profile.whitelisted = option.whitelist;
            profile.umc_trigger = (GamepadButtonFlags)option.trigger;

            profile.umc_input = (Input)option.input;
            profile.umc_output = (Output)option.output;

            profile.steering = option.steering;
            profile.inverthorizontal = option.inverthorizontal;
            profile.invertvertical = option.invertvertical;

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
