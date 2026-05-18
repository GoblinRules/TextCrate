using System.Text.Json;

namespace TextCrate;

internal enum TypingMethod
{
    SendInput = 0,
    SendKeys = 1,
    ClipboardPaste = 2
}

internal enum OcrCleanupMode
{
    PlainText = 0,
    CodeAndEnvironmentText = 1
}

internal enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2
}

internal enum HotKeyMode
{
    TargetMode = 0,
    JustStartTyping = 1
}

[Flags]
internal enum HotKeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

internal sealed class AppSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TextCrate");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public TypingMethod TypingMethod { get; set; } = TypingMethod.SendInput;
    public int KeyDelayMs { get; set; } = 1;
    public int StartDelayMs { get; set; } = 150;
    public bool ShowNotifications { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool StartAsAdmin { get; set; }
    public bool ConfirmLargePaste { get; set; } = true;
    public int ConfirmLargePasteOver { get; set; } = 500;
    public OcrCleanupMode OcrCleanupMode { get; set; } = OcrCleanupMode.CodeAndEnvironmentText;
    public bool EnhancedOcr { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool HotKeyEnabled { get; set; } = true;
    public string HotKey { get; set; } = "V";
    public HotKeyModifiers HotKeyModifiers { get; set; } = HotKeyModifiers.Alt | HotKeyModifiers.Control;
    public HotKeyMode HotKeyMode { get; set; } = HotKeyMode.TargetMode;

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        StartupRegistration.SetEnabled(StartWithWindows);
    }
}
