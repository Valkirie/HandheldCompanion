using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            string title = "- ";
            IntPtr hWnd = GetForegroundWindow();

            IntPtr processId;
            if (GetWindowThreadProcessId(hWnd, out processId) == 0)
                return;

            Console.WriteLine(processId);
            return;
        }
    }
}
