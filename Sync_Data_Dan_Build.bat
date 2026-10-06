@echo off
title Sinkronisasi Data Launching Apotek Firdan Farma
echo ========================================================
echo   SINKRONISASI DATA LAUNCHING APOTEK FIRDAN FARMA
echo ========================================================
echo.

echo [1/4] Menutup aplikasi jika sedang berjalan...
taskkill /F /IM ApotekFirdanFarma.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/4] Membersihkan database testing lama di komputer ini...
del /F /Q "%LOCALAPPDATA%\FirdanFarma\firdan_farma.db*" 2>nul

echo [3/4] Mengompilasi dan memperbarui aplikasi...
dotnet build -c Release -p:Platform=AnyCPU
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Gagal kompilasi! Pastikan .NET SDK sudah terpasang.
    pause
    exit /b %ERRORLEVEL%
)

powershell -ExecutionPolicy Bypass -File .\package-win7.ps1

echo [4/4] Selesai! Membuka aplikasi dengan data real launching...
echo.
echo Total 679 obat dan seluruh stok riil akan otomatis masuk!
echo.
start "" "dist\FirdanFarma-Win7-v5\ApotekFirdanFarma.exe"
pause
