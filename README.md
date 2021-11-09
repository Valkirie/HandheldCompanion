# Controller Service

Windows Service enabling AYA NEO built in 6-axis gyroscope/accelerometer, providing great features like high-precision aiming assistance.

----

## About
Controller Service provides gyroscope and accelerometer support to the AYA NEO 2020 and 2021 models through a virtual DualShock 4 controller. If the service is enabled, embedded controller will be cloaked to applications outside the whitelist. If the service is disabled, embedded controller will be uncloaked and virtual DualShock 4 controller disabled.
Controller Service relies on `ViGEmBus` driver and `ViGEmClient` libraries as well as `HidHide` kernel-mode filter driver. Therefore, we strongly encourage you in donating to `Nefarius` via [PayPal](https://paypal.me/NefariusMaximus) for continued maintenance and development.

### Emulated devices
Controller Service supports emulation of the following USB Gamepads:
- [Sony DualShock 4 Controller](https://en.wikipedia.org/wiki/DualShock#DualShock_4)

## Use cases
A few examples of the most common use cases for `Controller Service` are:
- You want to add high-precision motion controls to your Windows game library through [Steam](https://store.steampowered.com/controller/update/dec15).
- You want to play your Sony Playstation 4 library through [PlayStation Now](https://www.playstation.com/en-us/ps-now/) or [PS4 Remote Play](<https://remoteplay.dl.playstation.net/remoteplay/>).
- You want to enjoy your all your WiiU and Switch games with full motion controls.

## Supported Systems
The software is built for Windows 10/Windows 11 (x86 and amd64).

## Contribute
### Bugs & Features
Found a bug and want it fixed? Open a detailed issue on the [GitHub issue tracker](../../issues)!
Have an idea for a new feature? Let's have a chat about your request on [Discord](https://discord.vigem.org) or the [community forums](https://forums.vigem.org).

### Questions & Support
Please respect that the GitHub issue tracker isn't a helpdesk. We offer a [Discord server](https://discord.gg/cKaZ5SX8kx), where you're welcome to check out and engage in discussions!


## Installation
Pre-built production-signed binaries are [available as an all-in-one setup](../../releases/latest).
When extracted, run the `install.cmd` as administrator and you'll be set !
