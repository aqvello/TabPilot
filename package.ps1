$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. "$root\tools\release-version.ps1"
$version = Get-TabPilotVersion $root
Assert-TabPilotBinaryVersion "$root\bin\TabPilot.exe" $version
$stageRoot = Join-Path (Join-Path $root 'artifacts\package') ([Guid]::NewGuid().ToString('N'))
$stage = Join-Path $stageRoot 'TabPilot'
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$root\bin\TabPilot.exe" $stage -Force
Copy-Item "$root\bin\TabPilot.exe.config" $stage -Force
Copy-Item "$root\LICENSE" $stage -Force
$guide = Join-Path (Join-Path $root 'docs') ([string]::Concat([char]0x4F7F,[char]0x7528,[char]0x8BF4,[char]0x660E,'.txt'))
[IO.File]::WriteAllText((Join-Path $stage ([IO.Path]::GetFileName($guide))), [IO.File]::ReadAllText($guide, [Text.Encoding]::UTF8), [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllText((Join-Path $stage 'README.txt'), [IO.File]::ReadAllText((Join-Path $root 'docs\USAGE.txt'), [Text.Encoding]::UTF8), [Text.UTF8Encoding]::new($true))
if (!(Get-Content -LiteralPath (Join-Path $stage 'README.txt') -TotalCount 1).Contains($version.Tag)) { throw 'English guide version differs from release version' }
$archive = Join-Path $root 'artifacts\TabPilot-Windows-x64.zip'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($archive, [IO.FileMode]::Create)
try {
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $stage -File) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, ('TabPilot/' + $file.Name), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose() }
} finally {
    $stream.Dispose()
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
$digest = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archive + '.sha256'), ($digest + '  ' + [IO.Path]::GetFileName($archive) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output $archive
