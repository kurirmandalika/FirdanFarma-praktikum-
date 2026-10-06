# PRD Aplikasi Apotek Firdan Farma

**Versi dokumen:** 1.0  
**Tanggal:** 21 Agustus 2026  
**Platform:** Windows Desktop  
**Target runtime:** Windows 7 SP1, .NET Framework 4.8  
**Mode operasi:** Offline dengan database lokal SQLite  
**Status produk:** Siap diuji operasional; kompatibilitas Windows 7 tetap harus diuji pada perangkat target.

## 1. Ringkasan

Firdan Farma adalah aplikasi desktop untuk operasional apotek secara offline. Aplikasi membantu karyawan dan pemilik/Super Admin mengelola data obat, stok, batch, expired date, transaksi penjualan, stok masuk supplier, laporan, audit aktivitas, dan backup database.

Fokus utama aplikasi adalah menjaga stok dan laporan tetap akurat tanpa menghapus histori. Semua transaksi penting harus bisa ditelusuri kembali: siapa yang melakukan, kapan dilakukan, data apa yang berubah, stok sebelum/sesudah, metode pembayaran, nominal, diskon, dan alasan koreksi bila ada.

## 2. Tujuan Produk

1. Mempercepat transaksi penjualan obat di kasir.
2. Menampilkan stok aktual setiap obat.
3. Mencatat stok masuk, stok keluar, batch, dan expired date.
4. Memisahkan laporan penjualan Tunai dan QRIS.
5. Menggunakan cut-off laporan harian pukul 15.00 tanpa menghapus histori.
6. Memberikan kode transaksi terpisah untuk Tunai dan QRIS.
7. Mencatat diskon sebelum transaksi final.
8. Memberikan akses berbeda untuk Karyawan dan Super Admin.
9. Menyediakan audit aktivitas untuk pengecekan kesalahan input.
10. Menyediakan backup dan restore database lokal.

## 3. Pengguna dan Hak Akses

### 3.1 Karyawan

Karyawan menjalankan aktivitas operasional harian.

Hak akses:

- login aplikasi;
- melakukan penjualan;
- memilih metode pembayaran Tunai atau QRIS;
- memasukkan diskon sebelum transaksi final;
- mencetak struk;
- melihat stok;
- melihat kartu stok sesuai batas akses;
- input stok masuk atau koreksi stok hanya jika diberi izin;
- melihat ringkasan aktivitas miliknya.

Batasan:

- tidak boleh mengelola akun;
- tidak boleh melihat audit detail semua user;
- tidak boleh melakukan restore database;
- tidak boleh membatalkan transaksi final tanpa izin Super Admin.

### 3.2 Super Admin

Super Admin memiliki seluruh akses Karyawan ditambah akses pengawasan dan administrasi.

Hak akses:

- mengelola akun user;
- mengatur role dan izin;
- melihat seluruh aktivitas user;
- melihat audit detail sebelum/sesudah perubahan;
- melakukan pembatalan transaksi dengan alasan wajib;
- melakukan koreksi stok, batch, dan expired date dengan alasan wajib;
- melihat laporan lengkap;
- melakukan backup dan restore database;
- melihat data transaksi dibatalkan, diskon, koreksi, dan aktivitas berisiko.

### 3.3 Akun Awal Development

- `alfian` dengan role `SUPER_ADMIN`
- `apotek01` dengan role `KARYAWAN`

Password akun awal wajib diganti sebelum produksi. Password tidak boleh disimpan dalam bentuk plaintext.

## 4. Ruang Lingkup Fitur

### 4.1 Wajib Operasional

Fitur berikut wajib tersedia sebelum aplikasi dipakai operasional:

