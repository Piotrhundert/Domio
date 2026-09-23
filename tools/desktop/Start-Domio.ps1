# Domio - uruchamianie bez Visual Studio

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$PublishDir = Join-Path $RepoRoot "dist\Domio"
$ExePath = Join-Path $PublishDir "Domio.Web.exe"
$DatabasePath = Join-Path $RepoRoot "Domio.Web\domio.db"
$Url = "https://domio.home/"

if (-not (Test-Path $ExePath)) {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show(
        "Nie znaleziono opublikowanej aplikacji Domio.`n`nUruchom najpierw:`ntools\desktop\Install-DomioDesktop.ps1",
        "Domio",
        "OK",
        "Warning"
    ) | Out-Null
    exit 1
}

# Jeżeli Domio już odpowiada, nie uruchamiamy drugiej instancji.
$alreadyRunning = $false
try {
    $response = Invoke-WebRequest -Uri ($Url + "health") -UseBasicParsing -TimeoutSec 2
    if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 600) {
        $alreadyRunning = $true
    }
} catch {
    $alreadyRunning = $false
}

if (-not $alreadyRunning) {
    # Korzystamy z obecnej konfiguracji lokalnej i z tej samej bazy co Visual Studio.
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "https://domio.home:443"
    $env:ConnectionStrings__DefaultConnection =
        "Data Source=$DatabasePath;Foreign Keys=True;Pooling=False"

    Start-Process `
        -FilePath $ExePath `
        -WorkingDirectory $PublishDir `
        -WindowStyle Hidden

    # Czekamy maksymalnie ok. 15 sekund na start serwera.
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 500
        try {
            $response = Invoke-WebRequest -Uri ($Url + "health") -UseBasicParsing -TimeoutSec 1
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 600) {
                break
            }
        } catch {
            # aplikacja jeszcze startuje
        }
    }
}

Start-Process $Url
