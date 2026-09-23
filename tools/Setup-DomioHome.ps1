# Domio - konfiguracja lokalnego adresu https://domio.home/
# Uruchom ten plik raz. Skrypt sam poprosi o uprawnienia administratora.

$ErrorActionPreference = "Stop"
$HostNameDomio = "domio.home"
$HostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"
$FriendlyName = "Domio local HTTPS"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Write-Host "Domio: potrzebne sa uprawnienia administratora do pliku hosts." -ForegroundColor Yellow
    Start-Process powershell.exe -Verb RunAs -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`""
    )
    exit
}

Write-Host ""
Write-Host "=== Domio: konfiguracja https://domio.home/ ===" -ForegroundColor Cyan

# 1. Plik hosts
$lines = @()
if (Test-Path $HostsPath) {
    $lines = Get-Content -LiteralPath $HostsPath
}

$escapedHost = [Regex]::Escape($HostNameDomio)
$filtered = @(
    $lines | Where-Object {
        $_ -notmatch "(^|\s)$escapedHost(\s|$)"
    }
)

$filtered += "127.0.0.1`t$HostNameDomio"
Set-Content -LiteralPath $HostsPath -Value $filtered -Encoding ASCII
Write-Host "[OK] $HostNameDomio -> 127.0.0.1" -ForegroundColor Green

# 2. Certyfikat HTTPS w magazynie bieżącego użytkownika
$now = Get-Date
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq "CN=$HostNameDomio" -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt $now.AddDays(30)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $cert) {
    $cert = New-SelfSignedCertificate `
        -DnsName $HostNameDomio `
        -Type SSLServerAuthentication `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -FriendlyName $FriendlyName `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable `
        -NotAfter $now.AddYears(5)

    Write-Host "[OK] Utworzono certyfikat HTTPS dla $HostNameDomio" -ForegroundColor Green
}
else {
    Write-Host "[OK] Istniejacy certyfikat HTTPS dla $HostNameDomio jest nadal wazny" -ForegroundColor Green
}

# 3. Zaufanie certyfikatowi przez Windows / Edge / Chrome
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
    "Root",
    [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
)
$rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)

try {
    $trusted = $rootStore.Certificates |
        Where-Object { $_.Thumbprint -eq $cert.Thumbprint } |
        Select-Object -First 1

    if ($null -eq $trusted) {
        $rootStore.Add($cert)
        Write-Host "[OK] Certyfikat dodano do Zaufanych glownych urzedow certyfikacji" -ForegroundColor Green
    }
    else {
        Write-Host "[OK] Certyfikat jest juz zaufany" -ForegroundColor Green
    }
}
finally {
    $rootStore.Close()
}

# 4. Odswiezenie DNS
ipconfig /flushdns | Out-Null

# 5. Kontrola portu 443
$port443 = Get-NetTCPConnection -LocalPort 443 -State Listen -ErrorAction SilentlyContinue
if ($port443) {
    Write-Warning "Port 443 jest teraz zajety przez inny proces. Domio moze nie wystartowac, dopoki ten port nie bedzie wolny."
    $port443 | Select-Object LocalAddress, LocalPort, OwningProcess | Format-Table
}
else {
    Write-Host "[OK] Port 443 jest wolny" -ForegroundColor Green
}

Write-Host ""
Write-Host "Konfiguracja zakonczona." -ForegroundColor Cyan
Write-Host "Uruchom ponownie Visual Studio, wybierz profil https i wystartuj Domio." -ForegroundColor White
Write-Host "Adres: https://domio.home/" -ForegroundColor Green
Write-Host ""
Read-Host "Nacisnij Enter, aby zamknac"
