$ErrorActionPreference = "Stop"
Write-Host "Restore package..."
dotnet restore
Write-Host "Build Release .NET Framework 4.8..."
dotnet build -c Release -p:Platform=AnyCPU
Write-Host "Selesai. Output: bin\Release\net48\"
