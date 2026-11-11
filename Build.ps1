$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    dotnet build .\src\Sentinel.Agent\Sentinel.Agent.csproj -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'Agent build failed' }
    dotnet build .\src\Sentinel.Desktop\Sentinel.Desktop.csproj -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }
    Write-Host 'Build complete.' -ForegroundColor Green
}
finally { Pop-Location }
