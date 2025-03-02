using RedLoader;

namespace Force_Remove_Logs;

public static class Config
{
    public static ConfigCategory Category { get; private set; }
    public static ConfigEntry<bool> ConsoleLogging { get; private set; }
    //public static ConfigEntry<bool> SomeEntry { get; private set; }

    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("Force_Remove_Logs", "Force_Remove_Logs", "Force_Remove_Logs.cfg");

        ConsoleLogging = Category.CreateEntry(
            "ConsoleLogging",
            false,
            "Enable Logging (for debugging only!)",
            "Will log nearly everything the mod does into the log files");
    }

    // Same as the callback in "CreateSettings". Called when the settings ui is closed.
    public static void OnSettingsUiClosed()
    {
    }
}