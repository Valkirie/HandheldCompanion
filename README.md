[![Download Latest](https://img.shields.io/github/downloads/Valkirie/HandheldCompanion/latest/total?style=flat-square&color=orange&label=Download%20Latest)](https://github.com/Valkirie/ControllerService/releases/latest)
[![discord](https://img.shields.io/discord/1054321983166365726?color=orange&label=Discord&logo=discord&logoColor=white&style=flat-square)](https://discord.gg/znHuywFz5M)
[![YouTube Channel](https://img.shields.io/youtube/channel/subscribers/UCFLra6QVYJYeaWp2mGaq3Og?style=flat-square&color=orange&label=YouTube%20Channel&logo=youtube&logoColor=white)](https://www.youtube.com/channel/UCFLra6QVYJYeaWp2mGaq3Og)
[![Donations](https://img.shields.io/badge/PayPal-00457C?style=flat-square&color=orange&label=Donations&logo=paypal&logoColor=white)](https://www.paypal.com/paypalme/BenjaminLSR)

# Handheld Companion

A combination of a Windows service and a touch interface optimized GUI to increase your handheld gaming computer experience. Features include:
- Motion control a.k.a. gyro control through a device's inertial measurement unit (IMU, Gyroscope and Accelerometer) or external sensor. Settings availible for racing, 1st and 3rd person gaming and emulator support.
- Quicktools overlay, with easy access to various settings and informatio such as TDP, GPU, Screen Hz, Resolution, Brightness, Volume, Powermode control and battery level.
- Virtual controller simulation of [Microsoft Xbox 360 Controller](https://en.wikipedia.org/wiki/Xbox_360_controller) and [Sony DualShock 4 Controller](https://en.wikipedia.org/wiki/DualShock#DualShock_4).
- Profile settings system, automatic detection of active game and applying of settings.
- PS Remote Play support with DS4 controller, including motion and touchpad.
- 3D Controller overlay for stream recordings.

## Use cases
A few examples of the most common use cases are:
- You want to add universal motion controls (UMC) to any game.
- You want to add high-precision motion controls to your Windows game library through [Steam](https://store.steampowered.com/controller/update/dec15).
- You want to play your Sony Playstation 4 library through [PlayStation Now](https://www.playstation.com/en-us/ps-now/) or [PS4 Remote Play](<https://remoteplay.dl.playstation.net/remoteplay/>).
- You want to enjoy all your [Wii](https://dolphin-emu.org/), [WiiU](https://cemu.info/) and [Switch](https://yuzu-emu.org/) games with full motion controls through UDP motion control protocol.

[Youtube Channel](https://www.youtube.com/channel/UCFLra6QVYJYeaWp2mGaq3Og)

## Supported Systems
The software is built for Windows 10/Windows 11 (x86 and amd64).

## Supported Devices

- AOKZOE A1
- AYA Neo and its different versions
- AYA Neo Next and its different versions
- AYA Neo Air and it's different versions
- AYA Neo 2
- ONEXPLAYER MINI and its different versions (Intel, AMD, Gundam)
- GPD WIN Max 2 (Intel and AMD)
- GPD Win 3
- Steam Deck

## Supported Sensors
- Bosch BMI160 (and similar)
- USB IMU (GY-USB002)

## Supported Languages
- English
- French
- Chinese (Simplified)
- Chinese (Traditional)

## Visuals
![image](https://user-images.githubusercontent.com/934757/188308169-abfcc335-39e9-44c5-ac9a-bc0e02ac7cec.png)
![image](https://user-images.githubusercontent.com/934757/188308173-6fcd01ce-eabf-4340-b2f2-a9f63a5b4072.png)
![image](https://user-images.githubusercontent.com/934757/188308180-3dc34830-4e5e-4c74-a3cf-33eee5c77cd9.png)
![image](https://user-images.githubusercontent.com/934757/188308184-e8893451-127a-48b3-be90-6229c614c317.png)

## Overlay
The software has multiple built-in overlay options. 

### QuickTools

On the fly adjustment of TDP (global and profile), brightness, screen resolution and frequency, hotkeys and motion control profile settings. Summonable with a user defined button combination (including certaind supported devices mapped special keys). Window can be aligned how the user sees fit (left, right, floating).

![Quicktools-01](https://user-images.githubusercontent.com/14330834/184693435-7df5ad40-ddb1-4359-9335-1a5804441dc3.png)
Quicktools profile TDP control with Axiom Verge.

![Quicktools-02](https://user-images.githubusercontent.com/14330834/184693443-117d8594-f4e5-4400-8341-2fb95b986d01.png)
Quicktools profile motion settings with Borderlands Pre-Sequel.

### Virtual touchpad

Virtual touchpad on top of your gaming sessions. The virtual touchpad is used to mimic the DualShock 4 physical touchpad and grants maximum compatibility with PS Now, PS Remote software suites and games that make specific use of the Steampad touchpads.

![Touchpad](https://thumbs.gfycat.com/DiscreteJollyBluemorphobutterfly-size_restricted.gif)

Virtual Touchpad input demonstration with [PS Remote Play](https://remoteplay.dl.playstation.net/remoteplay/lang/en/)

![Example02](https://user-images.githubusercontent.com/14330834/184550793-d81e2ec9-0271-4aae-bc44-7aeb393631ea.png)

PS Remote Play, The Last of Us Part 2

### 3D Controller

Display a 3D virtual controller, showcasing the motion of the device and all button interaction, individual button presses, joystick and trigger positions. The following 3D models are availible.
  - OEM controller (Ayaneo Pro, Ayaneo Next, OneXPlayer Mini)
  - Emulated controller (DualShock 4, Xbox 360)
  - Xbox One controller
  - ZDO+ controller
  - Fisher-Price controller
  - Machenike HG510 
  - 8BitDo Lite 2
  - Nintendo 64
  - Dual Sense

![image](https://thumbs.gfycat.com/BlackandwhiteRareBorderterrier-size_restricted.gif)

## Contribute
### Bugs & Features
Found a bug and want it fixed? Open a detailed issue on the [GitHub issue tracker](../../issues)!
Have an idea for a new feature? Let's have a chat about your request on [Discord](https://discord.gg/znHuywFz5M).

### Questions & Support
Please respect that the GitHub issue tracker isn't a helpdesk. We offer a [Discord server](https://discord.gg/znHuywFz5M), where you're welcome to check out and engage in discussions!

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
- Godot Engine Illustration : [Juan Linietsky, Fernando Miguel Calabró](https://github.com/godotengine/tps-demo)

## Licensing

![image](https://user-images.githubusercontent.com/934757/159507299-ee55ec0b-8c0a-41b6-8dab-a1c72589565e.png)![image](https://user-images.githubusercontent.com/934757/159507349-caf88e3f-508b-4293-ae69-9918d6ba3d75.png)![image](https://user-images.githubusercontent.com/934757/159507749-c6ce02f6-b428-4592-96ca-95084ac5669b.png)![image](https://user-images.githubusercontent.com/934757/159507875-9ee29e9d-9528-4345-9503-0e2a13faeb4c.png)

This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License. To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/4.0/ or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

We believe in the fair use of open-source solutions. We expect OEMs to come forward before distributing our solution with their devices. This way we can work together to make your device and our solution compatible in the best possible way. We reserve the right to take any action necessary to block partial or full access to the application to any entities that do not comply with the license or fair use principle.
