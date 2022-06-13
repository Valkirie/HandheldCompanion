using CommandLine;
using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static HandheldCompanion.Options;

namespace HandheldCompanion
{
    public class CmdParser
    {
        public CmdParser()
        {
        }

        public void ParseArgs(string[] args, bool init = false)
        {
            if (args.Length == 0)
                return;

            LogManager.LogInformation("Parsing command: {0}", String.Join(' ', args));

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
            // implement me
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
            profile.umc_trigger = (GamepadButtonFlagsExt)option.trigger;

            profile.umc_input = (Input)option.input;
            profile.umc_output = (Output)option.output;

            profile.steering = option.steering;
            profile.inverthorizontal = option.inverthorizontal;
            profile.invertvertical = option.invertvertical;

            profile.flickstick_enabled = option.flickstick;

            MainWindow.profileManager.UpdateOrCreateProfile(profile);
            MainWindow.profileManager.SerializeProfile(profile);

            return true;
        }

        private bool CmdService(ProfileService option, bool init = false)
        {
            switch (option.action)
            {
                case ProfileServiceAction.create:
                    MainWindow.serviceManager.CreateService(MainWindow.CurrentPathService);
                    break;
                case ProfileServiceAction.delete:
                    MainWindow.serviceManager.DeleteService();
                    break;
                case ProfileServiceAction.start:
                    MainWindow.serviceManager.StartServiceAsync();
                    break;
                case ProfileServiceAction.stop:
                    MainWindow.serviceManager.StopServiceAsync();
                    break;
                case ProfileServiceAction.install:
                    MainWindow.serviceManager.CreateService(MainWindow.CurrentPathService);
                    MainWindow.serviceManager.SetStartType(System.ServiceProcess.ServiceStartMode.Automatic);
                    MainWindow.serviceManager.StartServiceAsync();
                    break;
                case ProfileServiceAction.uninstall:
                    MainWindow.pipeClient.SendMessage(new PipeShutdown());
                    MainWindow.serviceManager.StopServiceAsync();
                    MainWindow.serviceManager.DeleteService();
                    if (init) Process.GetCurrentProcess().Kill();
                    break;
                default:
                    return false;
            }

            return true;
        }

        private bool CmdError(IEnumerable<Error> errors)
        {
            LogManager.LogError("Couldn't parse command. Errors: {0}", String.Join(", ", errors));
            return true;
        }

        #endregion
    }
}
