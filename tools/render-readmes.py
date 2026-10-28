"""Generate four README entry pages with shared media and consistent claims."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
VERSION = re.search(r'const string Number = "([0-9]+\.[0-9]+\.[0-9]+)";', (ROOT / "src/AppVersion.cs").read_text()).group(1)
RELEASE = f"https://github.com/aqvello/TabPilot/releases/tag/v{VERSION}"
NAV = "[简体中文](README.md) · [English](README.en.md) · [日本語](README.ja.md) · [Español](README.es.md)"
PAGES = {
    "README.md": {
        "tag": "把标签找回来，把注意力留给工作。",
        "intro": "无需 Chrome 扩展的 **Windows 多窗口、多屏标签切换器**。按快捷键呼出，搜索标题，回到需要的标签。",
        "download": "下载 Windows 便携版", "build": "从源码构建", "limits": "兼容性与限制",
        "features": [
            ("⌨ 快捷呼出", "标题搜索、↑↓ 选择、Enter 切换。"),
            ("▦ 多屏定位", "按显示器分组，支持混合 DPI，记住浮窗位置。"),
            ("◉ 原生与本地", "UI Automation / Win32；无需重启或改动 Chrome。"),
        ],
        "before": "切换前：三屏总览，目标标签在右屏后台", "after": "切换后：右屏目标窗口置前并放大展示",
        "demo": "看一遍操作流程",
        "demo_note": "不用挨个翻窗口找标签。按快捷键呼出 TabPilot，搜到「Design System」后按 Enter，就能切到右屏的目标标签。橙色边框会标出它所在的窗口。\n\n演示使用示例页面，放大效果仅为方便观看。[素材说明](docs/assets/README.md)",
        "screens": "中英文界面", "zh": "简体中文", "en": "English", "more": "展开：搜索与按屏幕分组",
        "search": "标题搜索", "monitors": "按屏幕分组",
        "start": "一分钟开始使用",
        "steps": [
            "从预览版 Release 下载 `TabPilot-Windows-x64.zip` 并解压。",
            "运行 `TabPilot.exe`，保留同目录的 `.config` 文件。",
            "按注册成功的快捷键呼出。默认 **Ctrl + Alt + 空格**；被占用时会尝试替代组合键，实际按键显示在浮窗中。",
            "输入标题搜索，点击结果或按 ↑↓、Enter。目标窗口会激活并显示约两秒高亮。Esc 或点击外部收起，程序保留在托盘。",
        ],
        "requirements": "需要 Windows 10 1703+ x64 与 .NET Framework 4.8。当前为 **v{version} 预览版**；实际验证环境为 Windows 10 / Chrome 156，不保证所有版本兼容。",
        "languages": "选择语言", "language_text": "软件支持 **简体中文 / English**。点击浮窗右上角 **中文 / EN**，或托盘菜单 **Language / 语言**；选择立即生效并会在重启后保留。网页标题始终保留原文，系统和 Chrome 的底层诊断可能保留原始语言。README 另有日文、西班牙文版本，它们不代表软件已支持这两种界面语言。",
        "keys": "常用操作", "action": "操作", "key": "按键 / 入口",
        "shortcuts": [("呼出 / 收起", "已注册的全局快捷键"), ("选择并切换", "↑↓ / Enter"), ("隐藏 / 退出", "Esc / Ctrl + Q"), ("刷新标签", "F5 / Ctrl + R"), ("全部 / 按屏幕 / 最近使用", "Ctrl + 1 / 2 / 3"), ("记住位置", "拖动顶部标题")],
        "boundary": [
            "**可访问清单不等于全部标签。** 折叠组、权限或 Provider 行为可能导致遗漏；界面标明“读取到”和完整性未验证。",
            "**激活受到 Windows 策略限制。** 弹窗、权限差异或焦点竞争可能导致失败。程序会核验结果并报告原因。",
            "复杂折叠组、管理员级 Chrome、虚拟桌面等环境未充分验证；不宣称全面支持。",
            "后台标签 URL 没有可靠数据源，副标题显示所属窗口；“最近使用”只记录通过 TabPilot 成功切换过的标签。",
        ],
        "privacy": "数据与隐私", "privacy_text": "TabPilot 本身不上传标签数据，不读取 Chrome 配置、历史或会话文件。自己的设置与诊断优先写入便携 `data` 目录，不可写时使用 `%LOCALAPPDATA%\\TabPilot`。设置有备份、原子替换和失败重试；诊断可能包含标题，请在分享前检查。文档媒体只展示专用虚构页面与程序控件，生成方式见 [素材说明](docs/assets/README.md)。",
        "build_text": "使用系统 .NET Framework 编译器，无需 .NET SDK 或 NuGet 下载：",
        "tests": "测试与实现", "test_text": "内核自检 8 项、GUI／模型回归 77 项；多语言新增检查覆盖界面、诊断、搜索、标题保持和设置兼容。真实三屏检查与故障记录单独记录，合成演示不作为 Chrome 兼容性证据。",
        "links": "[GUI 验证](docs/GUI_VALIDATION.md) · [内核验证](docs/VALIDATION.md) · [控制台接口](docs/CONSOLE_POC.md) · [更新记录](CHANGELOG.md)",
        "integration": "原生 GUI 集成脚本会操作实际窗口，部分依赖本机屏幕坐标和私有测试前状态文件，请按自己的环境准备。原始截图、个人设置、标签清单和日志不会进入仓库或发布包。",
    },
    "README.en.md": {
        "tag": "Find the tab. Keep your focus.",
        "intro": "A **native Windows tab switcher for Chrome across windows and monitors**. Open the popup, search a title, and return to the tab you need. No Chrome extension required.",
        "download": "Download for Windows", "build": "Build from source", "limits": "Compatibility & limits",
        "features": [("⌨ One shortcut away", "Search titles, select with ↑↓, switch with Enter."), ("▦ Across monitors", "Monitor grouping, mixed DPI and saved popup placement."), ("◉ Native & local", "UI Automation / Win32. No Chrome restart or configuration changes.")],
        "before": "Before: three screens, target tab in the background on the right", "after": "After: right-screen target activated and enlarged for clarity",
        "demo": "See the workflow", "demo_note": "Find a tab without checking every window. Open TabPilot, search for Design System and press Enter to switch to the tab on the right-hand screen. An orange border shows which window you switched to.\n\nThe demo uses sample pages. The zoom makes the result easier to see. [About the demo](docs/assets/README.md)",
        "screens": "Chinese and English UI", "zh": "Simplified Chinese", "en": "English", "more": "More screenshots: search and monitor grouping", "search": "Title search", "monitors": "By monitor",
        "start": "Get started in a minute", "steps": ["Download and extract `TabPilot-Windows-x64.zip` from the preview Release.", "Run `TabPilot.exe`, keeping its `.config` file alongside it.", "Use the registered global shortcut. The default is **Ctrl + Alt + Space**; if occupied, alternatives are tried and the actual shortcut is shown in the popup.", "Search by title and click a result, or use ↑↓ and Enter. The target window activates and is highlighted for about two seconds. Esc or clicking outside hides the popup in the tray."],
        "requirements": "Requires Windows 10 1703+ x64 and .NET Framework 4.8. **v{version} is a preview**, validated locally on Windows 10 / Chrome 156; compatibility with every version is not guaranteed.",
        "languages": "Choose your language", "language_text": "The application supports **Simplified Chinese and English**. Use **中文 / EN** in the popup header or **Language / 语言** in the tray menu. Changes apply immediately and survive restarting. Page titles keep their original language; low-level Windows or Chrome diagnostics may also remain untranslated. Japanese and Spanish are README translations, not additional application UI languages.",
        "keys": "Keyboard first", "action": "Action", "key": "Shortcut / control", "shortcuts": [("Open / hide", "Registered global shortcut"), ("Select / switch", "↑↓ / Enter"), ("Hide / quit", "Esc / Ctrl + Q"), ("Refresh", "F5 / Ctrl + R"), ("All / by monitor / recent", "Ctrl + 1 / 2 / 3"), ("Save popup position", "Drag the top heading")],
        "boundary": ["**Accessible tabs are not necessarily all tabs.** Collapsed groups, permissions and provider behavior may hide entries. Counts are labeled as found, with completeness unverified.", "**Windows controls foreground activation.** Dialogs, integrity levels and focus competition can prevent activation. Results are verified and failures reported.", "Complex collapsed groups, elevated Chrome, virtual desktops and other environments are not fully tested.", "Background-tab URLs are not reliably available. Subtitles show window context, and Recent only records successful switches made through TabPilot."],
        "privacy": "Data & privacy", "privacy_text": "TabPilot itself does not upload tab data or read Chrome profiles, history or session files. Its own settings and diagnostics use a writable portable `data` folder, otherwise `%LOCALAPPDATA%\\TabPilot`. Settings have atomic replacement, backup recovery and retries. Diagnostics may contain titles; review them before sharing. Documentation media uses dedicated fictional pages and native application controls only; see [how assets are generated](docs/assets/README.md).",
        "build_text": "Uses the system .NET Framework compiler, without the .NET SDK or NuGet downloads:",
        "tests": "Tests & implementation", "test_text": "8 core checks and 77 GUI/model regression checks, including localized controls, warnings, search, original titles and settings compatibility. Real multi-monitor checks and observed failures are documented separately; synthetic demonstrations do not establish Chrome compatibility.",
        "links": "[GUI validation](docs/GUI_VALIDATION.md) · [Core validation](docs/VALIDATION.md) · [CLI reference](docs/CONSOLE_POC.md) · [Changelog](CHANGELOG.md)",
        "integration": "Native GUI integration scripts manipulate real windows and may require machine-specific monitor coordinates and private pre-test state files. Original screenshots, settings, inventories and logs are excluded from the repository and release package. Detailed validation notes currently use Chinese.",
    },
    "README.ja.md": {
        "tag": "タブを見つけて、作業に集中。",
        "intro": "複数のウィンドウとモニターに散らばった Chrome タブを探せる **Windows ネイティブのタブ切り替えツール**。ショートカットで開き、タイトルを検索して目的のタブへ。Chrome 拡張機能は不要です。",
        "download": "Windows 版をダウンロード", "build": "ソースからビルド", "limits": "互換性と制限",
        "features": [("⌨ ショートカットで検索", "タイトル検索、↑↓ で選択、Enter で切り替え。"), ("▦ 複数モニター", "モニター別表示、異なる DPI、位置の保存に対応。"), ("◉ ネイティブ・ローカル", "UI Automation / Win32 を使用。Chrome の再起動や設定変更は不要。")],
        "before": "切り替え前：3 画面、対象タブは右画面のバックグラウンド", "after": "切り替え後：右画面の対象を前面にして拡大表示",
        "demo": "操作の流れ", "demo_note": "ウィンドウを一つずつ開いて探す必要はありません。ショートカットで TabPilot を開き、Design System を検索して Enter を押すと、右の画面にあるタブへ切り替わります。切り替え先のウィンドウはオレンジ色の枠でわかります。\n\nデモにはサンプルページを使用しています。拡大は結果を見やすくするための演出です。[デモについて](docs/assets/README.md)",
        "screens": "中国語・英語の画面", "zh": "簡体字中国語", "en": "English", "more": "追加画像：検索とモニター別表示", "search": "タイトル検索", "monitors": "モニター別表示",
        "start": "使い始める", "steps": ["プレビュー版 Release から `TabPilot-Windows-x64.zip` をダウンロードして展開します。", "同じフォルダーに `.config` ファイルを残し、`TabPilot.exe` を実行します。", "登録されたショートカットで開きます。既定は **Ctrl + Alt + Space**。使用中の場合は別の組み合わせを試し、実際のキーを画面に表示します。", "タイトルを検索し、結果をクリックするか ↑↓ と Enter で選びます。対象ウィンドウを前面にして約 2 秒強調表示します。Esc または画面外のクリックでトレイに隠れます。"],
        "requirements": "Windows 10 1703+ x64 と .NET Framework 4.8 が必要です。**v{version} はプレビュー版**です。Windows 10 / Chrome 156 でローカル検証していますが、すべての環境での動作を保証しません。",
        "languages": "表示言語", "language_text": "アプリの表示言語は **簡体字中国語と英語**です。画面右上の **中文 / EN**、またはトレイの **Language / 语言** で切り替えられ、再起動後も保存されます。ページタイトルや OS・Chrome の診断情報は原文を維持します。日本語・スペイン語は README の翻訳であり、アプリ UI の対応言語ではありません。",
        "keys": "主な操作", "action": "操作", "key": "キー・入口", "shortcuts": [("表示・非表示", "登録済みのグローバルショートカット"), ("選択・切り替え", "↑↓ / Enter"), ("隠す・終了", "Esc / Ctrl + Q"), ("更新", "F5 / Ctrl + R"), ("すべて・モニター別・最近", "Ctrl + 1 / 2 / 3"), ("位置の保存", "上部のタイトルをドラッグ")],
        "boundary": ["**読み取れた一覧が全タブとは限りません。** 折りたたまれたグループ、権限、Provider の動作により欠落する場合があります。完全性は未確認として表示します。", "**前面への移動は Windows の制御を受けます。** ダイアログ、権限差、フォーカスの競合により失敗することがあり、結果を検証して報告します。", "複雑な折りたたみグループ、管理者権限の Chrome、仮想デスクトップなどは十分に検証していません。", "バックグラウンドタブの URL は確実に取得できません。ウィンドウ情報を表示し、Recent は TabPilot 経由の成功した切り替えだけを記録します。"],
        "privacy": "データとプライバシー", "privacy_text": "TabPilot 自体はタブ情報をアップロードせず、Chrome のプロファイル、履歴、セッションファイルも読みません。独自の設定と診断は書き込み可能な `data`、または `%LOCALAPPDATA%\\TabPilot` に保存します。設定には原子的置換、バックアップ、再試行があります。診断にはタイトルが含まれるため、共有前に確認してください。画像の作成方法は [素材の説明](docs/assets/README.md) を参照してください。",
        "build_text": "システムの .NET Framework コンパイラーを使用します。.NET SDK や NuGet ダウンロードは不要です。",
        "tests": "テストと実装", "test_text": "コアの自己テスト 8 項目と GUI・モデルの回帰チェック 77 項目。翻訳、検索、元のタイトルの維持、設定互換性も確認します。実機の複数モニター検証と失敗例は別資料に記録し、架空データのデモを互換性の証拠にはしません。",
        "links": "[GUI 検証](docs/GUI_VALIDATION.md) · [コア検証](docs/VALIDATION.md) · [CLI](docs/CONSOLE_POC.md) · [変更履歴](CHANGELOG.md)",
        "integration": "実機 GUI テストは実際のウィンドウを操作します。環境固有の座標やテスト前の状態ファイルが必要な場合があります。個人設定、元のスクリーンショット、タブ一覧、ログはリポジトリや配布 ZIP に含めません。詳細な検証記録は現在中国語です。",
    },
    "README.es.md": {
        "tag": "Encuentra la pestaña. Mantén la concentración.",
        "intro": "Un **selector de pestañas nativo de Windows para Chrome, entre ventanas y monitores**. Abre el panel, busca un título y vuelve a la pestaña que necesitas. Sin extensiones de Chrome.",
        "download": "Descargar para Windows", "build": "Compilar el código", "limits": "Compatibilidad y límites",
        "features": [("⌨ Un atajo para buscar", "Busca títulos, selecciona con ↑↓ y cambia con Enter."), ("▦ Varios monitores", "Agrupación por monitor, DPI mixto y posición guardada."), ("◉ Nativo y local", "UI Automation / Win32. Sin reiniciar ni reconfigurar Chrome.")],
        "before": "Antes: tres pantallas, pestaña de destino en segundo plano a la derecha", "after": "Después: ventana de la derecha activada y ampliada",
        "demo": "Mira el flujo de uso", "demo_note": "Encuentra una pestaña sin revisar cada ventana. Abre TabPilot, busca Design System y pulsa Enter para ir a la pestaña de la pantalla derecha. Un borde naranja te indica qué ventana se ha activado.\n\nLa demostración usa páginas de ejemplo. El zoom permite ver mejor el resultado. [Sobre la demostración](docs/assets/README.md)",
        "screens": "Interfaz en chino e inglés", "zh": "Chino simplificado", "en": "English", "more": "Más capturas: búsqueda y monitores", "search": "Búsqueda por título", "monitors": "Por monitor",
        "start": "Empieza en un minuto", "steps": ["Descarga y extrae `TabPilot-Windows-x64.zip` desde la Release de vista previa.", "Ejecuta `TabPilot.exe` y conserva su archivo `.config` en la misma carpeta.", "Usa el atajo global registrado. El predeterminado es **Ctrl + Alt + Espacio**; si está ocupado, se prueban alternativas y se muestra la combinación activa en el panel.", "Busca un título y haz clic en un resultado, o usa ↑↓ y Enter. Se activa la ventana y se resalta durante unos dos segundos. Esc o un clic fuera ocultan el panel en la bandeja."],
        "requirements": "Requiere Windows 10 1703+ x64 y .NET Framework 4.8. **v{version} es una vista previa**, probada localmente en Windows 10 / Chrome 156. No se garantiza compatibilidad con todas las versiones.",
        "languages": "Elige el idioma", "language_text": "La aplicación admite **chino simplificado e inglés**. Usa **中文 / EN** en la cabecera o **Language / 语言** en la bandeja. El cambio es inmediato y se conserva al reiniciar. Los títulos de páginas y algunos diagnósticos de Windows o Chrome mantienen su idioma original. Japonés y español son traducciones del README, no idiomas adicionales de la interfaz.",
        "keys": "Atajos principales", "action": "Acción", "key": "Atajo / control", "shortcuts": [("Mostrar / ocultar", "Atajo global registrado"), ("Seleccionar / cambiar", "↑↓ / Enter"), ("Ocultar / salir", "Esc / Ctrl + Q"), ("Actualizar", "F5 / Ctrl + R"), ("Todo / monitor / recientes", "Ctrl + 1 / 2 / 3"), ("Guardar posición", "Arrastrar el título superior")],
        "boundary": ["**Las pestañas accesibles no son necesariamente todas las pestañas.** Los grupos contraídos, permisos o el proveedor pueden ocultar entradas. La interfaz indica que la lista no está verificada como completa.", "**Windows controla la activación de ventanas.** Los diálogos, niveles de permisos y cambios de foco pueden impedirla. Se verifican los resultados y se informa de los fallos.", "Los grupos contraídos complejos, Chrome elevado, escritorios virtuales y otros entornos no están plenamente probados.", "No se obtienen con fiabilidad las URL de todas las pestañas en segundo plano. Se muestra la ventana; Recent solo registra cambios exitosos realizados mediante TabPilot."],
        "privacy": "Datos y privacidad", "privacy_text": "TabPilot no sube datos de pestañas ni lee perfiles, historial o archivos de sesión de Chrome. Su configuración y diagnósticos se guardan en una carpeta `data` escribible o en `%LOCALAPPDATA%\\TabPilot`. La configuración usa sustitución atómica, copia de seguridad y reintentos. Los diagnósticos pueden contener títulos: revísalos antes de compartirlos. Los medios usan datos ficticios; consulta [cómo se generan](docs/assets/README.md).",
        "build_text": "Utiliza el compilador .NET Framework del sistema, sin .NET SDK ni descargas de NuGet:",
        "tests": "Pruebas e implementación", "test_text": "8 comprobaciones del núcleo y 77 de regresión GUI/modelo, incluidas traducciones, avisos, búsqueda, títulos originales y compatibilidad de ajustes. Las pruebas reales con monitores y los fallos observados se documentan aparte; las demostraciones sintéticas no prueban compatibilidad con Chrome.",
        "links": "[Validación GUI](docs/GUI_VALIDATION.md) · [Núcleo](docs/VALIDATION.md) · [CLI](docs/CONSOLE_POC.md) · [Cambios](CHANGELOG.md)",
        "integration": "Las pruebas nativas de integración GUI manipulan ventanas reales y pueden necesitar coordenadas y estados previos específicos del equipo. No se incluyen capturas originales, ajustes personales, listas de pestañas ni registros en el repositorio o paquete. Las notas técnicas detalladas están actualmente en chino.",
    },
}

for filename, p in PAGES.items():
    media_language = "zh" if filename == "README.md" else "en"
    demo_alt = "TabPilot：三屏总览 → 搜索 → 选择 → 右屏目标窗口置前" if media_language == "zh" else "TabPilot: three screens → search → select → right-screen target brought into focus"
    features = "\n".join(f"| **{a}** | {b} |" for a, b in p["features"])
    steps = "\n".join(f"{i}. {text}" for i, text in enumerate(p["steps"], 1))
    shortcuts = "\n".join(f"| {a} | {b} |" for a, b in p["shortcuts"])
    limits = "\n".join(f"- {x}" for x in p["boundary"])
    text = f'''<div align="center">

<img src="docs/assets/banner.svg" width="1120" alt="TabPilot — Your tabs. One shortcut away." />

### {p["tag"]}

{NAV}

**[↓ {p["download"]}]({RELEASE})** · [{p["build"]}](#build) · [{p["limits"]}](#limits)

Windows x64 · .NET Framework 4.8 · UI: 简体中文 / English · v{VERSION} Preview

</div>

{p["intro"]}

| | |
| :--- | :--- |
{features}

## {p["demo"]}

![{demo_alt}](docs/assets/demo-{media_language}.gif)

{p["demo_note"]}

| {p["before"]} | {p["after"]} |
| :---: | :---: |
| ![{p["before"]}](docs/assets/workflow-before-{media_language}.png) | ![{p["after"]}](docs/assets/workflow-after-{media_language}.png) |

## {p["screens"]}

| {p["zh"]} | {p["en"]} |
| :---: | :---: |
| ![{p["zh"]}](docs/assets/tabs-zh.png) | ![{p["en"]}](docs/assets/tabs-en.png) |

<details>
<summary>{p["more"]}</summary>

| {p["search"]} | {p["monitors"]} |
| :---: | :---: |
| ![{p["search"]}](docs/assets/search-en.png) | ![{p["monitors"]}](docs/assets/monitors-en.png) |

</details>

## {p["start"]}

{steps}

{p["requirements"]}

## {p["languages"]}

{p["language_text"]}

## {p["keys"]}

| {p["action"]} | {p["key"]} |
| :--- | :--- |
{shortcuts}

<a id="limits"></a>

## {p["limits"]}

{limits}

## {p["privacy"]}

{p["privacy_text"]}

<a id="build"></a>

## {p["build"]}

{p["build_text"]}

```powershell
git clone https://github.com/aqvello/TabPilot.git
Set-Location TabPilot
powershell -NoProfile -ExecutionPolicy Bypass -File .\\build.ps1
.\\bin\\TabPilot.exe
```

## {p["tests"]}

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\\tests\\build-gui-checks.ps1
.\\bin\\GuiChecks.exe
.\\bin\\TabPilot.Cli.exe self-test
powershell -NoProfile -ExecutionPolicy Bypass -File .\\package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\\tests\\release-checks.ps1
```

{p["test_text"]}

{p["links"]}

{p["integration"]}

[MIT License](LICENSE) · Copyright © 2026 aqvello
'''.replace("{version}", VERSION)
    (ROOT / filename).write_text(text, encoding="utf-8")
print("Rendered four README languages.")
