# Changelog — PRD v5.0

## Database & migrasi
- Schema dinaikkan ke versi 5.
- Tambah `medicine_batches`, `stock_movements`, `sale_item_batches`.
- Tambah field permission user, status/note/grand total sales, invoice/purchase date purchases, dan detail audit.
- Migrasi histori lot/mutasi v4 ke struktur v5.
- Backup otomatis sebelum migrasi database lama.

## Penjualan
- Tunai/QRIS saja untuk flow utama.
- Kode T/Q terpisah per tanggal laporan.
- Cut-off otomatis 15:00 tanpa reset/hapus histori.
- Diskon YA/TIDAK sebelum final dan validasi nominal.
- FEFO per batch dan mapping batch per sale item.
- Pencegahan double submit dari UI.
- Pembatalan oleh Super Admin dengan reason, restore stok/batch, dan audit.
- Cetak struk Windows.

## Stok & pembelian
- Batch/expired menjadi data stok aktual.
- Kartu stok baru berbasis `stock_movements`.
- Stok Masuk mencatat faktur supplier, batch/expired per item.
- Koreksi stok dan batch dibatasi izin/role dan wajib audit.

## User, audit, dashboard, laporan
- Dua role dengan tampilan/akses berbeda.
- Permission pembelian dan koreksi stok.
- Audit detail before/after, qty, diskon, pembayaran, reason, status.
- Dashboard role-aware.
- Laporan Tunai, QRIS, gabungan, stok, pembelian, expired, batch, koreksi, diskon, audit.

## Tooling development
- `run-debug.ps1` untuk build + run Debug.
- `build-win11.ps1` untuk Release.
- `package-win7.ps1` untuk folder distribusi Win7.
- VS Code tasks/launch diperbarui.
