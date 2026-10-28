$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$root = Split-Path $PSScriptRoot
$probe = Join-Path $root 'bin\GuiProbe.exe'
$app = Join-Path $root 'bin\TabPilot.exe'
$data = Join-Path $root 'bin\data'
$evidence = Join-Path $root 'artifacts\private\gui\position'
New-Item -ItemType Directory -Force $evidence | Out-Null
$original = Get-Content -Raw -Encoding UTF8 (Join-Path $evidence 'position-before.json') | ConvertFrom-Json
$checks = [Collections.Generic.List[object]]::new()
function Ui { Get-Content -Raw -Encoding UTF8 "$data\ui-state.json" | ConvertFrom-Json }
function Wait-Ui([scriptblock]$condition) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt 20) {
        try { $s = Ui; if (& $condition $s) { return $s } } catch { }
        Start-Sleep -Milliseconds 100
    }
    throw 'GUI position wait timed out'
}
function Probe([string[]]$arguments) {
    $previous = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $output = & $probe @arguments 2>&1; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previous }
    if ($code -ne 0) { throw ($output -join "`n") }
    return ($output -join "`n")
}
function Same-Bounds($a,$b) { return $a.Left -eq $b.Left -and $a.Top -eq $b.Top -and $a.Right -eq $b.Right -and $a.Bottom -eq $b.Bottom }
function Show-Popup {
    if ((Ui).Visible) { $null = Probe @('hotkey'); $null = Wait-Ui {param($x) !$x.Visible} }
    $null = Probe @('hotkey')
    return Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
}
function Check([string]$name,[bool]$passed,$details) {
    $checks.Add([pscustomobject]@{Name=$name;Passed=$passed;Details=$details})
    $checks | ConvertTo-Json -Depth 15 | Set-Content -Encoding UTF8 "$evidence\checks.json"
    Write-Output "$name = $passed"
    if (!$passed) { throw "Position check failed: $name" }
}
try {
    $s = Show-Popup
    Check 'Upgrade preserves previous popup position' (Same-Bounds $s.Bounds $original.Bounds) $s.Bounds
    $layout = @((Get-Content -Raw -Encoding UTF8 "$data\latest-snapshot.json" | ConvertFrom-Json).Monitors)
    if ($layout.Count -lt 2) { throw 'Position checks require multiple displays' }
    $targets = @()
    for ($i = 0; $i -lt $layout.Count; $i++) {
        $display = $layout[$i]; $other = $layout[($i+1)%$layout.Count]
        $targets += @{Name=('Display-'+$i);X=($display.WorkArea.Left+100);Y=($display.WorkArea.Top+100);Dpi=[int][Math]::Round($display.ScalePercent*96/100);Display=$display.Device;OtherX=($other.WorkArea.Left+100);OtherY=($other.WorkArea.Top+100)}
    }
    foreach ($test in $targets) {
        $null = Probe @('move',[string]$test.X,[string]$test.Y)
        $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning -and $x.Dpi -eq $test.Dpi -and $x.SavedPlacement.MonitorDevice -eq $test.Display -and (Same-Bounds $x.Bounds $x.SavedPlacement.Bounds)}
        $before = $s.Bounds
        $null = Probe @('key','escape'); $null = Wait-Ui {param($x) !$x.Visible}
        $disk = Get-Content -Raw -Encoding UTF8 "$data\settings.json" | ConvertFrom-Json
        Check ($test.Name+' saved on hide') (Same-Bounds $disk.PopupPlacement.Bounds $before) $disk.PopupPlacement
        $foreground = Probe @('recall-from',[string]$test.OtherX,[string]$test.OtherY)
        $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
        Check ($test.Name+' ignores foreground monitor') ((Same-Bounds $s.Bounds $before) -and $s.SavedPlacement.MonitorDevice -eq $test.Display -and !$foreground.Contains($test.Display)) @{Foreground=$foreground;Bounds=$s.Bounds;Dpi=$s.Dpi}
        $null = Probe @('key','refresh'); $s = Wait-Ui {param($x) !$x.Scanning}
        Check ($test.Name+' refresh keeps position') (Same-Bounds $s.Bounds $before) @{Before=$before;After=$s.Bounds;Saved=$s.SavedPlacement}
    }
    $before = $s.Bounds
    $expectedDisplay = $s.SavedPlacement.MonitorDevice
    $otherDisplay = $layout | Where-Object {$_.Device -ne $expectedDisplay} | Select-Object -First 1
    $null = Probe @('key','quit'); Start-Sleep -Milliseconds 300
    $started = [DateTime]::UtcNow
    Start-Process -FilePath $app -ArgumentList '--hidden' -WorkingDirectory $root
    $null = Wait-Ui {param($x) !$x.Visible -and $x.HotkeyRegistered -and !$x.Scanning -and ([DateTime]$x.CapturedUtc).ToUniversalTime() -ge $started}
    $foreground = Probe @('recall-from',[string]($otherDisplay.WorkArea.Left+100),[string]($otherDisplay.WorkArea.Top+100))
    $s = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    Check 'Restart restores last screen and coordinates' ((Same-Bounds $s.Bounds $before) -and $s.SavedPlacement.MonitorDevice -eq $expectedDisplay) @{Foreground=$foreground;Bounds=$s.Bounds}
} catch {
    $checks.Add([pscustomobject]@{Name='Failure';Passed=$false;Details=$_.ToString()})
    throw
} finally {
    try {
        $null = Show-Popup
        $b = $original.Bounds
        $null = Probe @('bounds',[string]$b.Left,[string]$b.Top,[string]($b.Right-$b.Left),[string]($b.Bottom-$b.Top))
        $s = Wait-Ui {param($x) $x.Visible -and (Same-Bounds $x.Bounds $original.Bounds)}
        $null = Probe @('key','escape'); $null = Wait-Ui {param($x) !$x.Visible}
        $disk = Get-Content -Raw -Encoding UTF8 "$data\settings.json" | ConvertFrom-Json
        $checks.Add([pscustomobject]@{Name='Original position restored after tests';Passed=(Same-Bounds $disk.PopupPlacement.Bounds $original.Bounds);Details=$disk.PopupPlacement})
    } catch { $checks.Add([pscustomobject]@{Name='Position cleanup failure';Passed=$false;Details=$_.ToString()}) }
    $checks | ConvertTo-Json -Depth 15 | Set-Content -Encoding UTF8 "$evidence\checks.json"
}
