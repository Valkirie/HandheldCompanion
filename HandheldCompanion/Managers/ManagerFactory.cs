namespace HandheldCompanion.Managers
{
    public static class ManagerFactory
    {
        public static SettingsManager settingsManager = new();
        public static DeviceManager deviceManager = new();
        public static LayoutManager layoutManager = new();
        public static MultimediaManager multimediaManager = new();
        public static HotkeysManager hotkeysManager = new();
        public static ProfileManager profileManager = new();
    }
}
