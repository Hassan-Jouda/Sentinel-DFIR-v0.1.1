# Repairs WPF desktop publish layout only. Does not touch SentinelAgent service or its log data.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    $output = Join-Path $root 'dist\desktop-fixed'
    if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    dotnet publish .\src\Sentinel.Desktop\Sentinel.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }
    $exe = Join-Path $output 'Sentinel.Desktop.exe'
    $dll = Join-Path $output 'Sentinel.Desktop.dll'
    if (-not (Test-Path $exe) -or -not (Test-Path $dll)) { throw 'Expected EXE/DLL were not created.' }
    Write-Host 'Desktop published in multi-file mode:' -ForegroundColor Green
    Write-Host $exe
    Write-Host 'Run it with:' -ForegroundColor Green
    Write-Host '.\dist\desktop-fixed\Sentinel.Desktop.exe'
    Write-Host 'Keep all files together in desktop-fixed; do not copy the EXE by itself.' -ForegroundColor Yellow
}
finally { Pop-Location }
