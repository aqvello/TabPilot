$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. "$root\tools\release-version.ps1"
$version = Get-TabPilotVersion $root
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (!(Test-Path $csc)) { throw 'Requires Windows x64 with .NET Framework 4.x (system C# compiler).' }
$output = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $output | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll')
$references += @('UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll') | ForEach-Object { Join-Path $framework "WPF\$_" }
$compilerArgs = @('/nologo', '/platform:x64', '/optimize+', '/warn:4', '/warnaserror+', '/utf8output', "/win32manifest:$root\app.manifest")
$compilerArgs += $references | ForEach-Object { "/reference:$_" }
$compilerArgs += Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object FullName
foreach ($target in @(@{Name='TabPilot.exe';Kind='winexe'}, @{Name='TabPilot.Cli.exe';Kind='exe'})) {
    & $csc "/target:$($target.Kind)" "/out:$output\$($target.Name)" @compilerArgs
    if ($LASTEXITCODE -ne 0) { throw "C# build failed: $LASTEXITCODE" }
    Copy-Item "$root\app.config" "$output\$($target.Name).config" -Force
    Assert-TabPilotBinaryVersion "$output\$($target.Name)" $version
    Write-Output "Built $output\$($target.Name)"
}
