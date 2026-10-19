# Stops md2html, removes autostart, settings, and the installed files.
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:LOCALAPPDATA 'Programs\md2html'

Get-Process md2html -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'md2html' -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\Software\md2html' -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path $target -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "md2html uninstalled."
