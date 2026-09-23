# Domio - usuniecie lokalnego adresu i certyfikatu domio.home

$ErrorActionPreference = "Stop"
$HostNameDomio = "domio.home"
$HostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`""
    )
    exit
}

if (Test-Path $HostsPath) {
    $escapedHost = [Regex]::Escape($HostNameDomio)
    $lines = Get-Content -LiteralPath $HostsPath
    $filtered = @(
        $lines | Where-Object {
            $_ -notmatch "(^|\s)$escapedHost(\s|$)"
        }
    )
    Set-Content -LiteralPath $HostsPath -Value $filtered -Encoding ASCII
}

Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq "CN=$HostNameDomio" } |
    Remove-Item -Force

Get-ChildItem Cert:\CurrentUser\Root |
    Where-Object { $_.Subject -eq "CN=$HostNameDomio" } |
    Remove-Item -Force

ipconfig /flushdns | Out-Null

Write-Host "Usunieto konfiguracje domio.home." -ForegroundColor Green
Read-Host "Nacisnij Enter, aby zamknac"