| Area            | Kebutuhan                                                                        |
| --------------- | -------------------------------------------------------------------------------- |
| Login           | Login lokal, password hash, role Karyawan dan Super Admin                        |
| Master obat     | Tambah, ubah, nonaktifkan, cari, dan validasi data obat                          |
| Stok            | AWL, MSK, KLR, SISA, stok tidak boleh negatif                                    |
| Batch           | Nomor batch, expired date, saldo per batch                                       |
| Stok Masuk      | Input stok masuk supplier, faktur supplier, batch per item                       |
| Penjualan       | Keranjang, validasi stok, Tunai/QRIS, diskon, struk                              |
| Kode transaksi  | `T1`, `T2`, dst. untuk Tunai; `Q1`, `Q2`, dst. untuk QRIS                        |
| Cut-off laporan | Periode laporan berpindah otomatis pukul 15.00                                   |
| Kartu stok      | Riwayat masuk/keluar, batch, saldo sebelum/sesudah                               |
| Stok opname     | Snapshot stok, hitung fisik per batch, selisih, pemeriksaan, dan posting koreksi |
| Laporan         | Tunai, QRIS, gabungan, stok, stok masuk, expired, batch, audit                   |
| Audit           | Catat user, waktu, aksi, nilai sebelum/sesudah, nominal, alasan                  |
| Backup          | Backup manual, restore, dan safety backup sebelum operasi besar                  |
| Windows 7       | Build produksi harus diuji langsung di Windows 7 target                          |

### 4.2 Penting Setelah Operasional Stabil

- dashboard Super Admin dengan grand total;
- laporan diskon;
- laporan koreksi;
- ekspor Excel;
- laporan expired dan stok rendah;
- arsip/cetak laporan sesuai kebutuhan client.

### 4.3 Di Luar Scope Saat Ini

- modul faktur penuh;
- barcode scanner;
- sinkronisasi cloud;
- multi cabang;
- multi terminal real-time;
- analitik lanjutan.

## 5. Aturan Bisnis Utama

### 5.1 Cut-off Laporan Pukul 15.00

Hari laporan apotek tidak mengikuti pergantian tanggal pukul 00.00.

Aturan:

- transaksi sebelum pukul 15.00 masuk tanggal laporan hari kalender transaksi;
- transaksi mulai pukul 15.00 masuk tanggal laporan hari berikutnya;
- histori transaksi lama tidak boleh dihapus;
- setiap transaksi menyimpan `transaction_datetime` dan `report_date`.

Contoh:

| Waktu transaksi       | Tanggal kalender | Tanggal laporan |
| --------------------- | ---------------- | --------------- |
| 19 Agustus 2026 14:59 | 19 Agustus 2026  | 19 Agustus 2026 |
| 19 Agustus 2026 15:00 | 19 Agustus 2026  | 20 Agustus 2026 |
| 19 Agustus 2026 22:00 | 19 Agustus 2026  | 20 Agustus 2026 |
| 20 Agustus 2026 08:00 | 20 Agustus 2026  | 20 Agustus 2026 |

Rumus:

```text
jika waktu transaksi < 15:00
    report_date = tanggal kalender transaksi
jika waktu transaksi >= 15:00
    report_date = tanggal kalender transaksi + 1 hari
```

### 5.2 Kode Transaksi

Kode transaksi mengikuti tanggal laporan dan metode pembayaran.

Aturan:

- Tunai menggunakan prefix `T`, misalnya `T1`, `T2`, `T3`;
- QRIS menggunakan prefix `Q`, misalnya `Q1`, `Q2`, `Q3`;
- nomor urut Tunai dan QRIS terpisah;
- nomor urut dimulai dari 1 pada setiap tanggal laporan baru;
- kode transaksi tidak boleh duplikat dalam tanggal laporan yang sama.

### 5.3 Stok Obat

Stok ringkas menggunakan formula:

```text
SISA = AWL + MSK - KLR
```

Keterangan:

- AWL = stok awal;
- MSK = total stok masuk;
- KLR = total stok keluar;
- SISA = stok tersedia.

Aturan:

- stok tidak boleh negatif;
- obat yang sudah memiliki histori transaksi tidak boleh dihapus permanen;
- obat dinonaktifkan dengan soft delete;
- kolom supplier bukan bagian dari master obat;
- perubahan penting harus masuk audit log.

### 5.4 Batch dan Expired Date

Setiap stok masuk harus dapat mencatat:

- supplier;
- nomor faktur supplier;
- obat;
- jumlah masuk;
- harga beli;
- nomor batch;
- expired date;
- user input;
- waktu input;
- catatan.

Satu obat dapat memiliki banyak batch. Penjualan menggunakan prinsip FEFO atau First Expired First Out, yaitu batch dengan expired date terdekat dipakai terlebih dahulu.

