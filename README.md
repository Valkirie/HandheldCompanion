# Controller Service & Handheld Companion

A combination of a Windows service and a touch interface optimized GUI to increase your handheld gaming computer experience. Features include:
- Motion control through a device's inertial measurement unit (IMU, Gyroscope and Accelerometer) or external sensor.
- Virtual controller simulation of [Microsoft Xbox 360 Controller](https://en.wikipedia.org/wiki/Xbox_360_controller) and [Sony DualShock 4 Controller](https://en.wikipedia.org/wiki/DualShock#DualShock_4)
- Overlay, virtual touchpads and 3D controller model
- Per application based profile settings system

## Use cases
A few examples of the most common use cases are:
- You want to add high-precision motion controls to your Windows game library through [Steam](https://store.steampowered.com/controller/update/dec15).
- You want to play your Sony Playstation 4 library through [PlayStation Now](https://www.playstation.com/en-us/ps-now/) or [PS4 Remote Play](<https://remoteplay.dl.playstation.net/remoteplay/>).
- You want to enjoy all your [Wii](https://dolphin-emu.org/), [WiiU](https://cemu.info/) and [Switch](https://yuzu-emu.org/) games with full motion controls through UDP motion control protocol.

## Supported Systems
The software is built for Windows 10/Windows 11 (x86 and amd64).

## Supported Devices
- AYA Neo and its different versions
- AYA Next and its different versions
- ONEXPLAYER MINI and its different versions (Intel, AMD, Gundam)
- GPD WIN Max 2

## Supported Sensors
- Bosch BMI160 (and similar)
- USB IMU (GY-USB002)

## Supported Languages
- English
- French
- Chinese (Simplified)
- Chinese (Traditional)

## Visuals
![image](https://user-images.githubusercontent.com/934757/175270847-0638207d-f2c2-4bf7-becd-da6860638b0b.png)
![image](https://user-images.githubusercontent.com/934757/175270868-b2bffc77-878b-4a58-b2db-7387324c2390.png)
![image](https://user-images.githubusercontent.com/934757/175270899-edd658b5-1103-4bda-aaa6-1685e2b3d3f5.png)
![image](https://user-images.githubusercontent.com/934757/175270908-54eddc08-2620-4831-bc91-bfeb4340462a.png)
![image](https://user-images.githubusercontent.com/934757/175271013-6c130e24-ec81-4d9a-9d85-cb74c5e7e327.png)
![image](https://user-images.githubusercontent.com/934757/175271036-52fde73c-f9d4-4f67-aede-4a5618e0fe28.png)

## Overlay
The software has multiple built-in overlay options. 
- Virtual touchpad on top of your gaming sessions. The virtual touchpad is used to mimic the DualShock 4 physical touchpad and grants maximum compatibility with PS Now, PS Remote software suites and games that make specific use of the Steampad touchpads.
- Display a 3D virtual controller, showcasing the motion of the device and all button interaction, individual button presses, joystick and trigger positions. The following 3D models are availible.
  - OEM controller (Ayaneo Pro, Ayaneo Next, OneXPlayer Mini)
  - Emulated controller (DualShock 4, Xbox 360)
  - ZDO+ controller
  - Fisher-Price controller

![image](https://user-images.githubusercontent.com/934757/175498734-29c9dc25-e7ed-44f3-a6f6-9600122c35cd.png)

## Contribute
### Bugs & Features
Found a bug and want it fixed? Open a detailed issue on the [GitHub issue tracker](../../issues)!
Have an idea for a new feature? Let's have a chat about your request on [Discord](https://discord.gg/cKaZ5SX8kx).

### Questions & Support
Please respect that the GitHub issue tracker isn't a helpdesk. We offer a [Discord server](https://discord.gg/cKaZ5SX8kx), where you're welcome to check out and engage in discussions!

### Donation
If you would like to support this project, please consider making a donation to `BenjaminLSR` via [PayPal](https://www.paypal.com/paypalme/BenjaminLSR).

Controller Service relies on `ViGEmBus` driver and `ViGEmClient` libraries as well as `HidHide` kernel-mode filter driver. Therefore, we strongly encourage you in donating to `Nefarius` via [PayPal](https://paypal.me/NefariusMaximus) for continued maintenance and development.

## Installation
Installers are [available as an all-in-one setup](../../releases/latest).
Run the `install.exe` as administrator and you'll be set!

## Credits & Libraries
- ViGEmBus: [Nefarius](https://github.com/ViGEm/ViGEmBus)
- ViGEmClient : [Nefarius](https://github.com/ViGEm/ViGEmClient)
- SharpDX : [https://github.com/sharpdx/SharpDX](https://github.com/sharpdx/SharpDX)

## Licensing

![image](https://user-images.githubusercontent.com/934757/159507299-ee55ec0b-8c0a-41b6-8dab-a1c72589565e.png)![image](https://user-images.githubusercontent.com/934757/159507349-caf88e3f-508b-4293-ae69-9918d6ba3d75.png)![image](https://user-images.githubusercontent.com/934757/159507749-c6ce02f6-b428-4592-96ca-95084ac5669b.png)![image](https://user-images.githubusercontent.com/934757/159507875-9ee29e9d-9528-4345-9503-0e2a13faeb4c.png)

This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License. To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/4.0/ or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

We believe in the fair use of open-source solutions. We expect OEMs to come forward before distributing our solution with their devices. This way we can work together to make your device and our solution compatible in the best possible way. We reserve the right to take any action necessary to block partial or full access to the application to any entities that do not comply with the license or fair use principle.
