param([Parameter(Mandatory=$true)][ValidateCount(2,2)][string[]]$TabTitles)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$root = Split-Path $PSScriptRoot
$probe = Join-Path $root 'bin\GuiProbe.exe'
$data = Join-Path $root 'bin\data'
$evidence = Join-Path $root 'artifacts\private\gui'
function Ui { Get-Content -Raw -Encoding UTF8 "$data\ui-state.json" | ConvertFrom-Json }
function Wait-Ui([scriptblock]$condition) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt 20) {
        try { $s = Ui; if (& $condition $s) { return $s } } catch { }
        Start-Sleep -Milliseconds 100
    }
    throw 'GUI wait timed out'
}
function Probe([string[]]$arguments) { & $probe @arguments | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'Probe failed' } }
foreach ($test in @(@{Query=$TabTitles[0];Action='click-first';File='background-click-selection.json'}, @{Query=$TabTitles[1];Action='enter';File='background-enter-selection.json'})) {
    if ((Ui).Visible) { Probe @('hotkey'); $null = Wait-Ui {param($x) !$x.Visible} }
    Probe @('hotkey'); $null = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
    Probe @('key','all'); Probe @('query',$test.Query)
    $s = Wait-Ui {param($x) $x.Query -eq $test.Query -and $x.Displayed.Count -eq 1}
    $snapshot = Get-Content -Encoding UTF8 -Raw "$data\latest-snapshot.json" | ConvertFrom-Json
    $tab = @($snapshot.Windows.Scan.Tabs | Where-Object Id -eq $s.Displayed[0].Id)
    if ($tab.Count -ne 1 -or $tab[0].Selected) { throw 'Test target is already selected; choose a different background fixture' }
    if ($test.Action -eq 'click-first') { Probe @('click-first') } else { Probe @('key','enter') }
    $null = Wait-Ui {param($x) !$x.Selecting -and !$x.Visible}
    $r = Get-Content -Encoding UTF8 -Raw "$data\last-selection.json" | ConvertFrom-Json
    if ($r.Status -ne 'Passed' -or $r.WasSelected -or !$r.SelectionVerified -or !$r.ForegroundVerified -or $r.HighlightElapsedMs -lt 2000) { throw 'Background selection failed' }
    Copy-Item "$data\last-selection.json" (Join-Path $evidence $test.File) -Force
    Write-Output ($test.Action + ': Passed, background=True, highlight=' + $r.HighlightElapsedMs)
}
Probe @('hotkey'); $null = Wait-Ui {param($x) $x.Visible -and !$x.Scanning}
Probe @('key','all'); Probe @('clear'); Probe @('screenshot')
Copy-Item "$data\gui-screenshot.png" "$evidence\gui-final.png" -Force
