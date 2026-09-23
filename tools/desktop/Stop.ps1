# Domio - zatrzymanie aplikacji uruchomionej bez Visual Studio

$processes = Get-Process -Name "Domio.Web" -ErrorAction SilentlyContinue

if ($null -eq $processes) {
    Write-Host "Domio nie jest obecnie uruchomione." -ForegroundColor Yellow
    exit 0
}

$processes | Stop-Process -Force
Write-Host "Domio zostało zatrzymane." -ForegroundColor Green
