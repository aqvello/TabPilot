param([string]$SearchTerm, [switch]$SkipScreenshots)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$OutputEncoding = [Text.UTF8Encoding]::new()
$root = Split-Path $PSScriptRoot
$probe = Join-Path $root 'bin\GuiProbe.exe'
$app = Join-Path $root 'bin\TabPilot.exe'
$data = Join-Path $root 'bin\data'
$evidence = Join-Path $root 'artifacts\private\gui'
New-Item -ItemType Directory -Force $evidence | Out-Null
$checks = [Collections.Generic.List[object]]::new()
$runPath = Join-Path $evidence ('checks-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json')
function Read-Ui { Get-Content -Encoding UTF8 -Raw (Join-Path $data 'ui-state.json') | ConvertFrom-Json }
function Wait-Ui([scriptblock]$condition, [int]$seconds = 20) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $seconds) {
        try { $state = Read-Ui; if (& $condition $state) { return $state } } catch { }
        Start-Sleep -Milliseconds 100
    }
    throw 'Timed out waiting for GUI state'
}
function Probe([string[]]$arguments) {
    $previous = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $result = & $probe @arguments 2>&1; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previous }
    if ($code -ne 0) { throw ('Probe ' + ($arguments -join ' ') + ': ' + ($result -join "`n")) }
}
function Check([string]$name, [bool]$passed, $details) {
    $checks.Add([pscustomobject]@{Name=$name;Passed=$passed;Details=$details})
    $checks | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $evidence 'checks.json')
    $checks | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 $runPath
    Write-Output "$name = $passed"
    if (!$passed) { throw "GUI check failed: $name" }
}
try {
    $s = Wait-Ui {param($x) $x.HotkeyRegistered -and !$x.Scanning}
    if ($s.Visible) { Probe @('hotkey'); $s = Wait-Ui {param($x) !$x.Visible} }
    Probe @('hotkey'); $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    $hwnd = $s.WindowHandle
    Check 'Global hotkey opens popup' ($s.Total -gt 0) @{Hotkey=$s.Hotkey;Tabs=$s.Total}
    if (!$SearchTerm) { $SearchTerm = $s.Displayed[0].Title }
    Probe @('key','escape'); $s = Wait-Ui {param($x) !$x.Visible}
    Check 'Escape hides without exiting' ($s.WindowHandle -eq $hwnd) $null
    Probe @('hotkey'); $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    Probe @('query',$SearchTerm); $s = Wait-Ui {param($x) $x.Query -eq $SearchTerm -and $x.Displayed.Count -gt 0}
    Check 'Search filters actual titles' (@($s.Displayed | Where-Object {$_.Title.IndexOf($SearchTerm,[StringComparison]::OrdinalIgnoreCase) -lt 0}).Count -eq 0) @{Matches=$s.Displayed.Count}
    Probe @('query','TabPilot_NoSuch_7F42'); $s = Wait-Ui {param($x) $x.Query -eq 'TabPilot_NoSuch_7F42' -and $x.Displayed.Count -eq 0}
    Check 'Empty search results' ($s.Visible) $null
    Probe @('clear'); Probe @('button','按屏幕'); $s = Wait-Ui {param($x) $x.Mode -eq 'Screens' -and $x.GroupHeadings.Count -eq 3}
    Check 'Screen grouping' ($s.Displayed.Count -eq $s.Total) $s.GroupHeadings
    Probe @('button','全部'); $s = Wait-Ui {param($x) $x.Mode -eq 'All'}
    $first = $s.SelectedId
    Probe @('key','down'); $s = Wait-Ui {param($x) $x.SelectedId -ne $first -and $x.SelectedId -gt 0}
    Probe @('key','up'); $s = Wait-Ui {param($x) $x.SelectedId -eq $first}
    Check 'Up and down navigation' $true @{First=$first}
    Probe @('button','最近使用'); $s = Wait-Ui {param($x) $x.Mode -eq 'Recent'}
    $settings = Get-Content -Encoding UTF8 -Raw (Join-Path $data 'settings.json') | ConvertFrom-Json
    Check 'Recent contains only app-recorded tabs' (@($s.Displayed | Where-Object {$_.Identity -notin $settings.Recent.Identity}).Count -eq 0) @{Count=$s.Displayed.Count}
    Probe @('button','全部'); Probe @('query',$SearchTerm); $s = Wait-Ui {param($x) $x.Query -eq $SearchTerm -and $x.Displayed.Count -gt 0}
    $target = $s.Displayed[0]
    $started = Get-Date
    Probe @('click-first')
    $s = Wait-Ui {param($x) !$x.Selecting -and !$x.Visible}
    $selection = Get-Content -Encoding UTF8 -Raw (Join-Path $data 'last-selection.json') | ConvertFrom-Json
    Check 'Click selects real Chrome tab and highlights' ($selection.Status -eq 'Passed' -and $selection.Id -eq $target.Id -and $selection.HighlightBoundsVerified -and $selection.HighlightElapsedMs -ge 2000) $selection
    Copy-Item (Join-Path $data 'last-selection.json') (Join-Path $evidence 'click-selection.json') -Force
    Probe @('hotkey'); $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    Probe @('button','最近使用'); $s = Wait-Ui {param($x) $x.Mode -eq 'Recent' -and $x.Displayed.Count -gt 0}
    Check 'Successful selection updates recent' ($s.Displayed[0].Identity -eq $target.Identity) @{First=$s.Displayed[0].Title}
    Probe @('key','enter'); $s = Wait-Ui {param($x) !$x.Selecting -and !$x.Visible}
    $selection = Get-Content -Encoding UTF8 -Raw (Join-Path $data 'last-selection.json') | ConvertFrom-Json
    Check 'Enter activates selected row' ($selection.Status -eq 'Passed' -and $selection.HighlightElapsedMs -ge 2000) $selection
    Copy-Item (Join-Path $data 'last-selection.json') (Join-Path $evidence 'enter-selection.json') -Force
    Probe @('hotkey'); $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    $second = Start-Process -FilePath $app -WorkingDirectory $root -PassThru
    $null = $second.Handle
    if (!$second.WaitForExit(5000)) { throw 'Second instance did not exit' }
    $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    Check 'Second launch reuses existing window' ($s.WindowHandle -eq $hwnd -and $second.ExitCode -eq 0) $null
    Probe @('key','all'); Probe @('clear')
    $dialogProbe = Start-Process -FilePath $probe -ArgumentList @('button','快捷键') -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds 350
    $dump = & $probe dump
    Check 'Hotkey settings dialog opens' (($dump -join "`n") -match '修改呼出快捷键') $null
    Probe @('key','escape')
    if (!$dialogProbe.WaitForExit(5000)) { throw 'Hotkey dialog probe did not finish' }
    $layout = (Get-Content -Encoding UTF8 -Raw (Join-Path $data 'latest-snapshot.json') | ConvertFrom-Json).Monitors
    foreach ($display in $layout) {
        $monitor = @{Name=$display.Device;X=($display.WorkArea.Left+100);Y=($display.WorkArea.Top+100);Dpi=[int][Math]::Round($display.ScalePercent*96/100)}
        Probe @('move', [string]$monitor.X, [string]$monitor.Y)
        $s = Wait-Ui {param($x) $x.Dpi -eq $monitor.Dpi -and $x.Visible}
        if (!$SkipScreenshots) {
            Probe @('screenshot')
            Copy-Item (Join-Path $data 'gui-screenshot.png') (Join-Path $evidence ("gui-"+($monitor.Name -replace '[^A-Za-z0-9_-]','')+'.png')) -Force
        }
        Check ("GUI DPI layout on "+$monitor.Name) ($s.Displayed.Count -eq $s.Total) @{Dpi=$s.Dpi;Rows=$s.Total}
    }
    Probe @('key','refresh'); Start-Sleep -Milliseconds 100; Probe @('key','quit')
    Start-Sleep -Milliseconds 500
    $left = @(Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath -eq $app})
    Check 'Exit releases GUI and worker processes' ($left.Count -eq 0) $null
    Start-Process -FilePath $app -ArgumentList '--hidden' -WorkingDirectory $root
    $s = Wait-Ui {param($x) !$x.Visible -and $x.HotkeyRegistered -and !$x.Scanning}
    Check 'Hidden launch and hotkey re-registration' ($s.HotkeyKey -eq $settings.HotkeyKey) @{Hotkey=$s.Hotkey}
    Probe @('hotkey'); $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    if (!$SkipScreenshots) {
        Probe @('screenshot')
        Copy-Item (Join-Path $data 'gui-screenshot.png') (Join-Path $evidence 'gui-final.png') -Force
    }
    Check 'Ready for user' ($s.Total -gt 0 -and $s.Mode -eq 'All' -and $s.Query -eq '') @{Hotkey=$s.Hotkey;Tabs=$s.Total}
} catch {
    $checks.Add([pscustomobject]@{Name='Failure';Passed=$false;Details=$_.ToString()})
    $checks | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $evidence 'checks.json')
    $checks | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 $runPath
    throw
}
