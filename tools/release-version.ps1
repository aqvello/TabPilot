function Get-TabPilotVersion([string]$Root) {
    $source = [IO.File]::ReadAllText((Join-Path $Root 'src\AppVersion.cs'))
    $match = [regex]::Match($source, 'const string Number = "([0-9]+\.[0-9]+\.[0-9]+)";')
    if (!$match.Success) { throw 'Cannot determine release version from src/AppVersion.cs' }
    $number = $match.Groups[1].Value
    $windows = $number + '.0'
    [xml]$manifest = [IO.File]::ReadAllText((Join-Path $Root 'app.manifest'))
    $identity = $manifest.SelectSingleNode('/*[local-name()="assembly"]/*[local-name()="assemblyIdentity"]')
    if (!$identity -or $identity.version -ne $windows) { throw "Manifest version must be $windows (src/AppVersion.cs)" }
    [pscustomobject]@{ Number=$number; Tag=('v'+$number); Windows=$windows; Informational=($number+'-preview') }
}

function Assert-TabPilotBinaryVersion([string]$Path, $Version) {
    $assembly = [Reflection.AssemblyName]::GetAssemblyName($Path).Version.ToString()
    $file = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    if ($assembly -ne $Version.Windows -or $file.FileVersion -ne $Version.Windows -or $file.ProductVersion -ne $Version.Informational) {
        throw "Binary release version mismatch: $Path"
    }
}
