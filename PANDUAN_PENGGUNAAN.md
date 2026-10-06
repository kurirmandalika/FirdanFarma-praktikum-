# Panduan Penggunaan Firdan Farma

Dokumen ini menjelaskan fitur utama dan cara penggunaan aplikasi Firdan Farma untuk kasir, karyawan, dan Super Admin.

## 1. Memulai aplikasi

### Login awal

| Peran | Username | Password awal |
|---|---|---|
| Super Admin | `alfian` | `alfian` |
| Karyawan | `apotek01` | `apotek01` |

Segera gunakan menu **Pengguna** untuk mengganti password awal. Password tidak boleh dibagikan kepada orang lain.

### Menu sidebar

Sidebar menggunakan nama singkat agar mudah dipahami:

| Menu | Kegunaan |
|---|---|
| **Beranda** | Melihat ringkasan penjualan, stok, dan kondisi aplikasi. |
| **Obat** | Melihat, mencari, mengubah, menonaktifkan data obat, dan membuka stok masuk. |
| **Penjualan** | Mencatat transaksi penjualan ke pembeli. |
| **Kartu Stok** | Melihat riwayat dan saldo stok per obat. |
| **Koreksi Stok** | Mencatat penyesuaian stok karena hasil pemeriksaan fisik. |
| **Laporan** | Membuka laporan penjualan, stok, stok masuk, dan koreksi. |
| **Aktivitas** | Melihat jejak aktivitas user sesuai hak akses. |
| **Pengguna** | Mengelola user dan password. |
| **Data Pendukung** | Mengelola supplier, kategori, satuan, dan pengaturan pendukung. |
| **Backup Data** | Membuat backup, restore, atau export data. Menu ini khusus Super Admin. |
| **Ganti Pengguna** | Keluar dari user saat ini dan kembali ke halaman login. |

Karyawan hanya melihat menu yang diperlukan untuk pekerjaan sehari-hari. Detail audit dan pengaturan penting dibatasi untuk Super Admin.

## 2. Data obat

1. Buka menu **Obat**.
2. Gunakan pencarian berdasarkan nama, kode, atau barcode bila tersedia.
3. Gunakan tombol **Stok Masuk** untuk menambah stok dan membuat obat baru bila belum ada.
4. Untuk memperbaiki data, pilih obat lalu **Edit**.
5. Untuk menghentikan pemakaian obat, gunakan status nonaktif.

Data obat tidak lagi menyimpan supplier sebagai kolom master. Supplier dicatat pada stok masuk/faktur supaya sumber kedatangan tetap terlihat di laporan.

### Batch dan tanggal kedaluwarsa

Stok dapat memiliki beberapa batch. Saat menerima stok masuk, isi nomor batch dan tanggal kedaluwarsa jika tercantum pada kemasan. Penjualan menggunakan batch yang paling dekat tanggal kedaluwarsanya terlebih dahulu (FEFO).

Stok yang sudah kedaluwarsa tidak boleh dijual. Jika stok yang tersedia hanya batch kedaluwarsa, transaksi akan ditolak sampai stok tersebut dikoreksi atau ditangani sesuai prosedur internal.

## 3. Mencatat penjualan

1. Buka menu **Penjualan**.
2. Cari obat, masukkan jumlah, lalu tambahkan ke daftar transaksi.
3. Periksa harga, diskon, total, dan stok yang terpakai.
4. Pilih metode pembayaran **Tunai** atau **QRIS**.
5. Masukkan nominal pembayaran bila diperlukan.
6. Simpan transaksi dan cetak struk bila printer tersedia.

Setiap transaksi diberi kode berdasarkan metode pembayaran:

- Tunai: `T1`, `T2`, `T3`, dan seterusnya.
- QRIS: `Q1`, `Q2`, `Q3`, dan seterusnya.

Kode dibuat berurutan dalam periode laporan aktif. Jangan menghapus transaksi yang sudah tersimpan untuk memperbaiki kesalahan; gunakan pembatalan atau **Koreksi Stok** sesuai kebutuhan dan isi alasan yang jelas.

## 4. Periode laporan pukul 15.00

Pergantian laporan terjadi setiap hari pada pukul **15.00**. Sistem membagi transaksi ke periode sebelum dan sesudah waktu tersebut, sehingga rekap tetap tersimpan dan tidak hilang ketika aplikasi ditutup.

