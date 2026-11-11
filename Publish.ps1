$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    dotnet publish .\src\Sentinel.Agent\Sentinel.Agent.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o .\dist\agent
    if ($LASTEXITCODE -ne 0) { throw 'Agent publish failed' }
    dotnet publish .\src\Sentinel.Desktop\Sentinel.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o .\dist\desktop-fixed
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }
    Write-Host 'Executables in dist\agent and dist\desktop-fixed' -ForegroundColor Green
}
finally { Pop-Location }
