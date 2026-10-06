@echo off
title Memasang Shortcut Apotek Firdan Farma
echo ========================================================
echo   MEMASANG SHORTCUT APOTEK FIRDAN FARMA KE DESKTOP
echo ========================================================
echo.

set "TARGET=%~dp0ApotekFirdanFarma.exe"
set "DESKTOP=%USERPROFILE%\Desktop"
set "SHORTCUT=%DESKTOP%\Apotek Firdan Farma.lnk"

echo [1/2] Membuat shortcut di Desktop...
echo Set oWS = WScript.CreateObject("WScript.Shell") > "%temp%\mk_shortcut.vbs"
echo sLink = "%SHORTCUT%" >> "%temp%\mk_shortcut.vbs"
echo Set oLink = oWS.CreateShortcut(sLink) >> "%temp%\mk_shortcut.vbs"
echo oLink.TargetPath = "%TARGET%" >> "%temp%\mk_shortcut.vbs"
echo oLink.WorkingDirectory = "%~dp0" >> "%temp%\mk_shortcut.vbs"
echo oLink.Description = "Sistem Kasir & Stok Apotek Firdan Farma" >> "%temp%\mk_shortcut.vbs"
echo oLink.Save >> "%temp%\mk_shortcut.vbs"

cscript //nologo "%temp%\mk_shortcut.vbs"
if exist "%temp%\mk_shortcut.vbs" del "%temp%\mk_shortcut.vbs"

echo [2/2] Shortcut berhasil dibuat di Desktop!
echo.
echo ========================================================
echo   SELESAI! Ikon 'Apotek Firdan Farma' sudah ada di Desktop.
echo   Anda bisa langsung membukanya dari Desktop sekarang.
echo ========================================================
echo.
pause
