# Domio - publikacja i utworzenie skrótu na pulpicie
# Uruchom ten skrypt po każdej większej aktualizacji aplikacji.
# Nie wymaga Visual Studio - korzysta z polecenia dotnet publish.

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$ProjectPath = Join-Path $RepoRoot "Domio.Web\Domio.Web.csproj"
$PublishDir = Join-Path $RepoRoot "dist\Domio"
$TempPublishDir = Join-Path $RepoRoot "dist\Domio-publish-temp"
$DatabasePath = Join-Path $RepoRoot "Domio.Web\domio.db"
$StartScript = Join-Path $PSScriptRoot "Start-Domio.ps1"
$Desktop = [Environment]::GetFolderPath("Desktop")
$ShortcutPath = Join-Path $Desktop "Domio.lnk"

Write-Host ""
Write-Host "=== Domio - przygotowanie wersji bez Visual Studio ===" -ForegroundColor Cyan
Write-Host "Projekt: $ProjectPath"
Write-Host "Publikacja: $PublishDir"
Write-Host ""

if (-not (Test-Path $ProjectPath)) {
    throw "Nie znaleziono projektu Domio.Web.csproj: $ProjectPath"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw "Nie znaleziono polecenia dotnet. Zainstaluj .NET 10 SDK albo uruchom skrypt na komputerze, na którym jest zainstalowane Visual Studio/.NET SDK."
}

Write-Host "[1/5] Sprawdzanie .NET..." -ForegroundColor Yellow
$version = (& dotnet --version).Trim()
Write-Host "      Wersja SDK: $version" -ForegroundColor Green

if (Test-Path $TempPublishDir) {
    Remove-Item $TempPublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TempPublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

Write-Host "[2/5] Publikowanie Domio dla Windows x64..." -ForegroundColor Yellow

& dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $TempPublishDir `
    /p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    throw "Publikowanie Domio zakończyło się błędem."
}

Write-Host "[3/5] Aktualizacja plików programu..." -ForegroundColor Yellow

# Zachowujemy dane lokalne w katalogu dist, jeżeli takie kiedyś powstaną.
$preserveNames = @("domio.db", "backups")
$preserved = @{}

foreach ($name in $preserveNames) {
    $path = Join-Path $PublishDir $name
    if (Test-Path $path) {
        $backup = Join-Path $env:TEMP ("DomioPreserve_" + [Guid]::NewGuid().ToString("N"))
        Move-Item $path $backup -Force
        $preserved[$name] = $backup
    }
}

Get-ChildItem $PublishDir -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force

Copy-Item (Join-Path $TempPublishDir "*") $PublishDir -Recurse -Force

foreach ($entry in $preserved.GetEnumerator()) {
    Move-Item $entry.Value (Join-Path $PublishDir $entry.Key) -Force
}

Remove-Item $TempPublishDir -Recurse -Force

if (-not (Test-Path (Join-Path $PublishDir "Domio.Web.exe"))) {
    throw "Nie znaleziono Domio.Web.exe po publikacji."
}

Write-Host "[4/5] Sprawdzanie konfiguracji domio.home..." -ForegroundColor Yellow

$hostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"
$hostsConfigured = $false
if (Test-Path $hostsPath) {
    $hostsConfigured = (Get-Content $hostsPath -ErrorAction SilentlyContinue) -match '(^|\s)domio\.home(\s|$)'
}

$cert = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Subject -eq "CN=domio.home" -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date)
    } |
    Select-Object -First 1

if (-not $hostsConfigured) {
    Write-Warning "Brak wpisu domio.home w pliku hosts. Uruchom wcześniej tools\Setup-DomioHome.ps1."
} else {
    Write-Host "      domio.home jest skonfigurowane." -ForegroundColor Green
}

if ($null -eq $cert) {
    Write-Warning "Nie znaleziono certyfikatu domio.home w CurrentUser\My. Uruchom wcześniej konfigurację domio.home."
} else {
    Write-Host "      Certyfikat HTTPS znaleziony." -ForegroundColor Green
}

Write-Host "[5/5] Tworzenie skrótu Domio na pulpicie..." -ForegroundColor Yellow

$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($ShortcutPath)
$shortcut.TargetPath = "powershell.exe"
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$StartScript`""
$shortcut.WorkingDirectory = $RepoRoot
$shortcut.Description = "Uruchom Domio"
$shortcut.IconLocation = (Join-Path $PublishDir "Domio.Web.exe") + ",0"
$shortcut.Save()

Write-Host ""
Write-Host "GOTOWE." -ForegroundColor Green
Write-Host "Na pulpicie utworzono skrót: Domio" -ForegroundColor Green
Write-Host "Adres aplikacji: https://domio.home/" -ForegroundColor Cyan
Write-Host ""
Write-Host "Domio bez Visual Studio nadal używa tej samej bazy:" -ForegroundColor White
Write-Host $DatabasePath -ForegroundColor White
Write-Host ""
Write-Host "Po zmianach w kodzie uruchom ten instalator ponownie, aby odświeżyć pliki programu." -ForegroundColor Yellow
Write-Host ""
Read-Host "Nacisnij Enter, aby zakonczyc"
