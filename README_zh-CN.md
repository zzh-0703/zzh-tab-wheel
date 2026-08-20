<p align="right"><strong>简体中文</strong> · <a href="README.md">English</a></p>

<p align="center">
  <img src="assets/enabled-png/icon128.png" width="96" alt="TabWheel 图标">
</p>

<h1 align="center">TabWheel for Windows</h1>

<p align="center">
  在 Chromium 浏览器的<strong>原生顶部标签栏</strong>滚动鼠标滚轮，快速切换前后标签页。
  <br>
  不需要安装 Chrome 扩展。
</p>

<p align="center">
  <a href="https://github.com/zzh-0703/zzh-tab-wheel/releases/latest"><img alt="最新版本" src="https://img.shields.io/badge/release-v0.2.1-6750A4"></a>
  <img alt="Windows 10 和 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-2EA44F"></a>
  <img alt="不需要 Chrome 扩展" src="https://img.shields.io/badge/Chrome%20extension-not%20required-4285F4">
</p>

<p align="center">
  <strong><a href="https://github.com/zzh-0703/zzh-tab-wheel/releases/download/v0.2.1/TabWheel-Windows-v0.2.1.zip">下载 ZIP</a></strong>
  · <a href="https://github.com/zzh-0703/zzh-tab-wheel/releases/download/v0.2.1/TabWheel.exe">单独下载 EXE</a>
  · <a href="releases/v0.2.1/SHA256SUMS.txt">SHA-256 校验</a>
</p>

<p align="center">
  <img src="assets/readme/tabwheel-demo.gif" width="900" alt="在 Chrome 顶部标签栏滚动切换标签页">
</p>

## 为什么使用 TabWheel

- **必须位于顶部标签栏才生效**：网页、地址栏、游戏和普通应用中的滚轮行为不受影响。
- **不需要 Chrome 扩展**：它是一个独立的 Windows 托盘程序。
- **离开浏览器自动休眠**：游戏或其他程序在前台时会卸载鼠标钩子，返回浏览器后自动恢复。
- **状态一眼可见**：启用时为蓝紫色双箭头，主动停用时为灰色斜杠图标。
- **针对高频滚轮优化**：标签栏识别缓存约 50ms，标签切换间隔限制为 120ms。

## 快速开始

1. 下载并解压 [TabWheel-Windows-v0.2.1.zip](https://github.com/zzh-0703/zzh-tab-wheel/releases/download/v0.2.1/TabWheel-Windows-v0.2.1.zip)。
2. 双击 `TabWheel.exe`。
3. 把鼠标移动到浏览器顶部的标签页或标签栏空白处。
4. 向下滚切到下一个标签，向上滚切到上一个标签。

右键系统托盘里的 TabWheel 图标，可以启用或停用功能、反转滚动方向、设置开机启动或退出。双击托盘图标可快速切换启用状态。

> [!NOTE]
> 当前版本尚未进行代码签名，Windows SmartScreen 可能显示“未知发布者”。你可以校验 SHA-256，或者直接从源码自行构建。

## 支持的浏览器

| 浏览器 | 标准水平顶部标签栏 |
| --- | :---: |
| Google Chrome | ✅ |
| Microsoft Edge | ✅ |
| Brave | ✅ |
| Vivaldi | ✅ |
| Opera | ✅ |

当前版本不支持垂直标签页。

## 托盘状态

| 状态 | 图标 | 行为 |
| --- | --- | --- |
| 已启用 | <img src="assets/enabled-png/icon32.png" width="24" alt="已启用"> | 浏览器在前台时工作；离开浏览器自动休眠 |
| 已停用 | <img src="assets/disabled-png/icon32.png" width="24" alt="已停用"> | 用户主动停用，前台窗口监控和鼠标钩子均卸载 |

## 隐私与安全边界

- 只监听鼠标滚轮，不记录按键、网页内容、标签标题或网址。
- 只有受支持浏览器位于前台时才安装鼠标滚轮钩子。
- 浏览器内只有鼠标位于原生顶部标签栏时才拦截滚轮。
- 切换动作通过浏览器原生的 `Ctrl+PageUp` 和 `Ctrl+PageDown` 快捷键完成。
- 设置仅保存在 `%LocalAppData%\TabWheel\settings.ini`。
- 只有用户从托盘菜单主动启用开机启动时，才会写入当前用户的 Windows `Run` 项。

## 识别方式

程序优先通过 Windows Accessibility (`IAccessible`) 判断鼠标下方是否为页面标签控件。标签栏空白处没有独立控件，因此会使用浏览器窗口顶部的一小块兼容区域，同时排除地址栏控件和右上角窗口按钮。

相同鼠标位置的标签栏识别结果会短暂缓存约 50ms。标签切换间隔限制为 120ms，最多约 8 次/秒。

## 性能

以下数据来自一台 12 逻辑处理器的测试机器：

| 场景 | TabWheel 测试结果 |
| --- | --- |
| 空闲 | CPU 增量处于测量下限 |
| 非浏览器前台，3000 次合成滚轮事件 | 鼠标钩子处于卸载状态，CPU 增量处于测量下限 |
| 浏览器标签栏高速滚动 | 约 50ms 识别缓存，最多约 8 次标签切换/秒 |

CPU 时间的累计数值会随进程运行时间增加，但这不代表实时 CPU 占用会越来越高。本程序没有随滚轮事件持续增长的队列或缓存。

## 系统要求

- Windows 10 或 Windows 11
- .NET Framework 4.8，受支持的 Windows 版本通常已内置
- 标准水平顶部标签栏

## 从源码构建

在 64 位 Windows PowerShell 中运行：

```powershell
.\build.ps1 -OutputDirectory .\dist
```

构建使用 Windows 自带的 .NET Framework C# 编译器，不下载第三方依赖。

运行内置检查：

```powershell
.\dist\TabWheel.exe --self-test
.\dist\TabWheel.exe --smoke-test
.\dist\TabWheel.exe --state-smoke-test
```

## 贡献与安全反馈

- 提交修改前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。
- 使用 [错误报告](https://github.com/zzh-0703/zzh-tab-wheel/issues/new?template=bug-report.yml) 或 [功能建议](https://github.com/zzh-0703/zzh-tab-wheel/issues/new?template=feature-request.yml) 表单。
- 不要在公开 Issue 中披露安全漏洞，请按照 [SECURITY.md](SECURITY.md) 私密报告。
- 中国大陆用户可以访问 [Gitee 镜像](https://gitee.com/zhang-zihao990703/zzh-tab-wheel)。

## 许可证

Copyright © 2026 Zhang Zihao (章梓昊)。

本项目使用 [MIT License](LICENSE) 开源。
