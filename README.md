# TextCrate

TextCrate is a Windows tray utility by **Goblin Rules** for working with remote VMs, browser consoles, and systems where shared clipboard is unreliable or unavailable.

It has two core jobs:

- Type your local clipboard into a selected remote window.
- Read text from a selected screen region with OCR and copy it back to your clipboard.

Website: [ghostkernel.cc](https://ghostkernel.cc)

## Install

Release builds include three options:

- `TextCrate-vX.Y.Z-win-x64-setup.exe` - EXE installer with install location, startup, admin-launch, and launch-now options.
- `TextCrate-vX.Y.Z-win-x64.msi` - MSI installer.
- `TextCrate-vX.Y.Z-win-x64-portable.zip` - portable folder, no installer.

The builds are unsigned. Windows may show an unknown-publisher warning until TextCrate is code-signed in a future release.

## Usage

- Left-click the tray icon, then click a target window to type the current clipboard text into it.
- Right-click the tray icon for OCR, settings, cancel, relaunch as administrator, and exit.
- Choose **Read screen area to clipboard**, then drag a rectangle over text on screen.
- Press `Esc` while choosing a target or OCR area to cancel.

TextCrate uses bundled Tesseract English OCR first, then falls back to Windows OCR if needed. Enhanced OCR is enabled by default and runs extra internal passes for small UI text, table rows, colored status pills, times, ports, code, and `.env` style text without requiring mode changes.

## Settings

- **Theme**: Use system, light, or dark. Applies to settings and the tray menu.
- **Typing method**:
  - SendInput scan codes: best default for VM consoles.
  - SendKeys compatibility: fallback for normal Windows apps.
  - Clipboard paste: uses Ctrl+V when the target supports clipboard paste.
- **Delay between keys**: slows typing for remote consoles that drop characters.
- **Start delay**: waits after selecting the target before typing starts.
- **OCR cleanup**: plain text or code / `.env` cleanup.
- **Enhanced OCR**: automatic extra OCR passes for small UI and dashboard text.
- **Show completion notifications**: enables or disables system notifications.
- **Start with Windows**: registers TextCrate under the current user's startup apps.
- **Start as administrator when launching**: relaunches with UAC elevation on startup.
- **Confirm large paste operations**: asks before typing clipboard text over the configured character limit.
- **Paste hotkey**: default is `Ctrl+Alt+V`, matching the original ClickPaste behavior.

## Help

### Text is typed into the wrong place

Use target mode: left-click the tray icon, then click the remote console or input area. For the hotkey, set **Hotkey action** to **Choose target window**.

### Some characters are missed

Increase **Delay between keys**. Remote/browser VM consoles can drop characters when input is too fast.

### Text does not type into an administrator window

Use **Relaunch as administrator** from the tray menu, or enable **Start as administrator when launching** in settings.

### OCR misses small dashboard text

Keep **Enhanced OCR** enabled and select only the smallest useful screen area. Very tiny or low-contrast text may still need a tighter selection box.

### Hotkey does not register

Another app or Windows may already own that shortcut. TextCrate warns when registration fails. Pick a different key/modifier combination.

## Build

```powershell
dotnet build
```

Build release artifacts:

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-release.ps1
```

Artifacts are written to `artifacts/`.

## License

TextCrate is licensed under the BSD 3-Clause License. See [LICENSE](LICENSE).

Third-party notices are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
