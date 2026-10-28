# TabPilot Windows 原生可行性 PoC

用 Windows UI Automation 和 Win32 API 枚举现有 Google Chrome 窗口、显示器与标签，输入编号切换标签，并在目标窗口边缘显示约 2 秒的橙色高亮框。本页介绍控制台入口；新版图形浮窗见 [项目说明](../README.md)。

已在 Windows 10 / Chrome 155 测试环境中实测。**基本路线可行，但前台激活有条件，不能保证所有场景成功。** 实测覆盖率、失败记录和未测范围见 [验证报告](VALIDATION.md)。

## 构建和运行

要求 Windows 10 1703+ x64，系统 .NET Framework 4.x，以及与 Chrome 位于同一交互桌面的普通用户会话。无需 .NET SDK、NuGet、管理员权限或额外运行时安装。

在 Windows PowerShell 中：

```powershell
Set-Location .\TabPilot
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\bin\TabPilot.Cli.exe
```

程序按显示器 → 窗口 → 标签打印结果。输入标签前的数字并回车；`r` 重新枚举，`q` 退出。编号只属于当次快照；刷新后可能变化。

```text
Monitor DEMO ... mixed scaling verified
  Window 0x... PID=... minimized=False DPI=192 ...
    [17] TabPilot Fixture 17 selected=False ...
tab> 17
[17] Passed ... selected=True foreground=True border=2026ms ...
```

也可分开枚举、选择和批量验证：

```powershell
.\bin\TabPilot.Cli.exe list --out .\artifacts\private\snapshot.json
.\bin\TabPilot.Cli.exe select --snapshot .\artifacts\private\snapshot.json --id 17
.\bin\TabPilot.Cli.exe verify --snapshot .\artifacts\private\snapshot.json --ids 1,2,17
.\bin\TabPilot.Cli.exe self-test
```

`verify` 会依次激活目标标签，每个高亮 2 秒，最后尽力恢复快照中被测窗口的原选中标签及最小化状态。测试期间不要操作这些窗口；若快照已过期，应先重新枚举。它只恢复本次涉及的窗口。每次结果均保存，失败退出码为 `3`；没有可用标签时 `list` 返回 `2`；参数或程序异常返回 `1`。

日志默认保存在 `artifacts/private/`，含运行环境的 HWND、进程路径、窗口和标签的可访问名称；该目录已加入 `.gitignore`。PowerShell 5.1 读取这些 UTF-8 JSON 时须使用 `Get-Content -Encoding UTF8 -Raw`。

## 实现边界

- `EnumWindows` 枚举顶层窗口，`QueryFullProcessImageName` / 产品元数据识别 Google Chrome；同名但产品身份不匹配的程序单独标记排除。隐藏辅助窗口、拥有者弹窗仍记入 JSON，并标明不作为浏览器窗口扫描的原因。
- 每个浏览器窗口用独立 UIA 工作进程读取 Raw View；跳过 Document 和渲染器子树，仅接收 Chrome 标签栏下的 `TabItem`。不会把页面内的 ARIA tablist 算成浏览器标签。最多四个读取工作进程并发，共享一轮默认 15 秒的 UIA 读取预算（`--timeout-ms`），未开始的窗口也会明确报告预算耗尽；进程启动、Win32 枚举及清理另计。切换工作进程 20 秒超时，只终止自身工作进程。
- 映射包括 HWND、PID、进程启动时间及 UIA RuntimeId。切换前重取节点，禁止按旧位置或相同标题猜测。标签关闭、拖入其他窗口或 UIA 节点重建后，可能需要刷新。
- `SelectionItem.Select` 优先，支持时可回退 `Invoke`。核验目标为唯一选中标签，并核验 `GetForegroundWindow`。前台激活使用 `SetForegroundWindow` / UIA `SetFocus`，交互父进程可以合法向工作进程传递前台权限；失败明确返回 `FOREGROUND_DENIED`，不强行绕过 Windows 的前台策略。
- 内嵌清单声明 PerMonitorV2。窗口矩形来自 `GetWindowRect` / DWM；显示器来自 `MonitorFromWindow` / `EnumDisplayMonitors`；缩放来自 `GetScaleFactorForMonitor`，窗口 DPI 来自 `GetDpiForWindow`。跨屏窗口按 `MonitorFromWindow` 的规则归属主要相交显示器。
- 最小化时的窗口矩形可能是 `(-32000,-32000)` 一类图标坐标；`NormalBounds` 是恢复时的 **workspace** 坐标，不能当作当前屏幕坐标。显示器归属采用 Windows 的最小化前位置语义。
- 边框是四个独立、置顶、鼠标穿透且不激活的 Win32/WinForms 窗口。以 DWM 物理边界定位，约每 16 ms 跟踪位置，至少保持 2000 ms；记录实际边框坐标、持续时间及是否抢焦点。进程结束时边框销毁。
- 标签的 `Title` 是 UIA `Name`，Chrome 可能附加内存、组名等辅助信息；它不是浏览器内部未经修饰的文档标题。该 PoC 不读取 URL、页面 DOM 或 Chrome 内部 tab ID。

控制台和正式应用不安装、调用或依赖任何 Chrome 扩展；不启动或重启 Chrome；不使用 DevTools、远程调试端口、Chrome 启动参数、浏览器内部接口、配置/历史/会话文件或注册表设置。集成测试驱动仅通过现有 Chrome 的普通界面建立本地测试标签和标签组，不读写 Chrome 的配置文件。文档录制工具另行启动使用临时 profile 的隔离 Chrome，仅用于生成虚构演示素材，详见 [素材说明](assets/README.md)。

完整场景矩阵尚未在无扩展的干净 profile 中独立复测；临时隔离 profile 的文档演示只验证三屏四标签流程，不能代替完整矩阵。

## 复测

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\build-harness.ps1
.\bin\IntegrationHarness.exe prepare
.\bin\IntegrationHarness.exe matrix
```

`prepare` 在**已经运行的 Chrome** 内用 Ctrl+N 建立独立窗口，用普通输入打开 24 个不同标题的本地 HTML 文件。每页都有 `DOM DECOY TAB`，用于检验不会误读网页标签。`matrix` 移动这个测试窗口到每台现有显示器，核对独立已知的 24 个标题，并验证选中、前台、高亮和最小化恢复。它不修改系统的显示器配置或缩放。

测试驱动还提供 `create-group`、`collapse`、`capture NAME`、`dump`、`screenshot`、`close` 等控制台操作。`create-group` 的测试菜单名称对应简体中文 Chrome，其他语言需要调整测试驱动；生产枚举/切换不依赖该菜单文案。折叠组状态必须查看证据中的 `Groups[].State`，不能只凭文件名断言。

本页描述控制台内核。图形入口已包含浮窗、托盘及全局快捷键，使用方式见项目 README；尚无安装器。不同 Chrome 版本、策略、权限层级、锁屏及 UIA Provider 行为仍需继续验证。
