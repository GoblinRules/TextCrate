namespace TextCrate;

static class Program
{
    [STAThread]
    static void Main()
    {
        var settings = AppSettings.Load();
        if (settings.StartAsAdmin && !ElevationService.IsAdministrator())
        {
            if (ElevationService.RelaunchAsAdministrator())
            {
                return;
            }
        }

        using var mutex = new Mutex(false, "7D9E5B6A-09F2-4744-9A48-92DA4F5F37A4", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        Native.SetProcessDpiAware();
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }    
}
