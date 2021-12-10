using CommandLine;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerHelper
{
	public class Options
	{
		public enum ProfileOptionMode
		{
			[Description("xinput")]
			xinput,
			[Description("ds4")]
			ds4
		}

		[Verb("profile", false, HelpText = "create or update profile for specified app")]
		public class ProfileOption
		{
			[Option('m', "mode", Required = true)]
			public ProfileOptionMode mode { get; set; }

			[Option('e', "exe", Required = true)]
			public string exe { get; set; }
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
			[Option('a', "action", Required = true)]
			public ProfileServiceAction action { get; set; }
		}
	}

}
