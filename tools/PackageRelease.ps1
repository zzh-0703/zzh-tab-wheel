param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^\d+\.\d+\.\d+$')]
  [string]$Version
)

$ErrorActionPreference = "Stop"

$repositoryDirectory = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$releaseRoot = Join-Path $repositoryDirectory "releases"
$releaseDirectory = Join-Path $releaseRoot "v$Version"

if (Test-Path -LiteralPath $releaseDirectory) {
  throw "发布目录已存在，不会覆盖：$releaseDirectory"
}

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$workingDirectory = Join-Path $temporaryRoot ("TabWheel-package-" + [Guid]::NewGuid().ToString("N"))
$workingDirectoryPath = [System.IO.Path]::GetFullPath($workingDirectory)

if (-not $workingDirectoryPath.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
  throw "临时目录不在系统临时目录内：$workingDirectoryPath"
}

try {
  $buildDirectory = Join-Path $workingDirectoryPath "build"
  $packageDirectory = Join-Path $workingDirectoryPath "package"
  New-Item -ItemType Directory -Path $buildDirectory, $packageDirectory | Out-Null

  & (Join-Path $repositoryDirectory "build.ps1") -OutputDirectory $buildDirectory
  if ($LASTEXITCODE -ne 0) {
    throw "构建失败，退出码：$LASTEXITCODE"
  }

  $builtExecutable = Join-Path $buildDirectory "TabWheel.exe"
  $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($builtExecutable).FileVersion
  if ($fileVersion -ne "$Version.0") {
    throw "构建版本不匹配：期望 $Version.0，实际 $fileVersion"
  }

  Copy-Item -LiteralPath $builtExecutable -Destination (Join-Path $packageDirectory "TabWheel.exe")
  Copy-Item -LiteralPath (Join-Path $repositoryDirectory "README.md") -Destination $packageDirectory
  Copy-Item -LiteralPath (Join-Path $repositoryDirectory "README_zh-CN.md") -Destination $packageDirectory
  Copy-Item -LiteralPath (Join-Path $repositoryDirectory "LICENSE") -Destination $packageDirectory
  Copy-Item -LiteralPath (Join-Path $repositoryDirectory "assets") -Destination $packageDirectory -Recurse

  New-Item -ItemType Directory -Path $releaseDirectory | Out-Null
  Copy-Item -LiteralPath $builtExecutable -Destination (Join-Path $releaseDirectory "TabWheel.exe")

  $archivePath = Join-Path $releaseDirectory "TabWheel-Windows-v$Version.zip"
  Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $archivePath -CompressionLevel Optimal

  $checksumLines = @(
    "$(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $releaseDirectory 'TabWheel.exe') | Select-Object -ExpandProperty Hash)  TabWheel.exe",
    "$(Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath | Select-Object -ExpandProperty Hash)  TabWheel-Windows-v$Version.zip"
  )

  [System.IO.File]::WriteAllLines(
    (Join-Path $releaseDirectory "SHA256SUMS.txt"),
    $checksumLines,
    [System.Text.UTF8Encoding]::new($false)
  )

  Get-ChildItem -LiteralPath $releaseDirectory -File
} finally {
  if (Test-Path -LiteralPath $workingDirectoryPath) {
    Remove-Item -LiteralPath $workingDirectoryPath -Recurse -Force
  }
}
