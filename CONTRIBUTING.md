# Contributing to TabWheel

Thanks for helping improve TabWheel. Changes should preserve its core safety boundary: mouse-wheel events are consumed only while the pointer is over a supported browser's native horizontal top tab strip.

## Development environment

- Windows 10 or Windows 11
- 64-bit Windows PowerShell
- .NET Framework 4.8
- At least one supported Chromium browser for manual testing

No package restore is required. Build with:

```powershell
.\build.ps1 -OutputDirectory .\dist
```

## Required checks

Run all built-in checks before opening a pull request:

```powershell
.\dist\TabWheel.exe --self-test
.\dist\TabWheel.exe --smoke-test
.\dist\TabWheel.exe --state-smoke-test
```

For changes to browser detection or mouse handling, also verify manually that:

- scrolling over a browser tab or empty tab-strip space switches tabs;
- scrolling over a web page or the address bar remains unchanged;
- the mouse hook is removed while another application is in the foreground;
- disabling and re-enabling the tray application does not leak icon or hook handles.

## Pull requests

- Keep each pull request focused on one behavior or fix.
- Explain the user-visible change and how it was tested.
- Do not commit personal settings, browser profiles, recordings with private data, or unsigned binaries outside the versioned release directory.
- Do not change assembly versions or create release tags unless the change is part of a coordinated release.

Security vulnerabilities must not be reported through a public issue. Follow [SECURITY.md](SECURITY.md) instead.
