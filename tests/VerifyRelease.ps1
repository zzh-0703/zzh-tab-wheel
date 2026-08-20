param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$BuiltExecutable,

  [Parameter(Mandatory = $true)]
  [string]$ReleaseDirectory
)

$ErrorActionPreference = "Stop"

$expectedFileVersion = "$Version.0"
$builtExecutablePath = [System.IO.Path]::GetFullPath($BuiltExecutable)
$releaseDirectoryPath = [System.IO.Path]::GetFullPath($ReleaseDirectory)
$releaseExecutablePath = Join-Path $releaseDirectoryPath "TabWheel.exe"
$archivePath = Join-Path $releaseDirectoryPath "TabWheel-Windows-v$Version.zip"
$checksumPath = Join-Path $releaseDirectoryPath "SHA256SUMS.txt"
$releaseNotesPath = Join-Path $releaseDirectoryPath "RELEASE_NOTES.md"

foreach ($requiredPath in @(
  $builtExecutablePath,
  $releaseExecutablePath,
  $archivePath,
  $checksumPath,
  $releaseNotesPath
)) {
  if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
    throw "缺少发布验证文件：$requiredPath"
  }
}

function Assert-FileVersion([string]$path) {
  $actualVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion
  if ($actualVersion -ne $expectedFileVersion) {
    throw "版本不匹配：$path，期望 $expectedFileVersion，实际 $actualVersion"
  }
}

function Invoke-ExecutableCheck([string]$path, [string]$argument) {
  $process = Start-Process -FilePath $path -ArgumentList $argument -WindowStyle Hidden -Wait -PassThru
  if ($process.ExitCode -ne 0) {
    throw "运行检查失败：$path $argument，退出码 $($process.ExitCode)"
  }
}

Assert-FileVersion $builtExecutablePath
Assert-FileVersion $releaseExecutablePath

foreach ($executable in @($builtExecutablePath, $releaseExecutablePath)) {
  foreach ($argument in @("--self-test", "--smoke-test", "--state-smoke-test")) {
    Invoke-ExecutableCheck $executable $argument
  }
}

$checksumEntries = @{}
foreach ($line in Get-Content -LiteralPath $checksumPath) {
  if ($line -notmatch '^([0-9A-Fa-f]{64})\s{2}(.+)$') {
    throw "SHA256SUMS.txt 格式无效：$line"
  }

  $checksumEntries[$Matches[2]] = $Matches[1].ToUpperInvariant()
}

foreach ($fileName in @("TabWheel.exe", "TabWheel-Windows-v$Version.zip")) {
  if (-not $checksumEntries.ContainsKey($fileName)) {
    throw "SHA256SUMS.txt 缺少：$fileName"
  }

  $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $releaseDirectoryPath $fileName)).Hash
  if ($actualHash -ne $checksumEntries[$fileName]) {
    throw "SHA-256 不匹配：$fileName"
  }
}

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$extractDirectory = Join-Path $temporaryRoot ("TabWheel-verify-" + [Guid]::NewGuid().ToString("N"))
$extractDirectoryPath = [System.IO.Path]::GetFullPath($extractDirectory)

if (-not $extractDirectoryPath.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
  throw "临时目录不在系统临时目录内：$extractDirectoryPath"
}

try {
  Expand-Archive -LiteralPath $archivePath -DestinationPath $extractDirectoryPath

  foreach ($entryName in @(
    "TabWheel.exe",
    "README.md",
    "README_zh-CN.md",
    "LICENSE",
    "assets\enabled-png\icon128.png",
    "assets\readme\tabwheel-demo.gif"
  )) {
    if (-not (Test-Path -LiteralPath (Join-Path $extractDirectoryPath $entryName) -PathType Leaf)) {
      throw "发布 ZIP 缺少：$entryName"
    }
  }

  $archiveExecutableHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $extractDirectoryPath "TabWheel.exe")).Hash
  $releaseExecutableHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $releaseExecutablePath).Hash
  if ($archiveExecutableHash -ne $releaseExecutableHash) {
    throw "ZIP 内的 TabWheel.exe 与独立发布文件不一致。"
  }
} finally {
  if (Test-Path -LiteralPath $extractDirectoryPath) {
    Remove-Item -LiteralPath $extractDirectoryPath -Recurse -Force
  }
}

Write-Output "TabWheel v$Version 发布验证通过。"
