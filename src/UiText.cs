using System;
using System.Collections.Generic;

namespace TabPilot
{
    internal static class UiText
    {
        static string language = "zh-CN";
        internal static string Language { get { return language; } }
        internal static bool Supported(string language) { return language == "zh-CN" || language == "en"; }
        internal static void SetLanguage(string value) { language = Supported(value) ? value : "zh-CN"; }
        static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "；", "; " },
            { "TabPilot — 标签切换", "TabPilot — Tab switcher" },
            { "TabPilot 标签切换器", "TabPilot tab switcher" },
            { "搜索标签", "Search tabs" }, { "搜索所有已打开的网页…", "Search your open tabs…" },
            { "Chrome 标签列表", "Chrome tabs" },
            { "刷新", "Refresh" }, { "刷新标签", "Refresh tabs" },
            { "快捷键", "Shortcut" }, { "设置呼出快捷键", "Configure shortcut" },
            { "隐藏 TabPilot", "Hide TabPilot" }, { "全部", "All tabs" },
            { "按屏幕", "By monitor" }, { "最近使用", "Recent" },
            { "↑↓ 选择 · Enter 定位 · Esc 隐藏", "↑↓ Select · Enter Switch · Esc Hide" },
            { "详情", "Details" }, { "查看操作详情", "Show details" },
            { "显示当前能读取到的标签；部分折叠组或窗口可能有遗漏。", "Accessible tabs only. Some collapsed groups or windows may be missing." },
            { "重新读取 Chrome 标签（F5）", "Refresh Chrome tabs (F5)" },
            { "通过 TabPilot 成功切换过的标签，最近使用的排在前面。", "Successful switches made through TabPilot, most recent first." },
            { "{0} 个屏幕 · 读取到 {1} 个标签", "{0} monitors · {1} tabs found" },
            { " 呼出 · F5 刷新", " to open · F5 Refresh" },
            { "快捷键：{0} 呼出 / 收起", "Shortcut: {0} to show / hide" },
            { "正在读取 Chrome 标签…", "Reading Chrome tabs…" },
            { "还没有最近使用的标签\n通过 TabPilot 切换后会出现在这里", "No recent tabs yet\nSwitch with TabPilot to add one here" },
            { "暂时没有读取到 Chrome 标签\n打开 Chrome 后按 F5 刷新", "No Chrome tabs found\nOpen Chrome and press F5 to refresh" },
            { "没有找到匹配的标签\n试试网页标题或屏幕名称", "No matching tabs\nTry a title or monitor name" },
            { "未命名标签", "Untitled tab" }, { "未知屏幕", "Unknown monitor" },
            { "主屏幕", "Main monitor" }, { "左侧屏幕", "Left monitor" }, { "右侧屏幕", "Right monitor" },
            { "上方屏幕", "Upper monitor" }, { "下方屏幕", "Lower monitor" },
            { "窗口 ", "Window " }, { " · 已最小化", " · Minimized" }, { " · 当前标签", " · Active tab" },
            { "打开 TabPilot", "Open TabPilot" }, { "退出 TabPilot", "Quit TabPilot" },
            { "修改快捷键…（{0}）", "Change shortcut… ({0})" },
            { "快捷键未注册，可从托盘修改。", "Shortcut unavailable. Change it from the tray menu." },
            { "正在更新标签，可继续搜索已有结果…", "Refreshing tabs. You can search cached results…" },
            { "读取标签失败，可以按 F5 重试。", "Could not read tabs. Press F5 to retry." },
            { "切换失败，请刷新后重试。", "Could not switch tabs. Refresh and try again." },
            { "这个标签或窗口已经变化，请刷新后重新选择。", "This tab or window has changed. Refresh and select it again." },
            { "Windows 未允许激活目标窗口，请关闭目标窗口上的弹窗后重试。", "Windows did not allow activation. Check for dialogs in the target window and retry." },
            { "切换已尝试，但高亮或前台核验未通过，请检查目标窗口。", "Switch attempted, but highlight or foreground checks failed. Check the target window." },
            { "暂时无法切到这个标签，请刷新后重试。", "Cannot switch to this tab right now. Refresh and retry." },
            { "原快捷键被占用，已改用 {0}。", "The original shortcut is occupied. Now using {0}." },
            { "数据目录：", "Data directory: " }, { "操作记录读取失败：", "Could not read action record: " },
            { "TabPilot · 操作详情", "TabPilot · Details" }, { "快捷键不可用", "Shortcut unavailable" },
            { "请选择包含 Ctrl 或 Alt 的组合键，并避开系统保留键。", "Choose a shortcut with Ctrl or Alt, avoiding reserved system keys." },
            { "快捷键已被其他程序占用，或 Windows 不允许注册。请点击“快捷键”换一个组合键。", "This shortcut is occupied or blocked by Windows. Choose another shortcut." },
            { "空格", "Space" }, { "修改呼出快捷键", "Change popup shortcut" },
            { "按下新的组合键，例如 Ctrl + Alt + Q", "Press a new shortcut, e.g. Ctrl + Alt + Q" },
            { "保存", "Save" }, { "取消", "Cancel" },
            { "{0} 个窗口未完整读取", "{0} windows were not fully read" },
            { "存在折叠组，可能遗漏标签", "Collapsed groups may hide tabs" },
            { "部分进程无法访问，可能遗漏窗口", "Some processes are inaccessible; windows may be missing" },
            { "，可查看详情或按 F5 重试。", ". See Details or press F5 to retry." },
            { "没有识别到 Chrome 窗口；可查看详情或按 F5 重试。", "No Chrome windows identified. See Details or press F5 to retry." },
            { "已读取可访问标签，完整性未验证 · 详情", "Accessible tabs read; completeness unverified · Details" },
            { "尚无扫描结果。", "No scan results yet." },
            { "可访问标签数量不等于 Chrome 内部全部标签数量。", "The accessible inventory may not include every tab inside Chrome." },
            { "{0}: {1}，读取到 {2} 个标签", "{0}: {1}, {2} tabs found" },
            { "无法创建可写的数据目录。请检查程序目录或当前用户的数据目录权限。", "No writable data directory. Check application and user-data folder permissions." },
            { "设置文件损坏，已恢复上一次成功保存的备份。", "Settings were damaged. Restored the previous successful backup." },
            { "设置读取失败，已使用默认设置。", "Could not read settings. Using defaults. " },
            { "设置尚未保存，正在重试：", "Settings not saved yet. Retrying: " },
            { "TabPilot 无法启动：", "TabPilot could not start: " }
        };
        internal static string T(string chinese) { string translated; return Language == "en" && English.TryGetValue(chinese, out translated) ? translated : chinese; }
        internal static string F(string chinese, params object[] args) { return String.Format(T(chinese), args); }
        // Only for application-generated messages retained across a language change, never web titles.
        internal static string RelocalizeMessage(string message)
        {
            if (message == null) return null;
            foreach (var entry in English)
            {
                if (message == entry.Key || message == entry.Value) return Language == "en" ? entry.Value : entry.Key;
            }
            foreach (var entry in English)
            {
                // Preserve the exception text and paths after an application-owned prefix.
                if (!entry.Key.EndsWith("：", StringComparison.Ordinal) && entry.Key != "设置读取失败，已使用默认设置。") continue;
                string source = Language == "en" ? entry.Key : entry.Value;
                if (message.StartsWith(source, StringComparison.Ordinal))
                    return (Language == "en" ? entry.Value : entry.Key) + message.Substring(source.Length);
            }
            return message;
        }
    }
}
