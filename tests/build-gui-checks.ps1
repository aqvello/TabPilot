$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll')
$refs += @('UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll') | ForEach-Object { Join-Path $framework "WPF\$_" }
$compilerArgs = @('/nologo', '/target:exe', '/main:GuiChecks', '/platform:x64', '/optimize+', '/warnaserror+', '/utf8output', "/win32manifest:$root\app.manifest", "/out:$root\bin\GuiChecks.exe")
$compilerArgs += $refs | ForEach-Object { "/reference:$_" }
$compilerArgs += Get-ChildItem "$root\src\*.cs" | ForEach-Object FullName
$compilerArgs += "$PSScriptRoot\GuiChecks.cs"
$compilerArgs += "$PSScriptRoot\ReliabilityChecks.cs"
$compilerArgs += "$PSScriptRoot\LanguageChecks.cs"
& "$framework\csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'GUI check build failed' }
Copy-Item "$root\app.config" "$root\bin\GuiChecks.exe.config" -Force
