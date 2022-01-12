using CommandLine;
using ControllerCommon;
using System.ComponentModel;

namespace ControllerHelper
{
    public class Options
    {
        [Verb("profile", false, HelpText = "create or update profile for specified app")]
        public class ProfileOption
        {
            [Option("mode", Required = true)]
            public HIDmode mode { get; set; }

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

        [Verb("service", false, HelpText = "create, update or delete the service")]
        public class ProfileService
        {
            [Option("action", Required = true)]
            public ProfileServiceAction action { get; set; }
        }
    }

}
