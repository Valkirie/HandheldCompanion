using CommandLine;
using ControllerCommon;
using System.ComponentModel;

namespace ControllerHelperWPF
{
    public class Options
    {
        [Verb("device", false, HelpText = "Update emulation mode and cloak status")]
        public class DeviceOption
        {
            [Option("cloak", Required = false)]
            public bool cloak { get; set; }

            [Option("mode", Required = true)]
            public HIDmode mode { get; set; }
        }

        [Verb("profile", false, HelpText = "Create or update profile for specified app")]
        public class ProfileOption
        {
            [Option("whitelist", Required = false)]
            public bool whitelist { get; set; }

            [Option("exe", Required = true)]
            public string exe { get; set; }

            [Option("wrapper", Required = false)]
            public bool wrapper { get; set; }

            [Option("umc", Required = false)]
            public bool umc { get; set; }

            [Option("input", Required = false)]
            public int input { get; set; }

            [Option("trigger", Required = false)]
            public int trigger { get; set; }

            [Option("steering", Required = false)]
            public int steering { get; set; }                   // 0 = Roll, 1 = Yaw          

            [Option("inverthorizontal", Required = false)]
            public bool inverthorizontal { get; set; }          // if true, invert horizontal axis

            [Option("invertvertical", Required = false)]
            public bool invertvertical { get; set; }            // if false, invert vertical axis
        }

        public enum ProfileServiceAction
        {
            [Description("install")]
            install,
            [Description("uninstall")]
            uninstall,
            [Description("create")]
            create,
            [Description("delete")]
            delete,
            [Description("start")]
            start,
            [Description("stop")]
            stop
        }

        [Verb("service", false, HelpText = "Create, update or delete the service")]
        public class ProfileService
        {
            [Option("action", Required = true)]
            public ProfileServiceAction action { get; set; }
        }
    }

}
