using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerHelper
{
	class Options
	{
		[Verb("profile", true, HelpText = "create or update profile for specified app")]
		public class ProfileOption
		{
			[Option('m', "mode", Required = true)]
			public string mode { get; set; }

			[Option('e', "exe", Required = true)]
			public string exe { get; set; }
		}
	}

}
