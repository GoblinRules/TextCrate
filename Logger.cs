namespace TextCrate;

internal static class Logger
{
    private static readonly object Lock = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TextCrate");

    public static readonly string LogPath = Path.Combine(LogDirectory, "TextCrate.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message} {exception.GetType().Name}: {exception.Message}");
    }

    public static void EnsureLogFile()
    {
        Directory.CreateDirectory(LogDirectory);
        if (!File.Exists(LogPath))
        {
            File.WriteAllText(LogPath, string.Empty);
        }
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
            lock (Lock)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never break tray operation.
        }
    }
}
