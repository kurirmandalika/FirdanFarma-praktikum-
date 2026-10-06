# Manual Acceptance Test — Firdan Farma PRD v5

Gunakan database testing/backup sebelum uji.

## 1. Login dan role

- [ ] `alfian` dapat login sebagai `SUPER_ADMIN`.
- [ ] `apotek01` dapat login sebagai `KARYAWAN`.
- [ ] Password salah ditolak dan tercatat sebagai audit gagal.
- [ ] Karyawan tidak dapat membuka manajemen pengguna/audit detail.
- [ ] Super Admin dapat mengubah akun, role, password, status, izin stok masuk/koreksi.

## 2. Cut-off 15:00

Uji menggunakan data/waktu sistem yang terkontrol.

- [ ] 14:59 -> `tanggal_laporan` hari kalender yang sama.
- [ ] 15:00 -> `tanggal_laporan` hari berikutnya.
- [ ] Histori periode sebelumnya tetap dapat dibuka.

## 3. Kode transaksi

- [ ] Tunai pertama periode = T1.
- [ ] Tunai berikutnya = T2.
- [ ] QRIS pertama periode = Q1.
- [ ] Urutan T dan Q terpisah.
- [ ] Periode baru mulai kembali dari 1.
- [ ] Tidak ada kode duplikat.

## 4. Stok dan batch

- [ ] Stok Masuk menambah MSK dan SISA.
- [ ] Stok Masuk membuat batch dan expired date.
- [ ] Tombol Stok Masuk di tab Obat menampilkan pilihan Tunggal dan Batch (Faktur).
- [ ] Mode Tunggal dapat menyimpan satu obat tanpa supplier atau nomor faktur.
- [ ] Mode Batch (Faktur) menyimpan header dan beberapa detail obat sekaligus.
- [ ] Nomor faktur yang sama ditolak agar tidak tercatat dua kali.
- [ ] Harga jual dapat diisi berbeda untuk setiap baris stok masuk.
- [ ] Satu obat dapat memiliki beberapa batch.
- [ ] Penjualan mengurangi KLR/SISA.
- [ ] Stok tidak pernah negatif.
- [ ] Batch expired terdekat digunakan lebih dahulu (FEFO).
- [ ] Kartu stok menampilkan T/Q, batch, expired, masuk/keluar, saldo.
- [ ] Koreksi batch/expired meminta alasan dan muncul di audit.

## 5. Stok opname

- [ ] User dapat membuat sesi opname baru.
- [ ] Snapshot memasukkan semua obat aktif, batch, expired date, dan stok legacy tanpa batch.
- [ ] Stok fisik tidak boleh negatif.
- [ ] Item yang belum dihitung tidak dapat diselesaikan.
- [ ] Item yang memiliki selisih wajib memiliki alasan.
- [ ] Super Admin dapat menyimpan hitungan stok fisik.
- [ ] Super Admin dapat menyelesaikan atau membatalkan opname.
- [ ] Penjualan, stok masuk, pembatalan, dan koreksi stok ditolak selama opname aktif.
- [ ] Penyelesaian membuat mutasi STOK_OPNAME dan memperbarui saldo batch/obat.
- [ ] Penyelesaian ditolak jika stok berubah selama opname.
- [ ] Opname selesai tidak dapat diedit atau dibatalkan.
- [ ] Hasil opname dapat diexport ke Excel dan muncul di audit.

## 6. Diskon

- [ ] Sebelum simpan muncul pertanyaan diskon YA/TIDAK.
- [ ] TIDAK menghasilkan diskon 0.
- [ ] YA meminta nominal.
- [ ] Diskon negatif/tidak valid ditolak.
- [ ] Diskon > subtotal ditolak.
- [ ] Total akhir = subtotal - diskon.
- [ ] Diskon muncul di laporan dan audit.

## 7. Pembatalan

- [ ] Hanya Super Admin dapat membatalkan transaksi.
- [ ] Alasan wajib.
- [ ] Status menjadi DIBATALKAN.
- [ ] Stok kembali.
- [ ] Batch kembali bila mapping batch tersedia.
- [ ] Audit menyimpan siapa, kapan, alasan, nominal, metode, before/after.

## 8. Laporan

- [ ] Tunai terpisah dari QRIS.
- [ ] Gabungan menampilkan total Tunai, QRIS, diskon, bersih, jumlah transaksi.
- [ ] Laporan stok masuk tersedia.
- [ ] Laporan expired/batch tersedia.
- [ ] Laporan koreksi/diskon/audit tersedia untuk Super Admin.
- [ ] Export Excel menghasilkan file yang dapat dibuka.

## 9. Backup/restore

- [ ] Backup manual menghasilkan `.db`.
- [ ] Restore hanya Super Admin.
- [ ] Restore meminta konfirmasi.
- [ ] Backup keselamatan dibuat sebelum restore.
- [ ] Migrasi database lama membuat backup pre-migration.
- [ ] Data dapat dibaca ulang setelah restart.

## 10. Struk

- [ ] Struk dapat dipreview/dikirim ke printer Windows yang terpasang.
- [ ] Kode, waktu, kasir, metode, item, subtotal, diskon, total terbaca.
- [ ] Uji langsung pada printer client.

## 11. Windows 7 SP1 — WAJIB sebelum produksi

- [ ] EXE terbuka di Windows 7 SP1 + .NET Framework 4.8.
- [ ] SQLite database dibuat dan dibaca.
- [ ] Login berjalan.
- [ ] Penjualan Tunai dan QRIS berjalan.
- [ ] Stok Masuk + batch berjalan.
- [ ] Kartu stok berjalan.
- [ ] Cetak struk berjalan pada printer client.
- [ ] Backup/restore berjalan.
- [ ] Fungsi utama berjalan tanpa internet.
