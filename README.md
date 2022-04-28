# Controller Service & Handheld Companion

A combination of a Windows service and a touch interface to take advantage of your device's inertial measurement unit (IMU) and enhance your gaming experience with the controller.

## About
The **Controller Service** provides gyroscope and accelerometer support to devices with an embedded IMU through a virtual controller. **Handheld Companion** front-end provides a touch-enabled interface to manage all service settings and manage your game-specific profiles.
- When turned-on, embedded controller will be cloaked to applications outside the whitelist and virtual controller enabled.
- When turned-off, embedded controller will be uncloaked and virtual controller disabled.

### Emulated devices
Controller Service supports emulation of the following USB Gamepads:
- [Microsoft Xbox 360 Controller](https://en.wikipedia.org/wiki/Xbox_360_controller)
- [Sony DualShock 4 Controller](https://en.wikipedia.org/wiki/DualShock#DualShock_4)

## Use cases
A few examples of the most common use cases for `Controller Service` are:
- You want to add high-precision motion controls to your Windows game library through [Steam](https://store.steampowered.com/controller/update/dec15).
- You want to play your Sony Playstation 4 library through [PlayStation Now](https://www.playstation.com/en-us/ps-now/) or [PS4 Remote Play](<https://remoteplay.dl.playstation.net/remoteplay/>).
- You want to enjoy all your [Wii](https://dolphin-emu.org/), [WiiU](https://cemu.info/) and [Switch](https://yuzu-emu.org/) games with full motion controls through UDP motion control protocol. 

## Supported Systems
The software is built for Windows 10/Windows 11 (x86 and amd64).

## Supported Devices
- AYA Neo and its different versions
- AYA Next and its different versions
- ONEXPLAYER MINI (Work in progress...)

## Supported Sensors
- Bosch BMI160

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

## Visuals

![image](https://user-images.githubusercontent.com/934757/158461053-180c23d3-844a-4187-bf4b-968eb504d89e.png)
![image](https://user-images.githubusercontent.com/934757/158461074-b387f10a-de24-40df-a52e-61711850b24a.png)
![image](https://user-images.githubusercontent.com/934757/158461093-62402463-4e46-45e5-b1db-9045ab8f38e5.png)
![image](https://user-images.githubusercontent.com/934757/158461113-70890600-a7c4-46fb-a8ec-f39eb2341ee3.png)
![image](https://user-images.githubusercontent.com/934757/158461938-e5c96ad7-b6eb-4bd2-9260-9f4c1ca4d199.png)
![image](https://user-images.githubusercontent.com/934757/158461955-6cffd0ac-0399-4afa-9d32-cfb3ab6aab5b.png)
