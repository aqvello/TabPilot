$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
. "$root\tools\release-version.ps1"
$version = Get-TabPilotVersion $root
$passed = 0
function Check([bool]$condition, [string]$name) {
    if (!$condition) { throw "Release check failed: $name" }
    $script:passed++
    Write-Output "PASS $name"
}
foreach ($binary in @('TabPilot.exe','TabPilot.Cli.exe')) {
    Assert-TabPilotBinaryVersion "$root\bin\$binary" $version
    Check $true "$binary metadata matches source and manifest version"
}
$mismatched = [pscustomobject]@{ Windows='0.0.0.0'; Informational='0.0.0-preview' }
$rejected = $false
try { Assert-TabPilotBinaryVersion "$root\bin\TabPilot.exe" $mismatched } catch { $rejected = $true }
Check $rejected 'binary version mismatch is rejected'
$temporary = Join-Path "$root\artifacts\private" ('release-check-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force (Join-Path $temporary 'src') | Out-Null
    Copy-Item "$root\src\AppVersion.cs" (Join-Path $temporary 'src\AppVersion.cs')
    $manifest = [IO.File]::ReadAllText("$root\app.manifest").Replace(('version="'+$version.Windows+'"'), 'version="0.0.0.0"')
    [IO.File]::WriteAllText((Join-Path $temporary 'app.manifest'), $manifest)
    $rejected = $false
    try { $null = Get-TabPilotVersion $temporary } catch { $rejected = $true }
    Check $rejected 'manifest version mismatch is rejected'

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = "$root\artifacts\TabPilot-Windows-x64.zip"
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $guideName = [string]::Concat([char]0x4F7F,[char]0x7528,[char]0x8BF4,[char]0x660E,'.txt')
        $expected = @('TabPilot/TabPilot.exe','TabPilot/TabPilot.exe.config','TabPilot/LICENSE','TabPilot/README.txt',('TabPilot/'+$guideName))
        Check ($zip.Entries.Count -eq 5 -and @($zip.Entries | Where-Object {$_.FullName -notin $expected}).Count -eq 0) 'archive contains only the five approved files, including LICENSE'
        foreach ($entry in $zip.Entries) {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $temporary $entry.Name), $true)
        }
    } finally { $zip.Dispose() }
    Check ((Get-FileHash "$temporary\LICENSE").Hash -eq (Get-FileHash "$root\LICENSE").Hash) 'packaged MIT license matches repository license'
    Check ((Get-FileHash "$temporary\TabPilot.exe").Hash -eq (Get-FileHash "$root\bin\TabPilot.exe").Hash) 'packaged executable matches the validated binary'
    Check ((Get-FileHash "$temporary\TabPilot.exe.config").Hash -eq (Get-FileHash "$root\app.config").Hash) 'packaged runtime configuration matches source'
    Check (([IO.File]::ReadAllText("$temporary\README.txt").Contains($version.Tag)) -and ([IO.File]::ReadAllText((Join-Path $temporary $guideName)).Contains($version.Tag))) 'both packaged guides identify this release'
    $digest = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
    Check ([IO.File]::ReadAllText(($archive+'.sha256')).Trim() -eq ($digest+'  TabPilot-Windows-x64.zip')) 'published checksum matches archive'
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force } }
Write-Output "Release checks: $passed passed ($($version.Tag) Preview)"
