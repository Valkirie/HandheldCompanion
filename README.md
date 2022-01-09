# Controller Service

Windows Service enables AYA NEO built in 6-axis gyroscope/accelerometer, providing great features like high-precision aiming assistance.

----

## About
Controller Service provides gyroscope and accelerometer support to the AYA NEO 2020 and 2021 models through a virtual DualShock 4 controller. If the service is enabled, embedded controller will be cloaked to applications outside the whitelist. If the service is disabled, embedded controller will be uncloaked and virtual DualShock 4 controller disabled.
Controller Service relies on `ViGEmBus` driver and `ViGEmClient` libraries as well as `HidHide` kernel-mode filter driver. Therefore, we strongly encourage you in donating to `Nefarius` via [PayPal](https://paypal.me/NefariusMaximus) for continued maintenance and development.

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

## Contribute
### Bugs & Features
Found a bug and want it fixed? Open a detailed issue on the [GitHub issue tracker](../../issues)!
Have an idea for a new feature? Let's have a chat about your request on [Discord](https://discord.gg/cKaZ5SX8kx).

### Questions & Support
Please respect that the GitHub issue tracker isn't a helpdesk. We offer a [Discord server](https://discord.gg/cKaZ5SX8kx), where you're welcome to check out and engage in discussions!

### Donation
If you would like to support this project, please consider making a donation to `BenjaminLSR` via [PayPal](https://www.paypal.com/paypalme/BenjaminLSR).

## Installation
Installers are [available as an all-in-one setup](../../releases/latest).
Run the `install.exe` as administrator and you'll be set!

## Credits & Libraries
- ViGEmBus: [Nefarius](https://github.com/ViGEm/ViGEmBus)
- ViGEmClient : [Nefarius](https://github.com/ViGEm/ViGEmClient)
- SharpDX : [https://github.com/sharpdx/SharpDX](https://github.com/sharpdx/SharpDX)
- Icon : [Nikita Golubev](https://www.flaticon.com/authors/nikita-golubev)
