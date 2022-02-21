using CommandLine;
using ControllerCommon;
using ControllerHelperWPF.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using static ControllerHelperWPF.Options;

namespace ControllerHelperWPF
{
    public class CmdParser
    {
        private readonly PipeClient pipeClient;
        private readonly MainWindow mainWindow;

        private readonly ILogger microsoftLogger;
        private string[] args;

        public CmdParser(PipeClient pipeClient, MainWindow mainWindow, ILogger microsoftLogger)
        {
            this.pipeClient = pipeClient;
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;
        }

        public void ParseArgs(string[] args)
        {
            this.args = args;

            if (args.Length == 0)
                return;

            microsoftLogger?.LogInformation("Parsing command: {0}", String.Join(' ', args));

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
            mainWindow.UpdateHID(option.mode);
            mainWindow.UpdateCloak(option.cloak);
            return true;
        }

        private bool CmdProfile(ProfileOption option)
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

            mainWindow.profileManager.UpdateProfile(profile);
            mainWindow.profileManager.SerializeProfile(profile);

            return true;
        }

        private bool CmdService(ProfileService option)
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
                    mainWindow.serviceManager.StartService();
                    break;
                case ProfileServiceAction.stop:
                    mainWindow.serviceManager.StopService();
                    break;
                case ProfileServiceAction.install:
                    mainWindow.serviceManager.CreateService(mainWindow.CurrentPathService);
                    mainWindow.serviceManager.SetStartType(System.ServiceProcess.ServiceStartMode.Automatic);
                    mainWindow.serviceManager.StartService();
                    break;
                case ProfileServiceAction.uninstall:
                    pipeClient.SendMessage(new PipeShutdown());
                    mainWindow.serviceManager.StopService();
                    mainWindow.serviceManager.DeleteService();
                    mainWindow.Close(); // temp
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
