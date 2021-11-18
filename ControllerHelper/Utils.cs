using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ControllerHelper
{
    class Utils
    {
        #region imports
        [DllImport("Kernel32.dll")]
        static extern uint QueryFullProcessImageName(IntPtr hProcess, uint flags, StringBuilder text, out uint size);
        #endregion

        public static void SendToast(string title, string content)
        {
            string url = "file:///" + AppDomain.CurrentDomain.BaseDirectory + "Toast.png";
            var uri = new Uri(url);

            new ToastContentBuilder()
                .AddText(title)
                .AddText(content)
                .AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle)
                .SetToastDuration(ToastDuration.Short)
                .Show();
        }

        public static string GetPathToApp(Process proc)
        {
            string pathToExe = string.Empty;

            if (null != proc)
            {
                uint nChars = 256;
                StringBuilder Buff = new StringBuilder((int)nChars);

                uint success = QueryFullProcessImageName(proc.Handle, 0, Buff, out nChars);

                if (0 != success)
                    pathToExe = Buff.ToString();
                else
                    pathToExe = "";
            }

            return pathToExe;
        }
    }
}
