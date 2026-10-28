$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll')
$refs += @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll') | ForEach-Object { Join-Path $framework "WPF\$_" }
$compilerArgs = @('/nologo','/target:exe','/main:WorkflowDemo','/platform:x64','/warnaserror+','/utf8output',"/win32manifest:$root\app.manifest","/out:$root\bin\WorkflowDemo.exe")
$compilerArgs += $refs | ForEach-Object { "/reference:$_" }
$compilerArgs += Get-ChildItem "$root\src\*.cs" | ForEach-Object FullName
$compilerArgs += "$PSScriptRoot\WorkflowDemo.cs"
$compilerArgs += "$PSScriptRoot\DemoBrowser.cs"
$compilerArgs += "$PSScriptRoot\WorkflowScene.cs"
& "$framework\csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Workflow demo build failed' }
Copy-Item "$root\app.config" "$root\bin\WorkflowDemo.exe.config" -Force
