# TabWheel for Windows

鼠标位于 Chrome 原生标签页或标签栏时，滚动滚轮切换前后标签。它是一个独立的 Windows 托盘程序，不需要安装 Chrome 扩展。

## 下载

- 推荐下载：[TabWheel-Windows-v0.2.0.zip](releases/v0.2.0/TabWheel-Windows-v0.2.0.zip)
- 单独下载：[TabWheel.exe](releases/v0.2.0/TabWheel.exe)
- 文件校验：[SHA256SUMS.txt](releases/v0.2.0/SHA256SUMS.txt)

下载 ZIP 后解压，双击 `TabWheel.exe` 即可运行，不需要安装 Chrome 扩展。

## 使用

1. 双击 `TabWheel.exe`。
2. 保持 Chrome 为当前窗口。
3. 把鼠标放在某个标签页或标签栏空白处。
4. 向下滚切到下一个标签，向上滚切到上一个标签。
5. 右键系统托盘里的 TabWheel 图标，可暂停、反转方向、设置开机启动或退出。

托盘图标会直接显示状态：

- 蓝紫色双箭头：功能已启用；游戏或其他应用在前台时会自动休眠，返回浏览器自动恢复。
- 灰色双箭头加斜杠：用户已主动停用，不监听前台窗口或鼠标。

程序也兼容 Edge、Brave、Vivaldi 和 Opera 的标准顶部标签栏。

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

在 12 逻辑处理器的测试机器上：

- 空闲状态 CPU 增量为 0；
- 旧版在非浏览器前台承受约 1000 次滚轮事件/秒时，约占整机 CPU 0.18%；
- 0.2.0 在非浏览器前台会卸载鼠标钩子，3000 次合成滚轮压力测试的 CPU 增量为 0，工作集和私有内存均无增长。

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
