$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$root = Split-Path $PSScriptRoot
$probe = Join-Path $root 'bin\GuiProbe.exe'
$data = Join-Path $root 'bin\data'
$evidence = Join-Path $root 'artifacts\private\gui\reliability'
New-Item -ItemType Directory -Force $evidence | Out-Null
$original = Get-Content -Raw -Encoding UTF8 "$evidence\before-settings.json" | ConvertFrom-Json
$checks = [Collections.Generic.List[object]]::new()
function Ui { Get-Content -Raw -Encoding UTF8 "$data\ui-state.json" | ConvertFrom-Json }
function Wait-Ui([scriptblock]$condition) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt 25) {
        try { $s = Ui; if (& $condition $s) { return $s } } catch { }
        Start-Sleep -Milliseconds 50
    }
    throw ('Reliability GUI wait timed out: ' + ((Ui | Select-Object Visible,Scanning,Selecting,HotkeyRegistered,SettingsPending,SettingsError,Status) | ConvertTo-Json -Compress))
}
function Probe([string[]]$arguments) {
    $previous = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $output = & $probe @arguments 2>&1; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previous }
    if ($code -ne 0) { throw ('Probe ' + ($arguments -join ' ') + ': ' + ($output -join "`n")) }
}
function Check([string]$name,[bool]$passed) {
    $checks.Add([pscustomobject]@{Name=$name;Passed=$passed})
    $checks | ConvertTo-Json | Set-Content -Encoding UTF8 "$evidence\checks.json"
    Write-Output "$name = $passed"
    if (!$passed) { throw "Reliability check failed: $name" }
}
function Show-Popup {
    if (!(Ui).Visible) { Probe @('hotkey') }
    return Wait-Ui {param($x) $x.Visible -and !$x.Scanning -and !$x.Selecting}
}
try {
    $null = Wait-Ui {param($x) $null -ne $x.SettingsPending -and !$x.Scanning -and $x.HotkeyRegistered}
    Write-Output 'Resident is ready'
    $s = Show-Popup
    Check 'Completeness is visible in normal GUI status' ($s.Status.Contains([string]::Concat([char]0x5B8C,[char]0x6574,[char]0x6027)) -or $s.ScanWarning)
    $x = $s.Bounds.Left + 18; $y = $s.Bounds.Top + 12
    $locked = [IO.FileStream]::new("$data\settings.json",[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
    try {
        Probe @('move',[string]$x,[string]$y)
        $s = Wait-Ui {param($u) $u.SettingsPending -and $u.SettingsError}
        Check 'Write failure remains pending and visible' ($s.Status.Contains($s.SettingsError))
        Probe @('key','escape')
        $s = Wait-Ui {param($u) !$u.Visible -and $u.SettingsPending}
        Check 'Hide does not discard failed settings write' ([bool]$s.SettingsError)
    } finally { $locked.Dispose() }
    $s = Wait-Ui {param($u) !$u.SettingsPending -and !$u.SettingsError}
    $disk = Get-Content -Raw -Encoding UTF8 "$data\settings.json" | ConvertFrom-Json
    Check 'Background retry saves position after lock release' ($disk.PopupPlacement.Bounds.Left -eq $x -and $disk.PopupPlacement.Bounds.Top -eq $y)

    $s = Show-Popup
    Probe @('key','all')
    $snapshot = Get-Content -Raw -Encoding UTF8 "$data\latest-snapshot.json" | ConvertFrom-Json
    $selected = @($snapshot.Windows | Where-Object {$_.Eligible -and !$_.Minimized} | ForEach-Object {$_.Scan.Tabs} | Where-Object Selected | Select-Object -First 1)
    if (!$selected.Count) { throw 'No already selected Chrome tab available for focus handoff test' }
    $target = @($s.Displayed | Where-Object Id -eq $selected[0].Id)[0]
    Probe @('query',$target.Title)
    $s = Wait-Ui {param($u) $u.Query -eq $target.Title -and $u.Displayed.Count -gt 0}
    for ($i=0; $s.SelectedId -ne $target.Id -and $i -lt $s.Displayed.Count; $i++) {
        Probe @('key','down'); $s = Ui
    }
    if ($s.SelectedId -ne $target.Id) { throw 'Exact target row was not selected' }
    $focus = Start-Process -FilePath $probe -ArgumentList 'focus-after-selection' -WindowStyle Hidden -PassThru -RedirectStandardOutput "$evidence\focus-probe.txt" -RedirectStandardError "$evidence\focus-probe-error.txt"
    $null = $focus.Handle
    Probe @('key','enter')
    if (!$focus.WaitForExit(20000)) { $focus.Kill(); throw 'Focus handoff probe timed out' }
    $focus.Refresh()
    if ($focus.ExitCode -ne 0) { throw (Get-Content -Raw "$evidence\focus-probe-error.txt") }
    $s = Wait-Ui {param($u) !$u.Selecting -and !$u.Visible}
    Copy-Item "$data\last-selection.json" "$evidence\focus-selection.json" -Force
    $selection = Get-Content -Raw -Encoding UTF8 "$evidence\focus-selection.json" | ConvertFrom-Json
    Check 'Real focus handoff keeps popup hidden and selection successful' ($selection.Status -eq 'Passed' -and $selection.FocusChangedAfterSelection -and $selection.SelectionVerified)
} finally {
    $null = Show-Popup
    $b = $original.PopupPlacement.Bounds
    Probe @('bounds',[string]$b.Left,[string]$b.Top,[string]($b.Right-$b.Left),[string]($b.Bottom-$b.Top))
    $null = Wait-Ui {param($u) $u.Bounds.Left -eq $b.Left -and $u.Bounds.Top -eq $b.Top -and $u.Bounds.Right -eq $b.Right -and $u.Bounds.Bottom -eq $b.Bottom}
    Probe @('key','escape'); $null = Wait-Ui {param($u) !$u.Visible -and !$u.SettingsPending}
    Check 'Original position restored after reliability checks' $true
}
