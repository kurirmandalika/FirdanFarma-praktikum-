# Firdan Farma Desktop

Aplikasi desktop apotek offline berbasis **C# WinForms + .NET Framework 4.8 + SQLite**, dikembangkan di Windows 11 dan ditargetkan untuk diuji/dirilis ke Windows 7 SP1.

## Stack
- C# / WinForms
- .NET Framework 4.8 (`net48`)
- SQLite lokal (`System.Data.SQLite.Core`)
- ClosedXML untuk Excel `.xlsx`
- `System.Drawing.Printing` untuk struk Windows

## Cara development cepat
Di PowerShell pada folder project:

```powershell
.\run-debug.ps1
```

Atau:

```powershell
dotnet build -c Debug
.\bin\Debug\net48\ApotekFirdanFarma.exe
```

Di Visual Studio, buka `ApotekFirdanFarma.csproj` lalu tekan **F5**.

## Akun awal DEVELOPMENT
- Super Admin: `alfian` / `alfian`
- Karyawan: `apotek01` / `apotek01`

**Ganti password sebelum produksi.**

## Implementasi PRD yang sudah dimasukkan
- Login lokal, hashing password, role `KARYAWAN` dan `SUPER_ADMIN`
- Izin stok masuk dan izin koreksi stok per akun
- Dashboard berbeda untuk Karyawan dan Super Admin
- Master obat + soft delete
- AWL / MSK / KLR / SISA dengan invariant stok tidak negatif
- Batch sebagai data stok, expired date, saldo per batch
- FEFO untuk penjualan
- Koreksi batch/expired oleh Super Admin + reason + audit
- Penjualan Tunai/QRIS
- Kode T1/T2 dan Q1/Q2 terpisah per tanggal laporan
- Cut-off laporan otomatis pukul 15:00 tanpa menghapus histori
- Flow diskon YA/TIDAK sebelum transaksi final
- Pencegahan double-click pada tombol simpan transaksi
- Cetak struk melalui printer Windows
- Stok masuk / faktur masuk supplier + nomor faktur supplier + batch + expired + catatan, dibuka dari tab `Obat`
- Kartu stok dengan filter tanggal, batch, jenis, kode, user
- Stok opname per batch dengan draft, pemeriksaan, approval, dan posting selisih
- Pembatalan transaksi oleh Super Admin dengan pengembalian stok dan audit trail
- Laporan stok, Tunai, QRIS, gabungan, stok masuk, expired, batch, koreksi, diskon, audit
- User management untuk Super Admin
- Master kategori dan supplier
- Audit before/after, reason, nominal, diskon, pembayaran, batch, qty, status
- Backup/restore database
- Backup otomatis sebelum migrasi database dan operasi besar
- Export Excel obat dan laporan
- Laporan aktivitas ringkas Karyawan dan detail Super Admin
- Migrasi schema dari database versi sebelumnya ke v8 tanpa menghapus histori lama

## Lokasi data
Database aktif:

`%LOCALAPPDATA%\FirdanFarma\firdan_farma.db`

Backup:

`%LOCALAPPDATA%\FirdanFarma\backup\`

## Build Release

```powershell
.\build-win11.ps1
```

Output:

`bin\Release\net48\`

## Paket Windows 7

```powershell
.\package-win7.ps1
```

Output:

`dist\FirdanFarma-Win7-v5\`

Kirim **seluruh folder**, bukan hanya `.exe`.

### Syarat client
- Windows 7 SP1
- .NET Framework 4.8 Runtime
- Printer struk harus diuji menggunakan driver/printer client
- Fungsi utama tidak membutuhkan internet

## Catatan produksi
PRD mensyaratkan kompatibilitas Windows 7 dibuktikan dengan uji langsung. Build berhasil di Windows 11 belum cukup untuk menyatakan acceptance Windows 7 selesai. Jalankan seluruh skenario di `MANUAL_TESTS.md` pada mesin/VM Windows 7 target sebelum go-live.

## Belum final sesuai PRD
- Modul faktur penuh (kebutuhan client belum final)
- Barcode scanner
- Sinkronisasi cloud
- Multi cabang / multi terminal real-time

File `PRD.md` adalah satu-satunya dokumen PRD utama untuk kebutuhan project ini.

## Dokumen pendamping
- `PRD.md` - PRD utama project.
- `PANDUAN_PENGGUNAAN.md` - fitur dan cara menggunakan aplikasi.
- `CHANGELOG_v5.md` - ringkasan perubahan dari fondasi sebelumnya.
- `MANUAL_TESTS.md` - checklist acceptance sebelum go-live.
