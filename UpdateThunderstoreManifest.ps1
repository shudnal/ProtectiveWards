[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath,

    [Parameter(Mandatory = $true)]
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($AssemblyPath).Version
$version = '{0}.{1}.{2}' -f $assemblyVersion.Major, $assemblyVersion.Minor, $assemblyVersion.Build
$content = [System.IO.File]::ReadAllText($ManifestPath)
$manifest = $content | ConvertFrom-Json
if ($null -eq $manifest.PSObject.Properties['version_number']) {
    throw "Thunderstore manifest has no version_number property: $ManifestPath"
}

$pattern = [regex]'("version_number"\s*:\s*")[^"]*(")'
if ($pattern.Matches($content).Count -ne 1) {
    throw "Expected exactly one version_number property in Thunderstore manifest: $ManifestPath"
}

$updated = $pattern.Replace($content, ('${1}' + $version + '${2}'), 1)
if ($updated -cne $content) {
    [System.IO.File]::WriteAllText($ManifestPath, $updated, [System.Text.UTF8Encoding]::new($false))
}
