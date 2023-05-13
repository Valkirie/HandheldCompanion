# hidapi.net
hidapi wrapper library for .net 

## How to use it

!!! Make sure your target platform is set to x64, else you will get an error while importing the dll. !!!

```c#

HidDevice device = new HidDevice(0x28de, 0x1205); //Vendor ID and Product ID of the HID Device.

bool result = device.OpenDevice(); //will return false if not successful.

device.OnInputReceived += Device_OnInputReceived; //attach event listener for incoming data
device.BeginRead(); //Start reading

```
