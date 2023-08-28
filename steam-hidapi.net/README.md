# neptune-hidapi.net
Steam Deck Controller (Neptune) HID Api Library for .Net

#### How to use

* Download the .dll files from the releases section
* Copy them to the root of your project
* Right click, select settings and set the file to "Always copy to output dir"
* Make sure your project is built for x64 platform.
** If not, go to Build > Configuration manager and add a new configuration for x64.
* See below Example on how to grab controller inputs and write them to the console

```c#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neptune_hidapi.net
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NeptuneController controller = new NeptuneController();
            controller.OnControllerInputReceived += Controller_OnControllerInputReceived;
            controller.LizardModeEnabled = true; //Mouse and Keyboard emulation enabled.
            controller.Open(); //Open the controller device.
            Console.WriteLine($"Controller Serial: {controller.SerialNumber}"); // Output the serial number. Only available once device has been opened.
            Console.ReadLine();
        }

        static DateTime lastUpdate = DateTime.Now;

        private static void Controller_OnControllerInputReceived(object sender, NeptuneControllerInputEventArgs e)
        {
            if ((DateTime.Now - lastUpdate).TotalMilliseconds > 100)
            {
                Console.CursorTop = 0;
                Console.CursorLeft = 0;
                foreach (var btn in e.State.ButtonState.Buttons)
                {
                    Console.WriteLine($"{btn}: {e.State.ButtonState[btn]}      ");
                }
                foreach (var axis in e.State.AxesState.Axes)
                {
                    Console.WriteLine($"{axis}: {e.State.AxesState[axis]}      ");
                }
                lastUpdate = DateTime.Now;
            }
        }
    }
}

```
