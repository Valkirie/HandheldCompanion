using System.Linq;

namespace HandheldCompanion.Platforms.Discovery;

public static class EmulatorDefinitions
{
    public static GamePlatform AllPlatforms => All.Aggregate(GamePlatform.Generic, (platform, definition) => platform | definition.PlatformType);

    public static EmulatorDefinition[] All { get; } =
    [
        new()
        {
            Id = "dolphin",
            Name = "Dolphin",
            PlatformType = GamePlatform.Dolphin,
            PlatformColor = "#0074BD",
            LaunchArgumentMode = LaunchArgumentMode.Template,
            ArgumentTemplate = "-e {rom}",
            Executables = ["Dolphin.exe"],
            ProductNames = ["Dolphin Emulator"],
            Systems = ["GameCube", "Wii"],
            RomExtensions = [".gcm", ".gcz", ".iso", ".wbfs", ".rvz"],
            Configurations =
            [
                new() { Root = ConfigRoot.AppData, RelativePath = "Dolphin Emulator", Files = ["Dolphin.ini"], ContentKeys = ["ISOPath"] },
                new() { Root = ConfigRoot.Documents, RelativePath = "Dolphin Emulator", Files = ["Dolphin.ini"], ContentKeys = ["ISOPath"] },
                new() { Root = ConfigRoot.ExecutableDirectory, RelativePath = "User\\Config", Files = ["Dolphin.ini"], ContentKeys = ["ISOPath"] }
            ],
            PortableLocations = [new() { Marker = "User", ConfigPath = "User\\Config", Files = ["Dolphin.ini"] }]
        },
        new()
        {
            Id = "pcsx2",
            Name = "PCSX2",
            PlatformType = GamePlatform.PCSX2,
            PlatformColor = "#1565C0",
            ArgumentTemplate = "{rom}",
            Executables = ["pcsx2-qt.exe", "pcsx2.exe"],
            ProductNames = ["PCSX2"],
            Systems = ["PlayStation 2"],
            RomExtensions = [".iso", ".cso", ".chd", ".bin"],
            Configurations =
            [
                new() { Root = ConfigRoot.AppData, RelativePath = "PCSX2", Files = ["PCSX2.ini"] },
                new() { Root = ConfigRoot.ExecutableDirectory, RelativePath = "inis", Files = ["PCSX2.ini"] }
            ]
        },
        new()
        {
            Id = "cemu",
            Name = "Cemu",
            PlatformType = GamePlatform.Cemu,
            PlatformColor = "#00A3E0",
            LaunchArgumentMode = LaunchArgumentMode.Template,
            ArgumentTemplate = "-g {rom}",
            RomMetadata = new() { RelativePath = "meta\\meta.xml", ElementName = "longname_en", SearchParentDirectories = true },
            Executables = ["Cemu.exe"],
            ProductNames = ["Cemu"],
            Systems = ["Wii U"],
            RomExtensions = [".wud", ".wux", ".wua", ".rpx"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "Cemu", Files = ["settings.xml"], ContentKeys = ["GamePaths"] }],
            PortableLocations = [new() { Marker = "portable", ConfigPath = "portable", Files = ["settings.xml"] }]
        },
        new()
        {
            Id = "rpcs3",
            Name = "RPCS3",
            PlatformType = GamePlatform.RPCS3,
            PlatformColor = "#5B5B5B",
            ArgumentTemplate = "{rom}",
            Executables = ["rpcs3.exe"],
            ProductNames = ["RPCS3"],
            Systems = ["PlayStation 3"],
            RomExtensions = [".self", ".iso"],
            Configurations =
            [
                new() { Root = ConfigRoot.ExecutableDirectory, Files = ["config.yml", "games.yml"] },
                new() { Root = ConfigRoot.ExecutableDirectory, RelativePath = "config", Files = ["config.yml"] }
            ],
            RomMetadata = new() { Provider = RomMetadataProvider.ParamSfo, RelativePath = "PS3_GAME\\PARAM.SFO", SearchParentDirectories = false }
        },
        new()
        {
            Id = "shadps4",
            Name = "ShadPS4",
            PlatformType = GamePlatform.ShadPS4,
            PlatformColor = "#212894",
            LaunchArgumentMode = LaunchArgumentMode.Template,
            ArgumentTemplate = "-g {rom}",
            Executables = ["shadPS4.exe", "shadPS4QtLauncher.exe"],
            ProductNames = ["ShadPS4"],
            Systems = ["PlayStation 4"],
            RomExtensions = [],
            DirectoryRoms = true,
            RomMetadata = new() { Provider = RomMetadataProvider.ParamSfo, RelativePath = "sce_sys\\param.sfo", SearchParentDirectories = false },
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "shadPS4", Files = ["config.json"], ContentKeys = ["install_dirs"] }]
        },
        new()
        {
            Id = "citra",
            Name = "Citra",
            PlatformType = GamePlatform.Citra,
            PlatformColor = "#D32F2F",
            ArgumentTemplate = "{rom}",
            Executables = ["citra.exe", "citra-qt.exe"],
            ProductNames = ["Citra"],
            Systems = ["Nintendo 3DS"],
            RomExtensions = [".3ds", ".3dsx", ".cia", ".cci"],
            Configurations =
            [
                new() { Root = ConfigRoot.AppData, RelativePath = "Citra\\config", Files = ["qt-config.ini"] }
            ]
        },
        new()
        {
            Id = "azahar",
            Name = "Azahar",
            PlatformType = GamePlatform.Azahar,
            PlatformColor = "#D32F2F",
            ArgumentTemplate = "{rom}",
            Executables = ["azahar.exe", "azahar-qt.exe"],
            ProductNames = ["Azahar"],
            Systems = ["Nintendo 3DS"],
            RomExtensions = [".3ds", ".3dsx", ".cia", ".cci"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "Azahar\\config", Files = ["qt-config.ini"] }]
        },
        new()
        {
            Id = "yuzu-family",
            Name = "Yuzu / Citron / Eden",
            PlatformType = GamePlatform.Yuzu,
            PlatformColor = "#E60012",
            LaunchArgumentMode = LaunchArgumentMode.Template,
            ArgumentTemplate = "-g {rom}",
            Executables = ["yuzu.exe", "yuzu-windows-msvc.exe"],
            ProductNames = ["yuzu"],
            Systems = ["Nintendo Switch"],
            RomExtensions = [".nsp", ".xci", ".nca"],
            Configurations =
            [
                new() { Root = ConfigRoot.AppData, RelativePath = "yuzu\\config", Files = ["qt-config.ini"], ContentKeys = ["Paths\\gamedirs\\1\\path", "Paths\\gamedirs\\2\\path", "Paths\\gamedirs\\3\\path", "Paths\\gamedirs\\4\\path"] },
                new() { Root = ConfigRoot.AppData, RelativePath = "yuzu\\config", Files = ["qt-config.ini"], ContentKeys = ["Paths\\gamedirs\\1\\path", "Paths\\gamedirs\\2\\path", "Paths\\gamedirs\\3\\path", "Paths\\gamedirs\\4\\path"] }
            ]
        },
        new()
        {
            Id = "citron",
            Name = "Citron",
            PlatformType = GamePlatform.Citron,
            PlatformColor = "#E60012",
            ArgumentTemplate = "{rom}",
            Executables = ["citron.exe"],
            ProductNames = ["Citron"],
            Systems = ["Nintendo Switch"],
            RomExtensions = [".nsp", ".xci", ".nca"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "Citron\\config", Files = ["qt-config.ini", "config.json"], ContentKeys = ["Paths\\gamedirs\\1\\path", "Paths\\gamedirs\\2\\path", "Paths\\gamedirs\\3\\path", "Paths\\gamedirs\\4\\path"] }]
        },
        new()
        {
            Id = "eden",
            Name = "Eden",
            PlatformType = GamePlatform.Eden,
            PlatformColor = "#E60012",
            ArgumentTemplate = "{rom}",
            Executables = ["eden.exe"],
            ProductNames = ["Eden"],
            Systems = ["Nintendo Switch"],
            RomExtensions = [".nsp", ".xci", ".nca"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "Eden\\config", Files = ["qt-config.ini", "config.json"], ContentKeys = ["Paths\\gamedirs\\1\\path", "Paths\\gamedirs\\2\\path", "Paths\\gamedirs\\3\\path", "Paths\\gamedirs\\4\\path"] }]
        },
        new()
        {
            Id = "duckstation",
            Name = "DuckStation",
            PlatformType = GamePlatform.DuckStation,
            PlatformColor = "#2E7D32",
            ArgumentTemplate = "{rom}",
            Executables = ["duckstation-qt-x64-ReleaseLTCG.exe", "duckstation-qt-x64-Release.exe", "duckstation-qt.exe"],
            ProductNames = ["DuckStation"],
            Systems = ["PlayStation"],
            RomExtensions = [".iso", ".bin", ".cue", ".chd", ".ecm"],
            Configurations =
            [
                new() { Root = ConfigRoot.LocalAppData, RelativePath = "DuckStation", Files = ["settings.ini"] },
                new() { Root = ConfigRoot.Documents, RelativePath = "DuckStation", Files = ["settings.ini"] }
            ],
            PortableLocations = [new() { Marker = "portable.txt", Files = ["settings.ini"] }]
        },
        new()
        {
            Id = "retroarch",
            Name = "RetroArch",
            PlatformType = GamePlatform.RetroArch,
            PlatformColor = "#6A1B9A",
            LaunchArgumentMode = LaunchArgumentMode.Unsupported,
            Executables = ["retroarch.exe"],
            ProductNames = ["RetroArch"],
            Systems = ["Multi-system"],
            RomExtensions = [".zip", ".7z", ".nes", ".sfc", ".gb", ".gbc", ".gba", ".iso"],
            Configurations =
            [
                new() { Root = ConfigRoot.ExecutableDirectory, Files = ["retroarch.cfg"], ContentKeys = ["content_directory", "system_directory", "playlist_directory", "savefile_directory"] },
                new() { Root = ConfigRoot.AppData, Files = ["retroarch.cfg"], ContentKeys = ["content_directory", "system_directory", "playlist_directory", "savefile_directory"] },
                new() { Root = ConfigRoot.AppData, RelativePath = "RetroArch", Files = ["retroarch.cfg"], ContentKeys = ["content_directory", "system_directory", "playlist_directory", "savefile_directory"] }
            ]
        },
        new()
        {
            Id = "ppsspp",
            Name = "PPSSPP",
            PlatformType = GamePlatform.PPSSPP,
            PlatformColor = "#1565C0",
            ArgumentTemplate = "{rom}",
            Executables = ["PPSSPPWindows64.exe", "PPSSPPWindows.exe"],
            ProductNames = ["PPSSPP"],
            Systems = ["PlayStation Portable"],
            RomExtensions = [".iso", ".cso", ".chd", ".pbp"],
            Configurations =
            [
                new() { Root = ConfigRoot.ExecutableDirectory, RelativePath = "memstick\\PSP\\SYSTEM", Files = ["ppsspp.ini"] },
                new() { Root = ConfigRoot.Documents, RelativePath = "PPSSPP\\PSP\\SYSTEM", Files = ["ppsspp.ini"] }
            ],
            RomMetadata = new() { Provider = RomMetadataProvider.ParamSfo, RelativePath = "PSP_GAME\\PARAM.SFO", SearchParentDirectories = false }
        },
        new()
        {
            Id = "mame", Name = "MAME", PlatformType = GamePlatform.MAME, PlatformColor = "#B71C1C", LaunchArgumentMode = LaunchArgumentMode.Unsupported,
            Executables = ["mame.exe"], ProductNames = ["MAME"], Systems = ["Arcade"],
            RomExtensions = [".zip", ".7z"],
            Configurations =
            [
                new() { Root = ConfigRoot.ExecutableDirectory, Files = ["mame.ini"], ContentKeys = ["rompath"] },
                new() { Root = ConfigRoot.UserProfile, RelativePath = ".mame", Files = ["mame.ini"], ContentKeys = ["rompath"] }
            ]
        },
        new()
        {
            Id = "mupen64plus", Name = "Mupen64Plus", PlatformType = GamePlatform.Mupen64Plus, ArgumentTemplate = "{rom}",
            Executables = ["mupen64plus-gui.exe", "mupen64plus.exe"], ProductNames = ["Mupen64Plus"], Systems = ["Nintendo 64"],
            RomExtensions = [".n64", ".z64", ".v64"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "Mupen64Plus", Files = ["mupen64plus.cfg"] }]
        },
        new()
        {
            Id = "project64", Name = "Project64", PlatformType = GamePlatform.Project64, ArgumentTemplate = "{rom}",
            Executables = ["Project64.exe"], ProductNames = ["Project64"], Systems = ["Nintendo 64"], RomExtensions = [".n64", ".z64", ".v64"]
        },
        new()
        {
            Id = "ryujinx", Name = "Ryujinx", PlatformType = GamePlatform.Ryujinx, PlatformColor = "#3949AB", ArgumentTemplate = "{rom}",
            Executables = ["Ryujinx.exe"], ProductNames = ["Ryujinx"], Systems = ["Nintendo Switch"], RomExtensions = [".nsp", ".xci", ".nca"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "Ryujinx", Files = ["Config.json"] }],
            PortableLocations = [new() { Marker = "portable", ConfigPath = "portable", Files = ["Config.json"] }]
        },
        new()
        {
            Id = "melonds", Name = "melonDS", PlatformType = GamePlatform.MelonDS, ArgumentTemplate = "{rom}",
            Executables = ["melonDS.exe"], ProductNames = ["melonDS"], Systems = ["Nintendo DS"], RomExtensions = [".nds"],
            Configurations = [new() { Root = ConfigRoot.ExecutableDirectory, Files = ["melonDS.ini"] }]
        },
        new()
        {
            Id = "vita3k", Name = "Vita3K", PlatformType = GamePlatform.Vita3K, PlatformColor = "#00838F", ArgumentTemplate = "{rom}",
            Executables = ["Vita3K.exe"], ProductNames = ["Vita3K"], Systems = ["PlayStation Vita"], RomExtensions = [".pkg", ".zip"],
            Configurations = [new() { Root = ConfigRoot.ExecutableDirectory, Files = ["config.yml"] }, new() { Root = ConfigRoot.AppData, RelativePath = "Vita3K", Files = ["config.yml"] }],
            RomMetadata = new() { Provider = RomMetadataProvider.ParamSfo, RelativePath = "sce_sys\\param.sfo", SearchParentDirectories = false }
        },
        new()
        {
            Id = "xenia", Name = "Xenia", PlatformType = GamePlatform.Xenia, PlatformColor = "#107C10", ArgumentTemplate = "{rom}",
            Executables = ["xenia.exe", "xenia-canary.exe"], ProductNames = ["Xenia"], Systems = ["Xbox 360"], RomExtensions = [".iso", ".xex"],
            Configurations = [new() { Root = ConfigRoot.Documents, RelativePath = "Xenia", Files = ["xenia.config.toml"] }],
            PortableLocations = [new() { Marker = "portable.txt", Files = ["xenia.config.toml"] }]
        },
        new()
        {
            Id = "xemu", Name = "xemu", PlatformType = GamePlatform.Xemu, ArgumentTemplate = "{rom}",
            Executables = ["xemu.exe"], ProductNames = ["xemu"], Systems = ["Xbox"], RomExtensions = [".iso", ".xiso"],
            Configurations = [new() { Root = ConfigRoot.LocalAppData, RelativePath = "xemu", Files = ["xemu.toml"] }]
        },
        new()
        {
            Id = "flycast", Name = "Flycast", PlatformType = GamePlatform.Flycast, PlatformColor = "#455A64", ArgumentTemplate = "{rom}",
            Executables = ["flycast.exe"], ProductNames = ["Flycast"], Systems = ["Dreamcast", "Naomi", "Atomiswave"], RomExtensions = [".gdi", ".cdi", ".chd"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "flycast", Files = ["emu.cfg"] }]
        },
        new()
        {
            Id = "redream", Name = "Redream", PlatformType = GamePlatform.Redream, ArgumentTemplate = "{rom}",
            Executables = ["redream.exe"], ProductNames = ["redream"], Systems = ["Dreamcast"], RomExtensions = [".gdi", ".cdi", ".chd"]
        },
        new()
        {
            Id = "scummvm", Name = "ScummVM", PlatformType = GamePlatform.ScummVM, LaunchArgumentMode = LaunchArgumentMode.Unsupported,
            Executables = ["scummvm.exe"], ProductNames = ["ScummVM"], Systems = ["PC Adventure"], RomExtensions = [".scummvm"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "ScummVM", Files = ["scummvm.ini"] }]
        },
        new()
        {
            Id = "dosbox", Name = "DOSBox", PlatformType = GamePlatform.DOSBox, LaunchArgumentMode = LaunchArgumentMode.Unsupported,
            Executables = ["DOSBox.exe"], ProductNames = ["DOSBox"], Systems = ["DOS"], RomExtensions = [".exe", ".com", ".bat"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "DOSBox", Files = ["dosbox*.conf"] }]
        },
        new()
        {
            Id = "dosbox-x", Name = "DOSBox-X", PlatformType = GamePlatform.DOSBoxX, LaunchArgumentMode = LaunchArgumentMode.Unsupported,
            Executables = ["dosbox-x.exe"], ProductNames = ["DOSBox-X"], Systems = ["DOS"], RomExtensions = [".exe", ".com", ".bat"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "DOSBox-X", Files = ["dosbox-x.conf"] }]
        },
        new()
        {
            Id = "mednafen", Name = "Mednafen", PlatformType = GamePlatform.Mednafen, ArgumentTemplate = "{rom}",
            Executables = ["mednafen.exe"], ProductNames = ["Mednafen"], Systems = ["Multi-system"], RomExtensions = [".cue", ".iso", ".nes", ".sfc", ".gb", ".gba"],
            Configurations = [new() { Root = ConfigRoot.AppData, RelativePath = "mednafen", Files = ["mednafen-09x.cfg"] }]
        },
        new()
        {
            Id = "visualboyadvance", Name = "VisualBoyAdvance-M", PlatformType = GamePlatform.VisualBoyAdvance, ArgumentTemplate = "{rom}",
            Executables = ["visualboyadvance-m.exe"], ProductNames = ["VisualBoyAdvance-M"], Systems = ["Game Boy", "Game Boy Color", "Game Boy Advance"], RomExtensions = [".gb", ".gbc", ".gba"]
        },
        new()
        {
            Id = "snes9x", Name = "Snes9x", PlatformType = GamePlatform.Snes9x, ArgumentTemplate = "{rom}",
            Executables = ["snes9x.exe"], ProductNames = ["Snes9x"], Systems = ["Super Nintendo"], RomExtensions = [".smc", ".sfc"]
        },
        new()
        {
            Id = "desmume", Name = "DeSmuME", PlatformType = GamePlatform.DeSmuME, ArgumentTemplate = "{rom}",
            Executables = ["DeSmuME.exe"], ProductNames = ["DeSmuME"], Systems = ["Nintendo DS"], RomExtensions = [".nds"]
        },
        new()
        {
            Id = "aethersx2", Name = "AetherSX2", PlatformType = GamePlatform.AetherSX2, ArgumentTemplate = "{rom}",
            Executables = ["aethersx2.exe"], ProductNames = ["AetherSX2"], Systems = ["PlayStation 2"], RomExtensions = [".iso", ".cso", ".chd", ".bin"]
        },
        new()
        {
            Id = "sameboy", Name = "SameBoy", PlatformType = GamePlatform.SameBoy, ArgumentTemplate = "{rom}",
            Executables = ["sameboy.exe"], ProductNames = ["SameBoy"], Systems = ["Game Boy", "Game Boy Color"], RomExtensions = [".gb", ".gbc"]
        }
    ];
}
