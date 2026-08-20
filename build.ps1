param(
  [Parameter(Mandatory = $true)]
  [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$framework = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$enabledIconPath = Join-Path $projectDirectory "assets\TabWheelEnabled.ico"
$enabledTrayIconPath = Join-Path $projectDirectory "assets\enabled-png\icon32.png"
$disabledTrayIconPath = Join-Path $projectDirectory "assets\disabled-png\icon32.png"

if (-not (Test-Path -LiteralPath $compiler)) {
  throw "没有找到 .NET Framework C# 编译器：$compiler"
}

[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

$arguments = @(
  "/nologo",
  "/target:winexe",
  "/platform:anycpu",
  "/optimize+",
  "/win32icon:$enabledIconPath",
  "/resource:$enabledTrayIconPath,TabWheel.Enabled.png",
  "/resource:$disabledTrayIconPath,TabWheel.Disabled.png",
  "/out:$(Join-Path $outputPath 'TabWheel.exe')",
  "/reference:$(Join-Path $framework 'System.dll')",
  "/reference:$(Join-Path $framework 'System.Core.dll')",
  "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')",
  "/reference:$(Join-Path $framework 'System.Drawing.dll')",
  "/reference:$(Join-Path $framework 'Accessibility.dll')",
  (Join-Path $projectDirectory "Program.cs")
)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
  throw "编译失败，退出码：$LASTEXITCODE"
}

Get-Item -LiteralPath (Join-Path $outputPath "TabWheel.exe")
