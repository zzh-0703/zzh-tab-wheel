param(
  [Parameter(Mandatory = $true)]
  [int]$ProcessId,

  [int]$EventsPerSecond = 1000,

  [int]$DurationSeconds = 8,

  [string]$ResultPath = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms,System.Drawing

if (-not ("TabWheelStress.NativeInput" -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace TabWheelStress
{
    public static class NativeInput
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeyboardInput Keyboard;
            [FieldOffset(0)] public HardwareInput Hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public uint Message;
            public ushort ParameterLow;
            public ushort ParameterHigh;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr window);

        private const uint InputMouse = 0;
        private const uint MouseEventWheel = 0x0800;

        public static uint SendWheelBurst(int count)
        {
            Input[] inputs = new Input[count];
            for (int index = 0; index < count; index++)
            {
                inputs[index].Type = InputMouse;
                inputs[index].Data.Mouse.MouseData = 120;
                inputs[index].Data.Mouse.Flags = MouseEventWheel;
            }

            return SendInput((uint)count, inputs, Marshal.SizeOf(typeof(Input)));
        }
    }
}
'@
}

$target = Get-Process -Id $ProcessId
$originalCursor = [System.Windows.Forms.Cursor]::Position
$originalForeground = [TabWheelStress.NativeInput]::GetForegroundWindow()
$form = [System.Windows.Forms.Form]::new()
$form.Text = "TabWheel CPU 压力测试"
$form.Size = [System.Drawing.Size]::new(340, 160)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedToolWindow"
$form.ShowInTaskbar = $false
$form.TopMost = $true
$label = [System.Windows.Forms.Label]::new()
$label.Dock = "Fill"
$label.TextAlign = "MiddleCenter"
$label.Text = "高频滚轮测试：$EventsPerSecond 次/秒"
$form.Controls.Add($label)

$result = $null
try {
  $form.Show()
  $form.Activate()
  [System.Windows.Forms.Application]::DoEvents()
  [System.Windows.Forms.Cursor]::Position = $form.PointToScreen([System.Drawing.Point]::new(170, 80))
  Start-Sleep -Milliseconds 250

  $target.Refresh()
  $cpuStart = $target.CPU
  $privateStart = $target.PrivateMemorySize64
  $workingSetStart = $target.WorkingSet64
  $watch = [System.Diagnostics.Stopwatch]::StartNew()
  $sent = 0
  $intervalMilliseconds = 50
  $burstSize = [Math]::Max(1, [Math]::Round($EventsPerSecond * $intervalMilliseconds / 1000))
  $iterations = [Math]::Max(1, [Math]::Round($DurationSeconds * 1000 / $intervalMilliseconds))

  for ($iteration = 0; $iteration -lt $iterations; $iteration++) {
    $sent += [TabWheelStress.NativeInput]::SendWheelBurst($burstSize)
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds $intervalMilliseconds
  }

  $watch.Stop()
  Start-Sleep -Milliseconds 250
  $after = Get-Process -Id $ProcessId
  $cpuDelta = $after.CPU - $cpuStart
  $oneCorePercent = 100 * $cpuDelta / $watch.Elapsed.TotalSeconds
  $machinePercent = $oneCorePercent / [Environment]::ProcessorCount

  $result = [pscustomobject]@{
    EventsSent = $sent
    SampleSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 3)
    CpuDeltaSeconds = [Math]::Round($cpuDelta, 5)
    CpuPercentOneCore = [Math]::Round($oneCorePercent, 4)
    CpuPercentMachine = [Math]::Round($machinePercent, 4)
    WorkingSetChangeMb = [Math]::Round(($after.WorkingSet64 - $workingSetStart) / 1MB, 3)
    PrivateMemoryChangeMb = [Math]::Round(($after.PrivateMemorySize64 - $privateStart) / 1MB, 3)
    WorkingSetMb = [Math]::Round($after.WorkingSet64 / 1MB, 3)
    PrivateMemoryMb = [Math]::Round($after.PrivateMemorySize64 / 1MB, 3)
  }
}
finally {
  $form.Close()
  $form.Dispose()
  [System.Windows.Forms.Cursor]::Position = $originalCursor
  [TabWheelStress.NativeInput]::SetForegroundWindow($originalForeground) | Out-Null
}

if ($null -eq $result) {
  throw "压力测试没有生成结果。"
}

$json = $result | ConvertTo-Json -Compress
if (-not [String]::IsNullOrWhiteSpace($ResultPath)) {
  [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($ResultPath), $json)
}
Write-Output $json
