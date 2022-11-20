using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace ControllerCommon.Utils
{
    public static class RegistryUtils
    {
        private const string HKLM = @"HKEY_LOCAL_MACHINE";

        public static string GetHKLM(string key, string value)
        {
            string keyName = HKLM + "\\" + key;
            return Convert.ToString(Registry.GetValue(keyName, value, string.Empty));
        }
    }
}