Komputer tidak harus menyala terus-menerus. Periode ditentukan dari waktu transaksi yang tersimpan di database saat aplikasi digunakan. Pastikan tanggal dan jam Windows benar sebelum melakukan transaksi.

Saat aplikasi akan ditutup:

- **Ya**: export laporan penjualan periode aktif ke Excel, lalu aplikasi ditutup.
- **Tidak**: aplikasi ditutup tanpa export tambahan. Data transaksi yang sudah disimpan tetap aman.
- **Batal**: kembali ke aplikasi.

Laporan juga dapat disimpan manual dari menu **Laporan**. Fitur ini lebih tepat digunakan sebagai arsip atau pengiriman laporan, sedangkan database tetap menjadi sumber data utama.

## 5. Mencatat stok masuk

1. Buka menu **Obat**, lalu klik tombol **Stok Masuk**. Layar awal menampilkan pilihan **Tunggal** dan **Batch (Faktur)**.
2. Pilih **Tunggal** untuk menambah satu obat. Isi nama obat, satuan, jumlah, harga beli, harga jual, batch, dan tanggal kedaluwarsa bila ada.
3. Pilih **Batch (Faktur)** untuk memasukkan beberapa obat dari satu faktur fisik.
4. Isi nama supplier/faktur dan nomor faktur pada bagian header.
5. Tambahkan setiap obat sebagai satu baris, lalu isi jumlah, satuan, harga beli, diskon, PPN, batch, expired, dan harga jualnya.
6. Periksa seluruh baris dan klik **Simpan Stok Masuk**.

Setelah disimpan, obat yang belum ada otomatis dibuat di master obat, sedangkan obat yang sudah ada mendapatkan tambahan stok dan batch. Nomor faktur yang sama ditolak agar satu faktur tidak tercatat dua kali. Stok masuk yang sudah tersimpan menjadi histori dan tidak diedit langsung; gunakan **Koreksi Stok** atau buat pembetulan bila ada kesalahan.

## 6. Kartu stok

Buka menu **Kartu Stok**, pilih obat, batch, dan rentang tanggal. Kartu stok menampilkan:

- tanggal dan waktu pergerakan;
- nomor batch dan tanggal kedaluwarsa;
- jumlah masuk dan jumlah keluar;
- saldo total sebelum dan sesudah transaksi;
- saldo batch sebelum dan sesudah transaksi;
- kode transaksi, termasuk kode `T...` dan `Q...`;
- sumber pergerakan, seperti stok masuk, penjualan, atau koreksi;
- alasan koreksi jika ada.

Gunakan kartu stok untuk mencocokkan stok aplikasi dengan stok fisik. Selisih stok tidak boleh diselesaikan dengan mengubah angka master secara sembarangan; catat melalui **Koreksi Stok**.

## 7. Stok opname

Gunakan **Stok Opname** untuk pemeriksaan stok fisik berkala. Sistem membuat snapshot stok saat sesi dimulai, termasuk rincian batch dan expired date.

1. Pilih **Opname Baru** dan isi catatan bila diperlukan.
2. Pilih item pada tabel, lalu masukkan **Stok Fisik**.
3. Isi **Alasan selisih** jika jumlah fisik berbeda dari stok sistem.
4. Pilih **Simpan Hitungan** untuk setiap item.
5. Setelah semua item dihitung, Super Admin memilih **Selesaikan** untuk memposting selisih ke stok.
6. Export hasil opname jika diperlukan untuk arsip.

Tabel opname menampilkan stok sistem, stok fisik, selisih, harga beli, dan nilai selisih.

Selama sesi berstatus `DRAFT`, transaksi penjualan, stok masuk, pembatalan transaksi, dan koreksi stok ditahan agar snapshot tetap akurat. Jika opname tidak dilanjutkan, Super Admin dapat memilih **Batalkan** dengan alasan. Sesi yang selesai atau dibatalkan tidak dapat diubah.

Stok opname yang selesai otomatis membuat mutasi `STOK_OPNAME_MASUK` atau `STOK_OPNAME_KELUAR` di **Kartu Stok** dan tercatat di **Aktivitas**. Jika sistem mendeteksi stok berubah selama opname, penyelesaian ditolak agar selisih tidak salah; batalkan sesi dan mulai opname baru.

