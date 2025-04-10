﻿using HandheldCompanion.Shared;
using System.Collections.Generic;
using System.Windows.Input;

namespace HandheldCompanion.Managers
{
    public static class ManagerFactory
    {
        public static SettingsManager settingsManager;
        public static DeviceManager deviceManager;
        public static LayoutManager layoutManager;
        public static MultimediaManager multimediaManager;
        public static HotkeysManager hotkeysManager;
        public static ProfileManager profileManager;
        public static PowerProfileManager powerProfileManager;
        public static ProcessManager processManager;
        public static GPUManager gpuManager;

        public static List<IManager> Managers => new()
        {
            settingsManager,
            deviceManager,
            layoutManager,
            multimediaManager,
            hotkeysManager,
            profileManager,
            powerProfileManager,
            processManager,
            gpuManager
        };

        static ManagerFactory()
        {
            // fix touch support
            // we need to call this before ANY ManagementClass objects across the whole app, good job NET team
            if (Tablet.TabletDevices.Count == 0)
                LogManager.LogError("No touch support detected!");

            // prepare managers
            settingsManager = new();
            deviceManager = new();
            layoutManager = new();
            multimediaManager = new();
            hotkeysManager = new();
            profileManager = new();
            powerProfileManager = new();
            processManager = new();
            gpuManager = new();
        }
    }
}
