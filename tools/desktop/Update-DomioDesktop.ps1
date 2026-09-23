# Domio - szybka aktualizacja wersji bez Visual Studio
# Ten skrypt publikuje aktualny kod ponownie i zachowuje tę samą bazę danych.

$Installer = Join-Path $PSScriptRoot "Install-DomioDesktop.ps1"

if (-not (Test-Path $Installer)) {
    throw "Nie znaleziono Install-DomioDesktop.ps1."
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Installer