## 8. Koreksi stok

1. Lakukan pemeriksaan fisik terlebih dahulu.
2. Buka menu **Koreksi Stok**.
3. Pilih obat dan batch yang benar.
4. Masukkan jumlah penyesuaian atau jumlah fisik sesuai field yang tersedia.
5. Isi alasan, misalnya rusak, kedaluwarsa, salah input, atau hasil stock opname.
6. Simpan koreksi dan periksa kembali kartu stok.

> Koreksi stok mengubah saldo, tetapi tidak menghapus riwayat transaksi sebelumnya. Alasan wajib dibuat singkat, faktual, dan mudah diperiksa.

## 9. Laporan

Menu **Laporan** menyediakan laporan sesuai kebutuhan operasional, antara lain:

- penjualan Tunai;
- penjualan QRIS;
- gabungan penjualan;
- stok masuk supplier;
- stok dan batch;
- obat mendekati atau melewati kedaluwarsa;
- koreksi stok;
- diskon;
- kartu stok;
- ringkasan aktivitas.

Pilih tanggal mulai dan tanggal akhir, kemudian pilih **Tampilkan**. Gunakan **Export Excel** untuk menyimpan hasil sebagai arsip. Selalu periksa filter tanggal dan total sebelum laporan dikirim.

## 10. Aktivitas user

Menu **Aktivitas** mencatat aksi penting seperti login, penjualan, stok masuk, koreksi stok, perubahan data obat, backup, restore, dan logout.

- **Karyawan** melihat ringkasan aktivitas miliknya sendiri, termasuk obat, jumlah, nominal, metode pembayaran, dan catatan yang relevan.
- **Super Admin** dapat melihat aktivitas seluruh user, waktu aksi, detail transaksi, alasan, status berhasil/gagal, serta nilai sebelum dan sesudah perubahan.

Super Admin dapat memfilter tanggal, user, jenis aksi, dan aktivitas login gagal untuk mencari kesalahan input atau kejanggalan.

## 11. Backup, restore, dan export

Gunakan menu **Backup Data** sebelum migrasi database, pemeliharaan besar, atau perubahan penting. Simpan salinan backup di media yang aman dan berbeda dari komputer utama.

Backup lokal aplikasi biasanya berada di:

`%LOCALAPPDATA%\\FirdanFarma\\backup\\`

Restore hanya dilakukan oleh Super Admin setelah memastikan file backup benar. Setelah restore, tutup dan buka kembali aplikasi, lalu periksa jumlah obat, stok, dan transaksi.

Export Excel digunakan untuk laporan dan arsip. File export bukan pengganti backup database karena tidak menyimpan seluruh struktur dan histori aplikasi.

## 12. Pemecahan masalah singkat

| Masalah | Tindakan |
|---|---|
| Login gagal | Periksa username/password dan minta Super Admin melakukan reset bila perlu. |
| Penjualan ditolak | Periksa stok, batch, tanggal kedaluwarsa, harga, dan jumlah. |
| Stok fisik berbeda | Lakukan **Stok Opname** untuk pemeriksaan berkala atau **Koreksi Stok** untuk kejadian tunggal. |
| Transaksi stok ditahan | Periksa apakah ada **Stok Opname** berstatus DRAFT; selesaikan atau batalkan dengan Super Admin. |
| Laporan terlihat kosong | Periksa rentang tanggal dan periode pukul 15.00. |
| Struk tidak tercetak | Periksa printer default, driver, koneksi, dan ulangi cetak dari laporan bila tersedia. |
| Aplikasi tidak mau ditutup | Pilih **Ya** untuk export laporan, **Tidak** untuk tutup tanpa export, atau **Batal** untuk kembali. |

## 13. Checklist sebelum operasional

- Pastikan tanggal dan jam komputer benar.
- Pastikan user menggunakan akun masing-masing.
- Periksa master obat, harga jual, stok minimum, dan supplier.
- Masukkan batch serta tanggal kedaluwarsa saat menerima barang.
- Cocokkan total penjualan dan kas/QRIS sebelum pergantian shift.
- Simpan laporan periode penting dan lakukan backup berkala.
- Pastikan koreksi selalu memiliki alasan.
