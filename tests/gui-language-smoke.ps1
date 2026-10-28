$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$root = Split-Path $PSScriptRoot
$probe = Join-Path $root 'bin\GuiProbe.exe'
$data = Join-Path $root 'bin\data'
$evidence = Join-Path $root 'artifacts\private\localization'
$original = Get-Content -Raw -Encoding UTF8 "$evidence\before-settings.json" | ConvertFrom-Json
$checks = [Collections.Generic.List[object]]::new()
function Ui { Get-Content -Raw -Encoding UTF8 "$data\ui-state.json" | ConvertFrom-Json }
function Wait-Ui([scriptblock]$condition) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt 25) {
        try { $s = Ui; if (& $condition $s) { return $s } } catch { }
        Start-Sleep -Milliseconds 80
    }
    throw ('Language GUI wait timed out: ' + ((Ui | Select-Object Visible,Scanning,Selecting,Language,Status) | ConvertTo-Json -Compress))
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
    if (!$passed) { throw "Language check failed: $name" }
}
function Show-Popup {
    if (!(Ui).Visible) { Probe @('hotkey') }
    return Wait-Ui {param($x) $x.Visible -and !$x.Scanning -and !$x.Selecting}
}
try {
    $s = Show-Popup
    $initial = if ($original.Language) {$original.Language} else {'zh-CN'}
    Check 'Existing settings load with the expected language' ($s.Language -eq $initial)
    Probe @('pick-language','en')
    $s = Wait-Ui {param($x) $x.Visible -and $x.Language -eq 'en' -and !$x.SettingsPending}
    Probe @('verify-language','en')
    Check 'Header language menu switches visible UI to English' $true
    $disk = Get-Content -Raw -Encoding UTF8 "$data\settings.json" | ConvertFrom-Json
    Check 'English preference saved without changing the global shortcut' ($disk.Language -eq 'en' -and $disk.HotkeyKey -eq $original.HotkeyKey -and $disk.HotkeyModifiers -eq $original.HotkeyModifiers)
    Probe @('query','TabPilot_Fictional_NoMatch_7F42')
    $s = Wait-Ui {param($x) $x.Query -eq 'TabPilot_Fictional_NoMatch_7F42' -and $x.Displayed.Count -eq 0}
    Check 'English search remains functional' $true
    Probe @('clear'); Probe @('key','quit')
    Start-Sleep -Milliseconds 350
    $started = [DateTime]::UtcNow
    Start-Process -FilePath "$root\bin\TabPilot.exe" -ArgumentList '--hidden' -WorkingDirectory $root
    $s = Wait-Ui {param($x) !$x.Visible -and $x.Language -eq 'en' -and $x.HotkeyRegistered -and !$x.Scanning -and ([DateTime]$x.CapturedUtc).ToUniversalTime() -ge $started}
    Check 'Hidden restart restores English and global shortcut registration' $true
    $null = Show-Popup; Probe @('verify-language','en')
    Check 'Recalled popup renders English after restart' $true
    Probe @('pick-language','zh-CN')
    $null = Wait-Ui {param($x) $x.Visible -and $x.Language -eq 'zh-CN'}
    Probe @('verify-language','zh-CN')
    Check 'Header language menu switches back to Chinese' $true
} finally {
    $s = Show-Popup
    $wanted = if ($original.Language) {$original.Language} else {'zh-CN'}
    if ($s.Language -ne $wanted) { Probe @('pick-language',$wanted); $null = Wait-Ui {param($x) $x.Language -eq $wanted} }
    $b = $original.PopupPlacement.Bounds
    Probe @('bounds',[string]$b.Left,[string]$b.Top,[string]($b.Right-$b.Left),[string]($b.Bottom-$b.Top))
    Probe @('key','escape'); $null = Wait-Ui {param($x) !$x.Visible -and !$x.SettingsPending}
    $disk = Get-Content -Raw -Encoding UTF8 "$data\settings.json" | ConvertFrom-Json
    Check 'Original language and popup position restored' ($disk.Language -eq $wanted -and $disk.PopupPlacement.Bounds.Left -eq $b.Left -and $disk.PopupPlacement.Bounds.Top -eq $b.Top)
}
