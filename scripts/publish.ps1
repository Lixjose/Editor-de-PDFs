param([ValidateSet('win-x64','linux-x64')][string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet publish src/Mythlix.Desktop/Mythlix.Desktop.csproj -c Release -r $Runtime --self-contained true -m:1 -o "artifacts/$Runtime"
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
