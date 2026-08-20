<p align="right"><a href="README_zh-CN.md">简体中文</a> · <strong>English</strong></p>

<p align="center">
  <img src="assets/enabled-png/icon128.png" width="96" alt="TabWheel icon">
</p>

<h1 align="center">TabWheel for Windows</h1>

<p align="center">
  Switch Chromium tabs by scrolling over the browser's <strong>native top tab strip</strong>.
  <br>
  No browser extension required.
</p>

<p align="center">
  <a href="https://github.com/zzh-0703/zzh-tab-wheel/releases/latest"><img alt="Latest release" src="https://img.shields.io/badge/release-v0.2.1-6750A4"></a>
  <img alt="Windows 10 and 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-2EA44F"></a>
  <img alt="Chrome extension not required" src="https://img.shields.io/badge/Chrome%20extension-not%20required-4285F4">
</p>

<p align="center">
  <strong><a href="https://github.com/zzh-0703/zzh-tab-wheel/releases/download/v0.2.1/TabWheel-Windows-v0.2.1.zip">Download ZIP</a></strong>
  · <a href="https://github.com/zzh-0703/zzh-tab-wheel/releases/download/v0.2.1/TabWheel.exe">Download EXE</a>
  · <a href="releases/v0.2.1/SHA256SUMS.txt">SHA-256 checksums</a>
</p>

<p align="center">
  <img src="assets/readme/tabwheel-demo.gif" width="900" alt="Scrolling over Chrome's top tab strip switches tabs">
</p>

## Why TabWheel?

- **Works only over the top tab strip.** Scrolling on web pages, the address bar, games, and other applications is left untouched.
- **No Chrome extension.** TabWheel is a standalone Windows tray utility.
- **Sleeps outside supported browsers.** The low-level mouse hook is removed while a game or another application is in the foreground and restored when you return.
- **Clear tray state.** A blue-purple icon means enabled; a gray slashed icon means manually disabled.
- **Designed for high-rate wheels.** Tab-strip detection is cached for about 50 ms and tab switching is limited to one action every 120 ms.

## Quick start

1. Download and extract [TabWheel-Windows-v0.2.1.zip](https://github.com/zzh-0703/zzh-tab-wheel/releases/download/v0.2.1/TabWheel-Windows-v0.2.1.zip).
2. Run `TabWheel.exe`.
3. Move the pointer over a browser tab or an empty part of the top tab strip.
4. Scroll down for the next tab or up for the previous tab.

Right-click the tray icon to enable or disable TabWheel, reverse the direction, configure startup with Windows, or exit. Double-click the icon to quickly toggle the enabled state.

> [!NOTE]
> The executable is not code-signed yet, so Windows SmartScreen may show an "Unknown publisher" warning. You can verify the SHA-256 checksums or build the executable directly from source.

## Supported browsers

| Browser | Standard horizontal top tab strip |
| --- | :---: |
| Google Chrome | ✅ |
| Microsoft Edge | ✅ |
| Brave | ✅ |
| Vivaldi | ✅ |
| Opera | ✅ |

Vertical tabs are not supported in the current release.

## Tray states

| State | Icon | Behavior |
| --- | --- | --- |
| Enabled | <img src="assets/enabled-png/icon32.png" width="24" alt="Enabled"> | Active while a supported browser is in the foreground; automatically sleeps elsewhere |
| Disabled | <img src="assets/disabled-png/icon32.png" width="24" alt="Disabled"> | Manually disabled; foreground and mouse hooks are removed |

## Privacy and safety boundaries

- Listens only for mouse-wheel events; it does not record keys, page content, tab titles, or URLs.
- Installs the mouse-wheel hook only while a supported browser is in the foreground.
- Consumes the wheel event only when the pointer is over the native top tab strip.
- Switches tabs with the browser's native `Ctrl+PageUp` and `Ctrl+PageDown` shortcuts.
- Stores settings only in `%LocalAppData%\TabWheel\settings.ini`.
- Adds the current executable to the current user's Windows `Run` entry only when startup is explicitly enabled from the tray menu.

## How detection works

TabWheel first uses Windows Accessibility (`IAccessible`) to determine whether the pointer is over a page-tab control. Empty tab-strip space has no dedicated accessibility element, so a small fallback region at the top of the browser window is used while excluding address-bar controls and window buttons.

Results for the same pointer position are cached for roughly 50 ms. Tab switching is throttled to 120 ms, which limits the trigger rate to about eight switches per second.

## Performance

Measurements below were taken on a test machine with 12 logical processors:

| Scenario | Observed TabWheel result |
| --- | --- |
| Idle | CPU delta at the measurement floor |
| 3,000 synthetic wheel events while a non-browser app is foreground | Mouse hook unloaded; CPU delta at the measurement floor |
| High-rate scrolling over a browser tab strip | ~50 ms detection cache and at most ~8 tab switches per second |

Accumulated process CPU time naturally increases over the lifetime of any process; it does not mean the real-time CPU rate grows over time. TabWheel has no event queue or cache that grows with wheel usage.

## System requirements

- Windows 10 or Windows 11
- .NET Framework 4.8, normally included with supported Windows versions
- A standard horizontal top tab strip

## Build from source

Run the following command in 64-bit Windows PowerShell:

```powershell
.\build.ps1 -OutputDirectory .\dist
```

The build uses the .NET Framework C# compiler included with Windows and downloads no third-party dependencies.

Run the built-in checks with:

```powershell
.\dist\TabWheel.exe --self-test
.\dist\TabWheel.exe --smoke-test
.\dist\TabWheel.exe --state-smoke-test
```

## Contributing and security

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting a change.
- Use the [bug report](https://github.com/zzh-0703/zzh-tab-wheel/issues/new?template=bug-report.yml) or [feature request](https://github.com/zzh-0703/zzh-tab-wheel/issues/new?template=feature-request.yml) form.
- Do not disclose security vulnerabilities in a public issue. Follow [SECURITY.md](SECURITY.md) to report them privately.
- A mainland China mirror is available on [Gitee](https://gitee.com/zhang-zihao990703/zzh-tab-wheel).

## License

Copyright © 2026 Zhang Zihao (章梓昊).

TabWheel is released under the [MIT License](LICENSE).
