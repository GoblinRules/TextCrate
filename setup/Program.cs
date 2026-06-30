using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace TextCrate.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Any(arg => string.Equals(arg, "/uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            Uninstall();
            return;
        }

        Application.Run(new SetupForm());
    }

    private static void Uninstall()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TextCrate");
        var installPath = key?.GetValue("InstallLocation") as string;
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            MessageBox.Show("TextCrate installation was not found.", "TextCrate Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show("Remove TextCrate from this computer?", "TextCrate Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        foreach (var process in Process.GetProcessesByName("TextCrate"))
        {
            try { process.Kill(); } catch { }
        }

        using (var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
        {
            runKey?.DeleteValue("TextCrate", false);
        }

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var startMenuFolder = Path.Combine(programs, "TextCrate");
        if (Directory.Exists(startMenuFolder))
        {
            Directory.Delete(startMenuFolder, recursive: true);
        }

        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TextCrate", throwOnMissingSubKey: false);

        var currentInstaller = Environment.ProcessPath;
        var temporaryScript = Path.Combine(Path.GetTempPath(), $"TextCrate-Uninstall-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(temporaryScript, $"""
@echo off
timeout /t 1 /nobreak >nul
rmdir /s /q "{installPath}"
del "%~f0"
""");
        Process.Start(new ProcessStartInfo
        {
            FileName = temporaryScript,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        MessageBox.Show("TextCrate has been removed.", "TextCrate Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

internal sealed class SetupForm : Form
{
    private readonly TextBox _installLocation = new();
    private readonly CheckBox _startWithWindows = new();
    private readonly CheckBox _startAsAdmin = new();
    private readonly CheckBox _launchAfterInstall = new();
    private readonly Label _status = new();

    public SetupForm()
    {
        Text = "TextCrate Setup";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath) ?? SystemIcons.Application;
        ClientSize = new Size(620, 410);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
    }

    private void BuildLayout()
    {
        Controls.Add(new PictureBox
        {
            Image = Icon!.ToBitmap(),
            Location = new Point(24, 24),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.StretchImage
        });
        Controls.Add(new Label
        {
            Text = "TextCrate",
            Font = new Font("Segoe UI Semibold", 20F),
            Location = new Point(86, 25),
            Size = new Size(400, 42)
        });
        Controls.Add(new Label
        {
            Text = "Version 1.1.1 - Goblin Rules - ghostkernel.cc",
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(88, 67),
            Size = new Size(420, 24)
        });
        Controls.Add(new Label
        {
            Text = "Install location",
            Location = new Point(24, 122),
            Size = new Size(200, 24)
        });

        _installLocation.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "TextCrate");
        _installLocation.Location = new Point(24, 148);
        _installLocation.Size = new Size(470, 28);
        Controls.Add(_installLocation);

        var browse = new Button { Text = "Browse", Location = new Point(504, 146), Size = new Size(88, 30) };
        browse.Click += (_, _) => BrowseInstallLocation();
        Controls.Add(browse);

        _startWithWindows.Text = "Start TextCrate with Windows";
        _startWithWindows.Checked = true;
        _startWithWindows.Location = new Point(24, 196);
        _startWithWindows.Size = new Size(260, 28);
        Controls.Add(_startWithWindows);

        _startAsAdmin.Text = "Start as administrator when launching";
        _startAsAdmin.Location = new Point(24, 226);
        _startAsAdmin.Size = new Size(300, 28);
        Controls.Add(_startAsAdmin);

        _launchAfterInstall.Text = "Launch TextCrate after install";
        _launchAfterInstall.Checked = true;
        _launchAfterInstall.Location = new Point(24, 256);
        _launchAfterInstall.Size = new Size(260, 28);
        Controls.Add(_launchAfterInstall);

        Controls.Add(new Label
        {
            Text = "Unsigned build: Windows may show an unknown-publisher warning. Elevation requires a UAC prompt and should only be enabled if you need to type into administrator windows.",
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(24, 302),
            Size = new Size(568, 42)
        });

        _status.Location = new Point(24, 356);
        _status.Size = new Size(330, 24);
        _status.ForeColor = Color.FromArgb(71, 85, 105);
        Controls.Add(_status);

        var cancel = new Button { Text = "Cancel", Location = new Point(390, 352), Size = new Size(94, 34) };
        cancel.Click += (_, _) => Close();
        Controls.Add(cancel);

        var install = new Button
        {
            Text = "Install",
            Location = new Point(496, 352),
            Size = new Size(96, 34),
            BackColor = Color.FromArgb(14, 116, 144),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        install.Click += (_, _) => Install();
        Controls.Add(install);
    }

    private void BrowseInstallLocation()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where TextCrate should be installed",
            SelectedPath = _installLocation.Text
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installLocation.Text = dialog.SelectedPath;
        }
    }

    private void Install()
    {
        try
        {
            var installPath = _installLocation.Text.Trim();
            if (string.IsNullOrWhiteSpace(installPath))
            {
                MessageBox.Show(this, "Choose an install location.", "TextCrate Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _status.Text = "Installing...";
            Directory.CreateDirectory(installPath);
            ExtractPayload(installPath);
            CopyInstaller(installPath);
            WriteSettings(installPath);
            CreateStartMenuShortcut(installPath);
            RegisterUninstall(installPath);

            if (_launchAfterInstall.Checked)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(installPath, "TextCrate.exe"),
                    UseShellExecute = true
                });
            }

            _status.Text = "Installed.";
            MessageBox.Show(this, "TextCrate has been installed.", "TextCrate Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = "Install failed.";
            MessageBox.Show(this, ex.Message, "TextCrate Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExtractPayload(string installPath)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip")
            ?? throw new InvalidOperationException("Installer payload is missing.");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        archive.ExtractToDirectory(installPath, overwriteFiles: true);
    }

    private static void CopyInstaller(string installPath)
    {
        var currentPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
        {
            File.Copy(currentPath, Path.Combine(installPath, "TextCrate.Setup.exe"), overwrite: true);
        }
    }

    private void WriteSettings(string installPath)
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TextCrate");
        Directory.CreateDirectory(settingsDirectory);
        var settingsPath = Path.Combine(settingsDirectory, "settings.json");
        var settings = new
        {
            TypingMethod = 0,
            KeyDelayMs = 1,
            StartDelayMs = 150,
            ShowNotifications = true,
            StartWithWindows = _startWithWindows.Checked,
            StartAsAdmin = _startAsAdmin.Checked,
            ConfirmLargePaste = true,
            ConfirmLargePasteOver = 500,
            OcrCleanupMode = 1,
            EnhancedOcr = true,
            Theme = 0,
            HotKeyEnabled = true,
            HotKey = "V",
            HotKeyModifiers = 3,
            HotKeyMode = 0,
            ReadHotKeyEnabled = false,
            ReadHotKey = "R",
            ReadHotKeyModifiers = 3,
            LongTextRelayEnabled = false,
            LongTextRelayUseCustomEndpoint = false,
            LongTextRelayEndpoint = "https://qz9v4k.ghostkernel.cc",
            LongTextRelayExpiryMinutes = 15,
            LongTextRelayBurnAfterRead = true,
            LongTextRelayPromptForPassword = true,
            LongTextRelayOfferOver = 4000
        };
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));

        using var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (_startWithWindows.Checked)
        {
            runKey?.SetValue("TextCrate", $"\"{Path.Combine(installPath, "TextCrate.exe")}\"");
        }
        else
        {
            runKey?.DeleteValue("TextCrate", false);
        }
    }

    private static void CreateStartMenuShortcut(string installPath)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var folder = Path.Combine(programs, "TextCrate");
        Directory.CreateDirectory(folder);
        var shortcutPath = Path.Combine(folder, "TextCrate.lnk");
        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        var shortcut = shell!.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
        shortcut!.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [Path.Combine(installPath, "TextCrate.exe")]);
        shortcut.GetType().InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [installPath]);
        shortcut.GetType().InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, [Path.Combine(installPath, "TextCrate.exe")]);
        shortcut.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
    }

    private static void RegisterUninstall(string installPath)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TextCrate");
        var installerPath = Path.Combine(installPath, "TextCrate.Setup.exe");
        key.SetValue("DisplayName", "TextCrate");
        key.SetValue("DisplayVersion", "1.1.1");
        key.SetValue("Publisher", "Goblin Rules");
        key.SetValue("URLInfoAbout", "https://ghostkernel.cc");
        key.SetValue("InstallLocation", installPath);
        key.SetValue("DisplayIcon", Path.Combine(installPath, "TextCrate.exe"));
        key.SetValue("UninstallString", $"\"{installerPath}\" /uninstall");
        key.SetValue("QuietUninstallString", $"\"{installerPath}\" /uninstall");
        key.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
    }
}
