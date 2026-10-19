# Builds md2html, installs it per-user, and starts it. The app registers itself to start with Windows.
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:LOCALAPPDATA 'Programs\md2html'

Get-Process md2html -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish "$PSScriptRoot\src\Md2Html\Md2Html.csproj" -c Release -o $target -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Start-Process (Join-Path $target 'md2html.exe')
Write-Host "Installed to $target and started. Pin the 'MD' icon from the taskbar overflow (^) so it is always visible."