### 5.5 Diskon

Sebelum transaksi final, sistem menanyakan apakah transaksi mendapat diskon.

Jika tidak:

- diskon = Rp0;
- total akhir = subtotal.

Jika ya:

- user memasukkan nominal diskon;
- diskon tidak boleh negatif;
- diskon tidak boleh lebih besar dari subtotal;
- total akhir = subtotal - diskon;
- diskon tersimpan di transaksi, laporan, dan audit.

### 5.6 Koreksi dan Pembatalan

Transaksi final tidak boleh diubah diam-diam.

Aturan:

- pembatalan transaksi hanya oleh Super Admin;
- pembatalan wajib mengisi alasan;
- stok dan batch dikembalikan sesuai transaksi yang dibatalkan;
- audit mencatat user, waktu, transaksi, alasan, nilai, dan stok terkait;
- workflow aman untuk salah input adalah batalkan transaksi lalu input ulang.

## 6. Alur Utama

### 6.1 Alur Penjualan

1. User login.
2. User memilih obat dan qty.
3. Sistem mengecek stok.
4. Sistem memilih batch FEFO.
5. Sistem menghitung subtotal.
6. User memilih Tunai atau QRIS.
7. Sistem menanyakan diskon.
8. User mengonfirmasi transaksi.
9. Sistem menyimpan transaksi.
10. Sistem mengurangi stok dan saldo batch.
11. Sistem membuat kartu stok.
12. Sistem membuat audit log.
13. Sistem memberi kode transaksi sesuai tanggal laporan.
14. Struk dapat dicetak.

### 6.2 Alur Stok Masuk Supplier

1. User membuka menu `Obat` lalu klik tombol `Stok Masuk`.
2. Layar awal menampilkan pilihan `Tunggal` atau `Batch (Faktur)`.
3. Mode `Tunggal` digunakan untuk satu obat dan tidak mewajibkan supplier atau nomor faktur.
4. Mode `Batch (Faktur)` dimulai dengan pengisian nama supplier/faktur dan nomor faktur.
5. User menambahkan obat-obat dari faktur fisik satu per satu ke daftar baris.
6. Setiap baris mencatat nama obat, qty, satuan, harga beli, diskon, PPN, harga jual, batch, dan expired date.
7. Sistem menolak nomor faktur yang sama agar satu faktur tidak tercatat dua kali.
8. Setelah submit, sistem menyimpan header dan semua detail dalam satu transaksi.
9. Sistem memperbarui atau membuat master obat, menambah MSK, SISA, dan saldo batch.
10. Sistem membuat kartu stok, laporan stok masuk, dan audit log.

### 6.3 Alur Backup dan Restore

Backup:

1. Super Admin membuka menu backup.
2. Sistem membuat salinan database.
3. Sistem menampilkan lokasi file backup.
4. Audit log dicatat.

Restore:

1. Super Admin memilih file backup.
2. Sistem meminta konfirmasi.
3. Sistem membuat safety backup sebelum restore.
4. Sistem mengganti database aktif.
5. Audit log dicatat.

## 7. Data Utama

### 7.1 User

Data minimal:

- username;
- password hash;
- nama tampilan;
- role;
- status aktif;
- tanggal dibuat;
- tanggal diperbarui.

### 7.2 Obat

Data minimal:

- kode obat;
- nama obat;
- kategori;
- satuan;
- harga beli;
- harga jual;
- stok minimal;
- AWL;
- MSK;
- KLR;
- SISA;
- status aktif.

### 7.3 Batch Obat

Data minimal:

- obat;
- nomor batch;
- expired date;
- qty masuk;
- qty keluar;
- qty tersedia;
- supplier;
- referensi stok masuk.

### 7.4 Penjualan

Data minimal:

- kode transaksi;
- tanggal/jam transaksi;
- tanggal laporan;
- metode pembayaran;
- subtotal;
- diskon;
- total akhir;
- user;
- status;
- catatan.

### 7.5 Detail Penjualan

Data minimal:

- transaksi;
- obat;
- batch;
- qty;
- harga satuan;
- subtotal item.

### 7.6 Stok Masuk

Data minimal:

