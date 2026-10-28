param([ValidateSet('GuiProbe.exe','GuiProbe.Recovery.exe')][string]$OutputName = 'GuiProbe.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& "$framework\csc.exe" /nologo /target:exe /platform:x64 "/out:$root\bin\$OutputName" "/win32manifest:$root\app.manifest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "$root\src\AssemblyInfo.cs" "$root\src\AppVersion.cs" "$PSScriptRoot\GuiProbe.cs"
if ($LASTEXITCODE -ne 0) { throw 'GUI probe build failed' }
Copy-Item "$root\app.config" "$root\bin\$OutputName.config" -Force
