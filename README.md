<p align="center">
  <img src="assets/enabled-png/icon128.png" width="96" alt="TabWheel 图标">
</p>

<h1 align="center">TabWheel for Windows</h1>

<p align="center">
  在 Chromium 浏览器的<strong>原生顶部标签栏</strong>滚动鼠标滚轮，快速切换前后标签页。
  <br>
  Scroll over the native top tab strip to switch Chromium tabs.
</p>

<p align="center">
  <a href="releases/v0.2.0/TabWheel-Windows-v0.2.0.zip"><img alt="Version 0.2.0" src="https://img.shields.io/badge/version-0.2.0-6750A4"></a>
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-2EA44F"></a>
  <img alt="No Chrome extension" src="https://img.shields.io/badge/Chrome%20extension-not%20required-4285F4">
</p>

<p align="center">
  <strong><a href="releases/v0.2.0/TabWheel-Windows-v0.2.0.zip">下载 ZIP</a></strong>
  · <a href="releases/v0.2.0/TabWheel.exe">单独下载 EXE</a>
  · <a href="releases/v0.2.0/SHA256SUMS.txt">SHA-256 校验</a>
</p>

## 为什么使用 TabWheel

- **必须位于标签栏才生效**：网页、地址栏和普通应用中的滚轮行为不受影响。
- **不需要 Chrome 扩展**：它是一个独立的 Windows 托盘程序。
- **离开浏览器自动休眠**：游戏或其他程序在前台时会卸载鼠标钩子，返回浏览器后自动恢复。
- **状态一眼可见**：启用时为蓝紫色双箭头，主动停用时为灰色斜杠图标。
- **针对高频滚轮优化**：标签栏识别缓存约 50ms，标签切换间隔限制为 120ms。

## 快速开始

1. 下载并解压 [TabWheel-Windows-v0.2.0.zip](releases/v0.2.0/TabWheel-Windows-v0.2.0.zip)。
2. 双击 `TabWheel.exe`。
3. 把鼠标移动到浏览器顶部的标签页或标签栏空白处。
4. 向下滚切到下一个标签，向上滚切到上一个标签。

> [!NOTE]
> 当前版本尚未进行代码签名，Windows SmartScreen 可能显示“未知发布者”。你可以从源码自行构建，并用仓库提供的 SHA-256 文件校验下载内容。

## 使用和托盘状态

右键系统托盘里的 TabWheel 图标，可以启用/停用功能、反转滚动方向、设置开机启动或退出。双击托盘图标可快速切换启用状态。

| 状态 | 图标 | 行为 |
| --- | --- | --- |
| 已启用 | <img src="assets/enabled-png/icon32.png" width="24" alt="已启用"> | 浏览器在前台时工作；离开浏览器自动休眠 |
| 已停用 | <img src="assets/disabled-png/icon32.png" width="24" alt="已停用"> | 用户主动停用，不监听前台窗口或鼠标 |

### 支持的浏览器

| 浏览器 | 标准水平顶部标签栏 |
| --- | :---: |
| Google Chrome | ✅ |
| Microsoft Edge | ✅ |
| Brave | ✅ |
| Vivaldi | ✅ |
| Opera | ✅ |

## 安全边界

- 只监听鼠标滚轮，不记录按键、网页内容、标签标题或网址。
- 只有受支持浏览器位于前台时才安装鼠标滚轮钩子；游戏和其他应用在前台时不会接收滚轮回调。
- 浏览器内只有鼠标在顶部标签栏时才拦截滚轮。
- 切换动作通过浏览器原生的 `Ctrl+PageUp` / `Ctrl+PageDown` 快捷键完成。
- 设置仅保存在 `%LocalAppData%\TabWheel\settings.ini`。
- 开机启动由用户在托盘菜单中主动启用，写入当前用户的 Windows `Run` 项。

## 识别方式

程序优先通过 Windows Accessibility (`IAccessible`) 判断鼠标下方是否为“页面标签”控件。标签栏空白处没有独立控件，因此会使用浏览器窗口顶部 48 个逻辑像素作为兼容区域，并排除地址栏按钮、文本框和右上角窗口按钮区域。

相同鼠标位置的标签栏识别结果会短暂缓存 50ms，高速滚轮时不会为每个事件重复调用 Accessibility。标签切换间隔限制为 120ms，最多约 8 次/秒。

## 性能

以下数据来自一台 12 逻辑处理器的测试机器：

| 场景 | TabWheel 测试结果 |
| --- | --- |
| 空闲 | CPU 增量为 0 |
| 非浏览器前台，3000 次合成滚轮事件 | 鼠标钩子处于卸载状态，CPU 增量为 0 |
| 浏览器标签栏高速滚动 | 约 50ms 识别缓存，最多约 8 次标签切换/秒 |

CPU 时间的累计数值会随历史使用增加，但实时 CPU 占用不会因此逐渐升高。本程序没有随滚轮事件持续增长的队列或缓存。

## 系统要求

- Windows 10/11；
- .NET Framework 4.8（Windows 10/11 通常已内置）；
- 标准水平顶部标签栏。

## 已知限制

- Chrome 垂直标签页不在当前版本支持范围内。
- 这是未签名的开发测试版，Windows SmartScreen 可能提示“未知发布者”。正式商业发布应购买代码签名证书并对安装包签名。
- 某些深度修改界面的 Chromium 浏览器主题可能需要调整顶部兼容区域。

## 构建

在 64 位 Windows PowerShell 中运行：

```powershell
.\build.ps1 -OutputDirectory .\dist
```

构建使用 Windows 自带的 .NET Framework C# 编译器，不下载第三方依赖。

## 项目文件

```text
Program.cs            主程序源码
build.ps1             Windows 构建脚本
assets/               托盘与程序图标
tests/StressWheel.ps1 滚轮压力测试脚本
releases/v0.2.0/      可直接运行的发布文件
```

## 反馈与贡献

- 发现问题或希望支持其他浏览器界面，请提交 [Issue](https://github.com/zzh-0703/zzh-tab-wheel/issues)。
- 提交代码前，请确认没有改变“仅在顶部标签栏拦截滚轮”的安全边界。
- 中国大陆用户也可以访问 [Gitee 镜像](https://gitee.com/zhang-zihao990703/zzh-tab-wheel)。

<details>
<summary><strong>English summary</strong></summary>

TabWheel is a lightweight Windows tray utility that switches Chromium tabs when you scroll over the browser's native top tab strip. It does not require a Chrome extension and does not intercept scrolling over web pages or the address bar.

- Supports Chrome, Edge, Brave, Vivaldi, and Opera.
- Automatically removes the mouse hook while games or other applications are in the foreground.
- Scroll down for the next tab and up for the previous tab; direction can be reversed from the tray menu.
- Download the ready-to-run [v0.2.0 ZIP](releases/v0.2.0/TabWheel-Windows-v0.2.0.zip), or build it with `build.ps1`.

</details>

## License

Copyright © 2026 Zhang Zihao (章梓昊).

This project is licensed under the [MIT License](LICENSE).
