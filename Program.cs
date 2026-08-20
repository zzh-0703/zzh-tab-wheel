using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Accessibility;
using Microsoft.Win32;

[assembly: AssemblyTitle("TabWheel")]
[assembly: AssemblyDescription("Scroll over the Chrome tab strip to switch tabs")]
[assembly: AssemblyCompany("TabWheel")]
[assembly: AssemblyProduct("TabWheel")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Zhang Zihao (章梓昊)")]
[assembly: AssemblyVersion("0.2.1.0")]
[assembly: AssemblyFileVersion("0.2.1.0")]

namespace TabWheel
{
    internal static class Program
    {
        private const string MutexName = "Local\\TabWheel-8B95D857-0F52-4F74-95AB-812688A33973";

        [STAThread]
        private static void Main(string[] arguments)
        {
            DpiAwareness.Enable();

            if (arguments.Length == 1 &&
                String.Equals(arguments[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = SelfTest.Run() ? 0 : 1;
                return;
            }

            bool stateSmokeTest = arguments.Length == 1 &&
                String.Equals(arguments[0], "--state-smoke-test", StringComparison.OrdinalIgnoreCase);
            int testLifetimeMilliseconds = stateSmokeTest ? 3500 : ParseTestLifetime(arguments);
            if (testLifetimeMilliseconds > 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TabWheelApplicationContext(testLifetimeMilliseconds, stateSmokeTest));
                return;
            }

            bool ownsMutex;
            using (Mutex mutex = new Mutex(true, MutexName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    MessageBox.Show(
                        "TabWheel 已经在运行，请查看系统托盘。",
                        "TabWheel",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TabWheelApplicationContext(0, false));
            }
        }

        private static int ParseTestLifetime(string[] arguments)
        {
            if (arguments.Length != 1)
            {
                return 0;
            }

            if (String.Equals(arguments[0], "--smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                return 600;
            }

            const string prefix = "--test-duration-ms=";
            if (!arguments[0].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            int duration;
            if (!Int32.TryParse(arguments[0].Substring(prefix.Length), out duration))
            {
                return 0;
            }

            return Math.Max(100, Math.Min(duration, 600000));
        }
    }

    internal sealed class TabWheelApplicationContext : ApplicationContext
    {
        private readonly AppSettings settings;
        private readonly NotifyIcon trayIcon;
        private readonly ToolStripMenuItem enabledItem;
        private readonly ToolStripMenuItem reverseItem;
        private readonly ToolStripMenuItem startupItem;
        private readonly MouseWheelHook hook;
        private readonly ForegroundWindowMonitor foregroundMonitor;
        private readonly BrowserWindowClassifier browserClassifier;
        private readonly TabStripDetector detector;
        private readonly Icon enabledIcon;
        private readonly Icon disabledIcon;
        private readonly System.Windows.Forms.Timer testLifetimeTimer;
        private readonly System.Windows.Forms.Timer stateTestTimer;
        private readonly bool testMode;
        private readonly bool initialEnabledState;
        private readonly int stateTestHandleBaseline;
        private int stateTestTogglesRemaining;
        private int accumulatedDelta;
        private long lastWheelTick;
        private long lastSwitchTick;
        private IntPtr accumulationWindow;
        private IntPtr activeBrowserWindow;

        public TabWheelApplicationContext(int testLifetimeMilliseconds, bool stateSmokeTest)
        {
            testMode = testLifetimeMilliseconds > 0;
            settings = AppSettings.Load();
            initialEnabledState = settings.Enabled;
            browserClassifier = new BrowserWindowClassifier();
            detector = new TabStripDetector();
            hook = new MouseWheelHook(HandleWheel);
            foregroundMonitor = new ForegroundWindowMonitor(ForegroundWindowChanged);
            enabledIcon = IconResources.Load("TabWheel.Enabled.png");
            disabledIcon = IconResources.Load("TabWheel.Disabled.png");

            enabledItem = new ToolStripMenuItem("启用标签栏滚轮切换");
            enabledItem.Checked = settings.Enabled;
            enabledItem.CheckOnClick = true;
            enabledItem.Click += EnabledItemClick;

            reverseItem = new ToolStripMenuItem("反转滚轮方向");
            reverseItem.Checked = settings.ReverseDirection;
            reverseItem.CheckOnClick = true;
            reverseItem.Click += ReverseItemClick;

            startupItem = new ToolStripMenuItem("开机自动启动");
            startupItem.Checked = StartupManager.IsEnabled();
            startupItem.CheckOnClick = true;
            startupItem.Click += StartupItemClick;

            ToolStripMenuItem aboutItem = new ToolStripMenuItem("使用说明");
            aboutItem.Click += AboutItemClick;

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += ExitItemClick;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(enabledItem);
            menu.Items.Add(reverseItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(aboutItem);
            menu.Items.Add(exitItem);

            trayIcon = new NotifyIcon();
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += TrayIconDoubleClick;
            UpdateTrayState();

            try
            {
                ApplyEnabledState(settings.Enabled, false);
                if (testLifetimeMilliseconds > 0)
                {
                    testLifetimeTimer = new System.Windows.Forms.Timer();
                    testLifetimeTimer.Interval = testLifetimeMilliseconds;
                    testLifetimeTimer.Tick += TestLifetimeTimerTick;
                    testLifetimeTimer.Start();
                }
                if (stateSmokeTest)
                {
                    stateTestHandleBaseline = Process.GetCurrentProcess().HandleCount;
                    stateTestTogglesRemaining = 200;
                    stateTestTimer = new System.Windows.Forms.Timer();
                    stateTestTimer.Interval = 10;
                    stateTestTimer.Tick += StateTestTimerTick;
                    stateTestTimer.Start();
                }
            }
            catch (Exception exception)
            {
                ApplyEnabledState(false, true);
                MessageBox.Show(
                    "无法启动窗口或鼠标监听，程序已暂停：" + exception.Message,
                    "TabWheel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void TestLifetimeTimerTick(object sender, EventArgs eventArgs)
        {
            testLifetimeTimer.Stop();
            ExitThread();
        }

        private void StateTestTimerTick(object sender, EventArgs eventArgs)
        {
            if (stateTestTogglesRemaining > 0)
            {
                ApplyEnabledState(!settings.Enabled, false);
                stateTestTogglesRemaining--;
                return;
            }

            stateTestTimer.Stop();
            ApplyEnabledState(initialEnabledState, false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            int handlesAfter = Process.GetCurrentProcess().HandleCount;
            if (handlesAfter > stateTestHandleBaseline + 20)
            {
                Environment.ExitCode = 1;
            }
        }

        private bool HandleWheel(NativeMethods.Point point, int delta)
        {
            if (!settings.Enabled || activeBrowserWindow == IntPtr.Zero || ModifierKeysPressed())
            {
                ResetAccumulation();
                return false;
            }

            IntPtr chromeWindow = activeBrowserWindow;
            if (!detector.IsTabStripPoint(point, chromeWindow))
            {
                ResetAccumulation();
                return false;
            }

            long now = Environment.TickCount & Int32.MaxValue;
            if (chromeWindow != accumulationWindow || now - lastWheelTick > 320)
            {
                accumulatedDelta = 0;
            }

            if ((accumulatedDelta > 0 && delta < 0) || (accumulatedDelta < 0 && delta > 0))
            {
                accumulatedDelta = 0;
            }

            accumulationWindow = chromeWindow;
            lastWheelTick = now;
            accumulatedDelta += delta;

            if (Math.Abs(accumulatedDelta) < NativeMethods.WheelDelta || now - lastSwitchTick < 120)
            {
                return true;
            }

            bool nextTab = accumulatedDelta < 0;
            if (settings.ReverseDirection)
            {
                nextTab = !nextTab;
            }

            accumulatedDelta = 0;
            lastSwitchTick = now;
            return KeyboardSender.SwitchChromeTab(nextTab);
        }

        private static bool ModifierKeysPressed()
        {
            return NativeMethods.IsKeyDown(NativeMethods.VirtualKeyControl) ||
                   NativeMethods.IsKeyDown(NativeMethods.VirtualKeyShift) ||
                   NativeMethods.IsKeyDown(NativeMethods.VirtualKeyMenu) ||
                   NativeMethods.IsKeyDown(NativeMethods.VirtualKeyLeftWindows) ||
                   NativeMethods.IsKeyDown(NativeMethods.VirtualKeyRightWindows);
        }

        private void ResetAccumulation()
        {
            accumulatedDelta = 0;
            accumulationWindow = IntPtr.Zero;
        }

        private void EnabledItemClick(object sender, EventArgs eventArgs)
        {
            ApplyEnabledState(enabledItem.Checked, true);
        }

        private void ReverseItemClick(object sender, EventArgs eventArgs)
        {
            settings.ReverseDirection = reverseItem.Checked;
            settings.Save();
        }

        private void StartupItemClick(object sender, EventArgs eventArgs)
        {
            try
            {
                StartupManager.SetEnabled(startupItem.Checked);
            }
            catch (Exception exception)
            {
                startupItem.Checked = !startupItem.Checked;
                MessageBox.Show(
                    "无法修改开机启动设置：" + exception.Message,
                    "TabWheel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void AboutItemClick(object sender, EventArgs eventArgs)
        {
            MessageBox.Show(
                "用法：让 Chrome 保持当前窗口，把鼠标放在标签页或标签栏空白处，滚动滚轮。\r\n\r\n" +
                "向下滚：下一个标签\r\n" +
                "向上滚：上一个标签\r\n\r\n" +
                "程序只在前台 Chrome/Edge/Brave/Vivaldi/Opera 的顶部标签栏响应，普通网页区域不会拦截滚轮。" +
                "游戏或其他程序位于前台时，鼠标监听会自动休眠。双击托盘图标可以快速启用或暂停。\r\n\r\n" +
                "TabWheel 0.2.1 · 不需要安装 Chrome 扩展",
                "TabWheel 使用说明",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void TrayIconDoubleClick(object sender, EventArgs eventArgs)
        {
            ApplyEnabledState(!settings.Enabled, true);
        }

        private void ApplyEnabledState(bool enabled, bool persist)
        {
            Exception enableFailure = null;
            if (enabled)
            {
                try
                {
                    settings.Enabled = true;
                    foregroundMonitor.Start();
                }
                catch (Exception exception)
                {
                    enableFailure = exception;
                    settings.Enabled = false;
                    foregroundMonitor.Stop();
                    hook.Stop();
                    activeBrowserWindow = IntPtr.Zero;
                    detector.ResetCache();
                    ResetAccumulation();
                }
            }
            else
            {
                settings.Enabled = false;
                foregroundMonitor.Stop();
                hook.Stop();
                activeBrowserWindow = IntPtr.Zero;
                detector.ResetCache();
                ResetAccumulation();
            }

            enabledItem.Checked = settings.Enabled;
            UpdateTrayState();
            if (persist)
            {
                settings.Save();
            }

            if (enableFailure != null)
            {
                if (testMode)
                {
                    Environment.ExitCode = 1;
                }
                else
                {
                    MessageBox.Show(
                        "无法启用窗口或鼠标监听：" + enableFailure.Message,
                        "TabWheel",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private void ForegroundWindowChanged(IntPtr window)
        {
            if (!settings.Enabled)
            {
                return;
            }

            if (browserClassifier.IsSupportedBrowserWindow(window))
            {
                try
                {
                    activeBrowserWindow = window;
                    detector.ResetCache();
                    ResetAccumulation();
                    hook.Start();
                }
                catch (Exception exception)
                {
                    hook.Stop();
                    activeBrowserWindow = IntPtr.Zero;
                    detector.ResetCache();
                    ResetAccumulation();
                    if (testMode)
                    {
                        Environment.ExitCode = 1;
                    }
                    else
                    {
                        trayIcon.BalloonTipTitle = "TabWheel 暂未监听鼠标";
                        trayIcon.BalloonTipText = exception.Message;
                        trayIcon.ShowBalloonTip(3000);
                    }
                }
            }
            else
            {
                hook.Stop();
                activeBrowserWindow = IntPtr.Zero;
                detector.ResetCache();
                ResetAccumulation();
            }
        }

        private void UpdateTrayState()
        {
            trayIcon.Icon = settings.Enabled ? enabledIcon : disabledIcon;
            trayIcon.Text = settings.Enabled
                ? "TabWheel - 已启用（非浏览器时自动休眠）"
                : "TabWheel - 已停用";
        }

        private void ExitItemClick(object sender, EventArgs eventArgs)
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            if (testLifetimeTimer != null)
            {
                testLifetimeTimer.Stop();
                testLifetimeTimer.Dispose();
            }
            if (stateTestTimer != null)
            {
                stateTestTimer.Stop();
                stateTestTimer.Dispose();
            }
            foregroundMonitor.Dispose();
            hook.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            enabledIcon.Dispose();
            disabledIcon.Dispose();
            if (!testMode)
            {
                settings.Save();
            }
            base.ExitThreadCore();
        }
    }

    internal sealed class MouseWheelHook : IDisposable
    {
        private readonly WheelHandler handler;
        private readonly NativeMethods.LowLevelMouseProc callback;
        private IntPtr hookHandle;

        public delegate bool WheelHandler(NativeMethods.Point point, int delta);

        public MouseWheelHook(WheelHandler handler)
        {
            this.handler = handler;
            callback = HookCallback;
        }

        public void Start()
        {
            if (hookHandle != IntPtr.Zero)
            {
                return;
            }

            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);
                hookHandle = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WhMouseLowLevel,
                    callback,
                    moduleHandle,
                    0);
            }

            if (hookHandle == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public bool IsStarted
        {
            get { return hookHandle != IntPtr.Zero; }
        }

        public void Stop()
        {
            if (hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && message == (IntPtr)NativeMethods.WmMouseWheel)
            {
                try
                {
                    NativeMethods.LowLevelMouseHookData hookData =
                        (NativeMethods.LowLevelMouseHookData)Marshal.PtrToStructure(
                            data,
                            typeof(NativeMethods.LowLevelMouseHookData));
                    int delta = unchecked((short)((hookData.MouseData >> 16) & 0xffff));
                    if (handler(hookData.Location, delta))
                    {
                        return (IntPtr)1;
                    }
                }
                catch
                {
                    // A hook callback must never throw into Windows.
                }
            }

            return NativeMethods.CallNextHookEx(hookHandle, code, message, data);
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class ForegroundWindowMonitor : IDisposable
    {
        private readonly Action<IntPtr> handler;
        private readonly NativeMethods.WinEventProc callback;
        private IntPtr hookHandle;

        public ForegroundWindowMonitor(Action<IntPtr> handler)
        {
            this.handler = handler;
            callback = WinEventCallback;
        }

        public bool IsStarted
        {
            get { return hookHandle != IntPtr.Zero; }
        }

        public void Start()
        {
            if (hookHandle != IntPtr.Zero)
            {
                handler(NativeMethods.GetForegroundWindow());
                return;
            }

            hookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EventSystemForeground,
                NativeMethods.EventSystemForeground,
                IntPtr.Zero,
                callback,
                0,
                0,
                NativeMethods.WinEventOutOfContext | NativeMethods.WinEventSkipOwnProcess);

            if (hookHandle == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            handler(NativeMethods.GetForegroundWindow());
        }

        public void Stop()
        {
            if (hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private void WinEventCallback(
            IntPtr eventHook,
            uint eventType,
            IntPtr window,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime)
        {
            if (eventType != NativeMethods.EventSystemForeground || window == IntPtr.Zero)
            {
                return;
            }

            try
            {
                handler(window);
            }
            catch
            {
                // A system event callback must never throw into Windows.
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class BrowserWindowClassifier
    {
        private readonly HashSet<string> supportedProcesses;

        public BrowserWindowClassifier()
        {
            supportedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            supportedProcesses.Add("chrome");
            supportedProcesses.Add("msedge");
            supportedProcesses.Add("brave");
            supportedProcesses.Add("vivaldi");
            supportedProcesses.Add("opera");
            supportedProcesses.Add("opera_gx");
        }

        public bool IsSupportedBrowserWindow(IntPtr window)
        {
            if (window == IntPtr.Zero || !NativeMethods.IsWindow(window))
            {
                return false;
            }

            StringBuilder className = new StringBuilder(128);
            NativeMethods.GetClassName(window, className, className.Capacity);
            if (!className.ToString().StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal))
            {
                return false;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(window, out processId);
            if (processId == 0)
            {
                return false;
            }

            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    return supportedProcesses.Contains(process.ProcessName);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class TabStripDetector
    {
        private const int RoleSystemPageTab = 0x25;
        private const int RoleSystemPageTabList = 0x3c;
        private const int RoleSystemText = 0x2a;
        private const int RoleSystemPushButton = 0x2b;
        private const int RoleSystemComboBox = 0x2e;
        private const int RoleSystemMenuItem = 0x0c;
        private const int RoleSystemLink = 0x1e;
        private const uint CacheLifetimeMilliseconds = 50;
        private const int CachePointTolerance = 4;
        private IntPtr cachedWindow;
        private NativeMethods.Point cachedPoint;
        private uint cachedAt;
        private bool cachedResult;
        private bool hasCachedResult;

        public bool IsTabStripPoint(NativeMethods.Point point, IntPtr browserWindow)
        {
            if (browserWindow == IntPtr.Zero || !NativeMethods.IsWindow(browserWindow))
            {
                return false;
            }

            IntPtr hitWindow = NativeMethods.WindowFromPoint(point);
            if (hitWindow == IntPtr.Zero)
            {
                return false;
            }

            IntPtr rootWindow = NativeMethods.GetAncestor(hitWindow, NativeMethods.GaRoot);
            if (rootWindow == IntPtr.Zero || rootWindow != browserWindow)
            {
                return false;
            }

            NativeMethods.Rectangle windowRectangle;
            if (!NativeMethods.GetWindowRect(rootWindow, out windowRectangle))
            {
                return false;
            }

            int scale = GetScalePercent(rootWindow);
            int localX = point.X - windowRectangle.Left;
            int localY = point.Y - windowRectangle.Top;
            int windowWidth = windowRectangle.Right - windowRectangle.Left;
            int broadTopLimit = Scale(64, scale);

            if (localX < 0 || localX >= windowWidth || localY < 0 || localY > broadTopLimit)
            {
                return false;
            }

            uint now = unchecked((uint)Environment.TickCount);
            if (hasCachedResult &&
                cachedWindow == browserWindow &&
                Math.Abs(point.X - cachedPoint.X) <= CachePointTolerance &&
                Math.Abs(point.Y - cachedPoint.Y) <= CachePointTolerance &&
                now - cachedAt <= CacheLifetimeMilliseconds)
            {
                return cachedResult;
            }

            bool blockingControl;
            if (IsAccessiblePageTab(point, out blockingControl))
            {
                return CacheResult(browserWindow, point, now, true);
            }

            if (blockingControl)
            {
                return CacheResult(browserWindow, point, now, false);
            }

            int fallbackHeight = Scale(48, scale);
            int windowButtonsWidth = Scale(150, scale);
            if (localY <= fallbackHeight && localX < windowWidth - windowButtonsWidth)
            {
                return CacheResult(browserWindow, point, now, true);
            }

            return CacheResult(browserWindow, point, now, false);
        }

        public void ResetCache()
        {
            hasCachedResult = false;
            cachedWindow = IntPtr.Zero;
            cachedAt = 0;
            cachedResult = false;
            cachedPoint.X = 0;
            cachedPoint.Y = 0;
        }

        private bool CacheResult(
            IntPtr browserWindow,
            NativeMethods.Point point,
            uint now,
            bool result)
        {
            cachedWindow = browserWindow;
            cachedPoint = point;
            cachedAt = now;
            cachedResult = result;
            hasCachedResult = true;
            return result;
        }

        private static bool IsAccessiblePageTab(NativeMethods.Point point, out bool blockingControl)
        {
            blockingControl = false;
            IAccessible accessible;
            object childId;
            int result = NativeMethods.AccessibleObjectFromPoint(point, out accessible, out childId);
            if (result != 0 || accessible == null)
            {
                return false;
            }

            IAccessible current = accessible;
            object currentChild = childId;

            try
            {
                for (int depth = 0; depth < 7 && current != null; depth++)
                {
                    int role;
                    if (TryGetRole(current, currentChild, out role))
                    {
                        if (role == RoleSystemPageTab || role == RoleSystemPageTabList)
                        {
                            return true;
                        }

                        if (role == RoleSystemText ||
                            role == RoleSystemPushButton ||
                            role == RoleSystemComboBox ||
                            role == RoleSystemMenuItem ||
                            role == RoleSystemLink)
                        {
                            blockingControl = true;
                        }
                    }

                    object parentObject;
                    try
                    {
                        parentObject = current.accParent;
                    }
                    catch
                    {
                        parentObject = null;
                    }

                    IAccessible parent = parentObject as IAccessible;
                    if (!Object.ReferenceEquals(current, accessible) && Marshal.IsComObject(current))
                    {
                        Marshal.ReleaseComObject(current);
                    }

                    current = parent;
                    currentChild = 0;
                }
            }
            finally
            {
                if (current != null && !Object.ReferenceEquals(current, accessible) && Marshal.IsComObject(current))
                {
                    Marshal.ReleaseComObject(current);
                }
                if (Marshal.IsComObject(accessible))
                {
                    Marshal.ReleaseComObject(accessible);
                }
            }

            return false;
        }

        private static bool TryGetRole(IAccessible accessible, object childId, out int role)
        {
            role = 0;
            try
            {
                object roleObject = accessible.get_accRole(childId);
                if (roleObject == null)
                {
                    return false;
                }
                role = Convert.ToInt32(roleObject, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int GetScalePercent(IntPtr window)
        {
            try
            {
                uint dpi = NativeMethods.GetDpiForWindow(window);
                if (dpi > 0)
                {
                    return (int)(dpi * 100 / 96);
                }
            }
            catch (EntryPointNotFoundException)
            {
            }

            return 100;
        }

        private static int Scale(int value, int percent)
        {
            return value * percent / 100;
        }
    }

    internal static class KeyboardSender
    {
        public static bool SwitchChromeTab(bool nextTab)
        {
            ushort pageKey = nextTab
                ? NativeMethods.VirtualKeyPageDown
                : NativeMethods.VirtualKeyPageUp;

            NativeMethods.Input[] inputs = new NativeMethods.Input[4];
            inputs[0] = NativeMethods.CreateKeyboardInput(NativeMethods.VirtualKeyControl, false);
            inputs[1] = NativeMethods.CreateKeyboardInput(pageKey, false);
            inputs[2] = NativeMethods.CreateKeyboardInput(pageKey, true);
            inputs[3] = NativeMethods.CreateKeyboardInput(NativeMethods.VirtualKeyControl, true);

            uint sent = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf(typeof(NativeMethods.Input)));
            return sent == inputs.Length;
        }
    }

    internal static class IconResources
    {
        public static Icon Load(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("找不到内置图标资源：" + resourceName);
                }

                using (Bitmap bitmap = new Bitmap(stream))
                {
                    IntPtr iconHandle = bitmap.GetHicon();
                    try
                    {
                        using (Icon icon = Icon.FromHandle(iconHandle))
                        {
                            return (Icon)icon.Clone();
                        }
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(iconHandle);
                    }
                }
            }
        }
    }

    internal sealed class AppSettings
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabWheel");
        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.ini");

        public bool Enabled { get; set; }
        public bool ReverseDirection { get; set; }

        private AppSettings()
        {
            Enabled = true;
            ReverseDirection = false;
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return settings;
                }

                string[] lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    string[] parts = line.Split(new char[] { '=' }, 2);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    bool value;
                    if (!Boolean.TryParse(parts[1].Trim(), out value))
                    {
                        continue;
                    }

                    if (String.Equals(parts[0].Trim(), "Enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.Enabled = value;
                    }
                    else if (String.Equals(parts[0].Trim(), "ReverseDirection", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.ReverseDirection = value;
                    }
                }
            }
            catch
            {
                // Defaults are safe if a settings file cannot be read.
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllLines(
                    SettingsPath,
                    new string[]
                    {
                        "Enabled=" + Enabled.ToString(CultureInfo.InvariantCulture),
                        "ReverseDirection=" + ReverseDirection.ToString(CultureInfo.InvariantCulture)
                    },
                    Encoding.UTF8);
            }
            catch
            {
                // A read-only profile should not stop the utility from running.
            }
        }
    }

    internal static class StartupManager
    {
        private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ValueName = "TabWheel";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    string value = key == null ? null : key.GetValue(ValueName) as string;
                    return !String.IsNullOrEmpty(value);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled)
                {
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }

    internal static class DpiAwareness
    {
        public static void Enable()
        {
            try
            {
                if (NativeMethods.SetProcessDpiAwarenessContext((IntPtr)(-4)))
                {
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
            }

            try
            {
                NativeMethods.SetProcessDPIAware();
            }
            catch
            {
            }
        }
    }

    internal static class SelfTest
    {
        public static bool Run()
        {
            int expectedInputSize = IntPtr.Size == 8 ? 40 : 28;
            int expectedHookDataSize = IntPtr.Size == 8 ? 32 : 24;
            if (Marshal.SizeOf(typeof(NativeMethods.Input)) != expectedInputSize ||
                Marshal.SizeOf(typeof(NativeMethods.LowLevelMouseHookData)) != expectedHookDataSize)
            {
                return false;
            }

            try
            {
                using (Icon enabled = IconResources.Load("TabWheel.Enabled.png"))
                using (Icon disabled = IconResources.Load("TabWheel.Disabled.png"))
                {
                    if (enabled.Handle == IntPtr.Zero ||
                        disabled.Handle == IntPtr.Zero ||
                        enabled.Size.Width < 16 ||
                        disabled.Size.Width < 16)
                    {
                        return false;
                    }
                }

                using (MouseWheelHook hook = new MouseWheelHook(
                    delegate(NativeMethods.Point point, int delta) { return false; }))
                {
                    hook.Start();
                    if (!hook.IsStarted)
                    {
                        return false;
                    }
                    hook.Stop();
                    if (hook.IsStarted)
                    {
                        return false;
                    }
                    hook.Start();
                    if (!hook.IsStarted)
                    {
                        return false;
                    }
                }

                int foregroundEvents = 0;
                using (ForegroundWindowMonitor monitor = new ForegroundWindowMonitor(
                    delegate(IntPtr window) { foregroundEvents++; }))
                {
                    monitor.Start();
                    if (!monitor.IsStarted || foregroundEvents < 1)
                    {
                        return false;
                    }
                    monitor.Stop();
                    if (monitor.IsStarted)
                    {
                        return false;
                    }
                    monitor.Start();
                    if (!monitor.IsStarted || foregroundEvents < 2)
                    {
                        return false;
                    }
                }

                BrowserWindowClassifier classifier = new BrowserWindowClassifier();
                if (classifier.IsSupportedBrowserWindow(IntPtr.Zero))
                {
                    return false;
                }
                new TabStripDetector().ResetCache();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class NativeMethods
    {
        public const int WhMouseLowLevel = 14;
        public const int WmMouseWheel = 0x020a;
        public const int GaRoot = 2;
        public const int WheelDelta = 120;
        public const uint EventSystemForeground = 0x0003;
        public const uint WinEventOutOfContext = 0x0000;
        public const uint WinEventSkipOwnProcess = 0x0002;
        public const ushort VirtualKeyPageUp = 0x21;
        public const ushort VirtualKeyPageDown = 0x22;
        public const ushort VirtualKeyShift = 0x10;
        public const ushort VirtualKeyControl = 0x11;
        public const ushort VirtualKeyMenu = 0x12;
        public const ushort VirtualKeyLeftWindows = 0x5b;
        public const ushort VirtualKeyRightWindows = 0x5c;
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;

        public delegate IntPtr LowLevelMouseProc(int code, IntPtr message, IntPtr data);
        public delegate void WinEventProc(
            IntPtr eventHook,
            uint eventType,
            IntPtr window,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LowLevelMouseHookData
        {
            public Point Location;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MouseInput Mouse;
            [FieldOffset(0)]
            public KeyboardInput Keyboard;
            [FieldOffset(0)]
            public HardwareInput Hardware;
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

        public static Input CreateKeyboardInput(ushort virtualKey, bool keyUp)
        {
            Input input = new Input();
            input.Type = InputKeyboard;
            input.Data.Keyboard.VirtualKey = virtualKey;
            input.Data.Keyboard.ScanCode = 0;
            input.Data.Keyboard.Flags = keyUp ? KeyEventKeyUp : 0;
            input.Data.Keyboard.Time = 0;
            input.Data.Keyboard.ExtraInfo = UIntPtr.Zero;
            return input;
        }

        public static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(
            int hookId,
            LowLevelMouseProc callback,
            IntPtr module,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(
            IntPtr hook,
            int code,
            IntPtr message,
            IntPtr data);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Point point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr window, int flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWinEventHook(
            uint eventMinimum,
            uint eventMaximum,
            IntPtr eventHookModule,
            WinEventProc eventHookFunction,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr eventHook);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr window, out Rectangle rectangle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("oleacc.dll")]
        public static extern int AccessibleObjectFromPoint(
            Point point,
            [MarshalAs(UnmanagedType.Interface)] out IAccessible accessible,
            [MarshalAs(UnmanagedType.Struct)] out object childId);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint count, Input[] inputs, int size);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDPIAware();
    }
}
