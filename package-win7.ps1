$ErrorActionPreference = "Stop"
dotnet build -c Release -p:Platform=AnyCPU
$src = Join-Path $PSScriptRoot "bin\Release\net48"
$dst = Join-Path $PSScriptRoot "dist\FirdanFarma-Win7-v5"
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
New-Item $dst -ItemType Directory -Force | Out-Null
Copy-Item "$src\*" $dst -Recurse -Force
Copy-Item "$PSScriptRoot\README.md" $dst -Force
Copy-Item "$PSScriptRoot\MANUAL_TESTS.md" $dst -Force
if (Test-Path "$PSScriptRoot\dist\Manual_Karyawan_FirdanFarma.pdf") {
    Copy-Item "$PSScriptRoot\dist\Manual_Karyawan_FirdanFarma.pdf" $dst -Force
}
if (Test-Path "$PSScriptRoot\dist\Manual_SuperAdmin_FirdanFarma.pdf") {
    Copy-Item "$PSScriptRoot\dist\Manual_SuperAdmin_FirdanFarma.pdf" $dst -Force
}
Copy-Item "$PSScriptRoot\Pasang_Ke_Desktop.bat" $dst -Force
Write-Host "Paket portable dibuat di: $dst"
Write-Host "PC client wajib diuji langsung: Windows 7 SP1 + .NET Framework 4.8."
