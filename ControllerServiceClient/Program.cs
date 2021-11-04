using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ControllerServiceClient
{
    class Program
    {
        [DllImport("User32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);

        static void Main(string[] args)
        {
            var re = new Regex("(?<=\")[^\"]*(?=\")|[^\" ]+");
            var strings = re.Matches(args[0]).Cast<Match>().Select(m => m.Value).ToArray();

            if (args.Length == 0)
            {
                Console.WriteLine("Invalid args");
                return;
            }

            List<string> args2 = new List<string>();
            if (args.Length > 1)
                args2.AddRange(args);
            else
                args2.AddRange(strings.Where(a => a != " "));

            var command = args2[0];

            switch (command)
            {
                case "-g": // get foreground process id
                    PushProcessId();
                    break;
                case "-t" when args2.Count == 3:
                    SendToast(args2[1], args2[2]); // send toast notification
                    break;
                default:
                    Console.WriteLine("Invalid command");
                    break;
            }
            return;
        }

        static void SendToast(string title, string content)
        {
            string url = "file:///" + AppDomain.CurrentDomain.BaseDirectory + "Toast.png";
            var uri = new Uri(url);

            new ToastContentBuilder()
                .AddText(title)
                .AddText(content)
                // .AddInlineImage(uri)
                .AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle)
                .SetToastDuration(ToastDuration.Short)
                .Show();
        }

        static void PushProcessId()
        {
            IntPtr hWnd = GetForegroundWindow();
            IntPtr processId;

            if (GetWindowThreadProcessId(hWnd, out processId) == 0)
                return;

            Console.WriteLine(processId);

        }
    }
}
