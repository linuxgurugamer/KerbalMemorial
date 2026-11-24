param([Parameter(Mandatory=$true)][string]$KspRoot, [string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot/KerbalMemorial.csproj" -c $Configuration "-p:KSPRoot=$KspRoot"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
