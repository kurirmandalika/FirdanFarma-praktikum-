$ErrorActionPreference = "Stop"
Write-Host "Build Debug .NET Framework 4.8..."
dotnet build -c Debug -p:Platform=AnyCPU
$exe = Join-Path $PSScriptRoot "bin\Debug\net48\ApotekFirdanFarma.exe"
if (!(Test-Path $exe)) { throw "EXE tidak ditemukan: $exe" }
Write-Host "Menjalankan Firdan Farma..."
Start-Process $exe