- nomor stok masuk;
- supplier;
- nomor faktur supplier;
- tanggal stok masuk;
- total;
- user;
- catatan.

### 7.7 Kartu Stok

Data minimal:

- obat;
- batch;
- waktu mutasi;
- jenis mutasi;
- referensi transaksi;
- qty masuk;
- qty keluar;
- stok sebelum;
- stok sesudah;
- metode pembayaran bila berasal dari penjualan;
- user;
- alasan/catatan.

### 7.8 Audit Log

Data minimal:

- user;
- role;
- waktu;
- aksi;
- entitas;
- referensi;
- nilai sebelum;
- nilai sesudah;
- nominal;
- metode pembayaran;
- alasan;
- catatan;
- status.

## 8. Laporan

Laporan yang tersedia:

1. Laporan penjualan Tunai.
2. Laporan penjualan QRIS.
3. Laporan penjualan gabungan.
4. Laporan stok.
5. Kartu stok.
6. Laporan stok masuk.
7. Laporan batch.
8. Laporan expired/dekat expired.
9. Laporan stok rendah.
10. Laporan diskon.
11. Laporan koreksi stok.
12. Laporan audit aktivitas.

Filter minimal:

- tanggal laporan;
- rentang tanggal;
- metode pembayaran;
- user;
- obat;
- batch;
- kode transaksi;
- jenis mutasi.

Laporan penting sebaiknya dapat diunduh ke Excel sesuai tanggal/rentang yang dipilih, dan file stok masuk/penjualan harian harus memberi sekat per tanggal agar tidak tercampur.

## 9. Kebutuhan Non-Fungsional

### 9.1 Kompatibilitas

- Aplikasi berjalan sebagai desktop app Windows.
- Target client adalah Windows 7 SP1.
- Runtime target adalah .NET Framework 4.8.
- Fungsi utama tidak membutuhkan internet.
- Aplikasi dapat dijalankan dari folder hasil build/package.

### 9.2 Keamanan

- Password wajib di-hash.
- Role diperiksa pada fitur sensitif.
- Operasi berisiko meminta konfirmasi.
- Audit log mencatat aksi penting.
- Karyawan tidak boleh mengakses audit sensitif semua user.

### 9.3 Keandalan Data

- Database lokal menggunakan SQLite.
- Transaksi final tidak boleh tersimpan ganda akibat double-click.
- Nomor transaksi harus unik.
- Stok tidak boleh negatif.
- Histori lama tidak boleh rusak saat migrasi.
- Backup dibuat sebelum migrasi, restore, atau operasi besar.

### 9.4 Printer

- Cetak struk menggunakan printer Windows.
- Printer struk wajib diuji pada perangkat client.
- Hasil cetak harus memuat kode transaksi, item, qty, subtotal, diskon, total, metode pembayaran, dan waktu transaksi.

## 10. Kriteria Penerimaan

### 10.1 Login dan Role

- Karyawan dapat login dan melihat menu operasional.
- Super Admin dapat login dan melihat menu administrasi.
- Karyawan tidak dapat membuka fitur khusus Super Admin.
- Super Admin dapat mengelola akun user.

### 10.2 Penjualan

- Penjualan Tunai berhasil tersimpan.
- Penjualan QRIS berhasil tersimpan.
- Diskon dapat diisi sebelum final transaksi.
- Diskon tidak boleh melebihi subtotal.
- Stok berkurang sesuai qty terjual.
- Batch yang keluar mengikuti FEFO.
- Struk dapat dicetak.
- Double-click tombol simpan tidak membuat transaksi ganda.

### 10.3 Kode dan Cut-off

- Transaksi Tunai mendapat kode `T1`, `T2`, dst.
- Transaksi QRIS mendapat kode `Q1`, `Q2`, dst.
- Urutan Tunai dan QRIS terpisah.
- Transaksi pukul 14:59 masuk laporan hari berjalan.
- Transaksi pukul 15:00 masuk laporan hari berikutnya.
- Histori laporan lama tetap tersedia.

### 10.4 Stok dan Batch

- Stok Masuk menambah stok.
- Penjualan mengurangi stok.
- Satu obat dapat memiliki banyak batch.
- Batch menyimpan expired date.
- Stok tidak dapat menjadi negatif.
- Kartu stok menampilkan mutasi dan saldo.

