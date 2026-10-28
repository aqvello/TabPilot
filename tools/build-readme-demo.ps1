$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
New-Item -ItemType Directory -Force "$root\bin" | Out-Null
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll')
$refs += @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll') | ForEach-Object { Join-Path $framework "WPF\$_" }
$compilerArgs = @('/nologo','/target:exe','/main:ReadmeDemo','/platform:x64','/warnaserror+','/utf8output',"/win32manifest:$root\app.manifest","/out:$root\bin\ReadmeDemo.exe")
$compilerArgs += $refs | ForEach-Object { "/reference:$_" }
$compilerArgs += Get-ChildItem "$root\src\*.cs" | ForEach-Object FullName
$compilerArgs += "$PSScriptRoot\ReadmeDemo.cs"
& "$framework\csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Readme demo build failed' }
Copy-Item "$root\app.config" "$root\bin\ReadmeDemo.exe.config" -Force
