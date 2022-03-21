using CommandLine;
using ControllerCommon;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static HandheldCompanion.Options;

namespace HandheldCompanion
{
    public class CmdParser
    {
        private readonly PipeClient pipeClient;
        private readonly MainWindow mainWindow;

        private readonly ILogger microsoftLogger;

        public CmdParser(PipeClient pipeClient, MainWindow mainWindow, ILogger microsoftLogger)
        {
            this.pipeClient = pipeClient;
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;
        }

        public void ParseArgs(string[] args, bool init = false)
        {
            if (args.Length == 0)
                return;

            microsoftLogger?.LogInformation("Parsing command: {0}", String.Join(' ', args));

            Parser.Default.ParseArguments<ProfileOption, ProfileService, DeviceOption>(args)
                .MapResult(
                    (ProfileOption option) => CmdProfile(option, init),
                    (ProfileService option) => CmdService(option, init),
                    (DeviceOption option) => CmdDevice(option, init),
                    errors => CmdError(errors));
        }

        #region cmd

        private bool CmdDevice(DeviceOption option, bool init = false)
        {
            mainWindow.UpdateHID(option.mode);
            mainWindow.UpdateCloak(option.cloak);
            return true;
        }

        private bool CmdProfile(ProfileOption option, bool init = false)
        {
            Profile profile = new Profile(option.exe);

            // shall we check if profile already exists ?

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

            mainWindow.profileManager.UpdateOrCreateProfile(profile);
            mainWindow.profileManager.SerializeProfile(profile);

            return true;
        }

        private bool CmdService(ProfileService option, bool init = false)
        {
            switch (option.action)
            {
                case ProfileServiceAction.create:
                    mainWindow.serviceManager.CreateService(mainWindow.CurrentPathService);
                    break;
                case ProfileServiceAction.delete:
                    mainWindow.serviceManager.DeleteService();
                    break;
                case ProfileServiceAction.start:
                    mainWindow.serviceManager.StartServiceAsync();
                    break;
                case ProfileServiceAction.stop:
                    mainWindow.serviceManager.StopServiceAsync();
                    break;
                case ProfileServiceAction.install:
                    mainWindow.serviceManager.CreateService(mainWindow.CurrentPathService);
                    mainWindow.serviceManager.SetStartType(System.ServiceProcess.ServiceStartMode.Automatic);
                    mainWindow.serviceManager.StartServiceAsync();
                    break;
                case ProfileServiceAction.uninstall:
                    pipeClient.SendMessage(new PipeShutdown());
                    mainWindow.serviceManager.StopServiceAsync();
                    mainWindow.serviceManager.DeleteService();
                    if (init) Process.GetCurrentProcess().Kill();
                    break;
                default:
                    return false;
            }

            return true;
        }

        private bool CmdError(IEnumerable<Error> errors)
        {
            microsoftLogger?.LogError("Couldn't parse command. Errors: {0}", String.Join(", ", errors));
            return true;
        }

        #endregion
    }
}