### 10.5 Stok Opname

- User dapat membuat sesi stok opname dan menyimpan hitungan sebagai draft.
- Snapshot mencakup semua obat aktif, batch, expired date, dan stok legacy tanpa batch.
- Stok fisik tidak boleh negatif dan selisih wajib memiliki alasan.
- Stok opname dikelola oleh Super Admin; Super Admin menyimpan hitungan, menyelesaikan, atau membatalkan opname.
- Transaksi stok ditahan selama opname aktif agar snapshot tetap akurat.
- Penyelesaian membuat mutasi `STOK_OPNAME_MASUK` atau `STOK_OPNAME_KELUAR` tanpa menghapus histori.
- Sesi selesai tidak dapat diedit atau dibatalkan.

### 10.6 Audit

- Penjualan tercatat di audit.
- Stok Masuk tercatat di audit.
- Koreksi stok tercatat di audit.
- Pembatalan transaksi tercatat di audit.
- Audit menyimpan user, waktu, aksi, nominal, metode pembayaran, dan alasan bila diperlukan.
- Super Admin dapat melihat detail audit.

### 10.7 Backup dan Restore

- Backup manual berhasil membuat file database.
- Restore hanya dapat dilakukan Super Admin.
- Restore meminta konfirmasi.
- Safety backup dibuat sebelum restore.
- Backup dan restore tercatat di audit.

### 10.7 Windows 7

- Aplikasi dapat dibuka pada Windows 7 SP1 target.
- Login berjalan pada Windows 7.
- Database dapat dibuat dan dibaca.
- Penjualan berjalan pada Windows 7.
- Cetak struk berjalan pada printer client.
- Backup dan restore berjalan pada Windows 7.
- Fungsi utama berjalan tanpa internet.

## 11. Status Implementasi Saat Ini

| Fitur                             | Status                          |
| --------------------------------- | ------------------------------- |
| Login, role, dan user management  | Sudah ada                       |
| Master obat dan soft delete       | Sudah ada                       |
| Stok AWL/MSK/KLR/SISA             | Sudah ada                       |
| Batch dan expired date            | Sudah ada                       |
| FEFO saat penjualan               | Sudah ada                       |
| Stok Masuk supplier               | Sudah ada                       |
| Penjualan Tunai/QRIS              | Sudah ada                       |
| Diskon sebelum final transaksi    | Sudah ada                       |
| Kode transaksi T/Q                | Sudah ada                       |
| Cut-off laporan 15.00             | Sudah ada                       |
| Kartu stok                        | Sudah ada                       |
| Stok opname                       | Sudah ada                       |
| Laporan operasional               | Sudah ada                       |
| Audit aktivitas                   | Sudah ada                       |
| Backup/restore                    | Sudah ada                       |
| Export Excel                      | Sudah ada                       |
| Modul faktur penuh                | Belum final                     |
| Barcode scanner                   | Belum ada                       |
| Cloud sync/multi cabang           | Belum ada                       |
| Uji Windows 7 di perangkat target | Wajib dilakukan sebelum go-live |

## 12. Definisi Selesai

Sebuah fitur dianggap selesai jika:

- UI dapat digunakan oleh role yang tepat;
- data tersimpan ke database;
- data dapat dibaca ulang;
- validasi berjalan;
- stok dan laporan konsisten;
- audit log dibuat untuk aksi penting;
- error ditampilkan dengan pesan yang jelas;
- skenario utama berhasil diuji;
- untuk rilis produksi, build sudah diuji pada Windows 7 target.

## 13. Catatan Keputusan Produk

1. Reset pukul 15.00 berarti pergantian periode laporan, bukan hapus data.
2. Tanggal laporan dapat berbeda dari tanggal kalender transaksi.
3. Tunai dan QRIS memiliki nomor urut sendiri.
4. Batch dan expired date adalah bagian dari stok.
5. Karyawan tidak boleh melihat audit sensitif semua user.
6. Koreksi penting wajib punya alasan.
7. Super Admin harus dapat mengetahui siapa melakukan apa, kapan, dan apa dampaknya.
8. Faktur penuh belum menjadi scope final sampai kebutuhan client diperjelas.
