from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Sequence

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / "dist"
EMP_DIR = DIST / "release_screens"
ADM_DIR = DIST / "manual_assets"

KARYAWAN_PDF = DIST / "Manual_Karyawan_FirdanFarma.pdf"
ADMIN_PDF = DIST / "Manual_SuperAdmin_FirdanFarma.pdf"

W, H = 1654, 2339
M = 88

BG = (245, 247, 250)
WHITE = (255, 255, 255)
INK = (15, 23, 42)
SUB = (71, 85, 105)
MUTED = (100, 116, 139)
LINE = (226, 232, 240)
ACCENT = (37, 99, 235)
ACCENT_SOFT = (239, 246, 255)
SUCCESS = (22, 163, 74)
AMBER = (180, 83, 9)
PURPLE = (124, 58, 237)
RED = (220, 38, 38)
SIDEBAR = (30, 41, 59)

FONT_REG = r"C:\Windows\Fonts\segoeui.ttf"
FONT_BOLD = r"C:\Windows\Fonts\segoeuib.ttf"
FONT_LIGHT = r"C:\Windows\Fonts\segoeuil.ttf"


def font(path: str, size: int):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()


def fonts():
    return {
        "reg": lambda size: font(FONT_REG, size),
        "bold": lambda size: font(FONT_BOLD, size),
        "light": lambda size: font(FONT_LIGHT, size),
    }


F = fonts()


@dataclass
class PageSpec:
    title: str
    subtitle: str
    screenshot: Path
    bullets: Sequence[str]
    badge: str
    color: tuple[int, int, int] = ACCENT


def new_page():
    img = Image.new("RGB", (W, H), BG)
    return img, ImageDraw.Draw(img)


def box(draw, xy, fill, outline=LINE, radius=22, width=1):
    draw.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)


def line(draw, xy, fill=LINE, width=1):
    draw.line(xy, fill=fill, width=width)


def text_size(draw, txt, ft):
    bb = draw.multiline_textbbox((0, 0), txt, font=ft, spacing=4)
    return bb[2] - bb[0], bb[3] - bb[1]


def wrap(draw, txt, ft, max_width):
    out: List[str] = []
    for raw in txt.split("\n"):
        if not raw:
            out.append("")
            continue
        words = raw.split(" ")
        cur = words[0]
        for word in words[1:]:
            trial = cur + " " + word
            if draw.textbbox((0, 0), trial, font=ft)[2] <= max_width:
                cur = trial
            else:
                out.append(cur)
                cur = word
        out.append(cur)
    return "\n".join(out)


def draw_wrapped(draw, x, y, txt, ft, fill, max_width, spacing=6):
    txt = wrap(draw, txt, ft, max_width)
    draw.multiline_text((x, y), txt, font=ft, fill=fill, spacing=spacing)
    _, h = text_size(draw, txt, ft)
    return y + h


def badge(draw, x, y, w, h, txt, fill, fg=WHITE):
    box(draw, (x, y, x + w, y + h), fill, outline=fill, radius=16)
    ft = F["bold"](15)
    tw, th = text_size(draw, txt, ft)
    draw.text((x + (w - tw) / 2, y + (h - th) / 2 - 1), txt, font=ft, fill=fg)


def header(draw, title, subtitle, role, page_no, role_color):
    draw.text((M, 50), title, font=F["bold"](42), fill=INK)
    draw.text((M, 104), subtitle, font=F["reg"](18), fill=SUB)
    badge(draw, W - M - 180, 52, 180, 40, role, role_color, WHITE)
    badge(draw, W - M - 120, 104, 120, 34, f"Hal {page_no}", ACCENT_SOFT, ACCENT)
    line(draw, (M, 166, W - M, 166), fill=LINE, width=2)


def image_panel(path: Path, w, h, caption=None):
    if not path.exists():
        raise FileNotFoundError(path)
    with Image.open(path) as src:
        fitted = ImageOps.contain(src.convert("RGB"), (w - 24, h - 24))
    panel = Image.new("RGB", (w, h), WHITE)
    pd = ImageDraw.Draw(panel)
    box(pd, (0, 0, w - 1, h - 1), WHITE, outline=LINE, radius=24)
    ox = (w - fitted.width) // 2
    oy = 14
    panel.paste(fitted, (ox, oy))
    if caption:
        pd.text((18, h - 44), caption, font=F["reg"](15), fill=MUTED)
    return panel


def bullets_panel(draw, x, y, w, bullets, accent=ACCENT):
    box(draw, (x, y, x + w, y + 430), WHITE, outline=LINE, radius=22)
    draw.text((x + 22, y + 18), "Yang perlu diperhatikan", font=F["bold"](22), fill=INK)
    cur = y + 66
    for i, b in enumerate(bullets, 1):
        badge(draw, x + 22, cur + 2, 34, 34, str(i), accent, WHITE)
        draw_wrapped(draw, x + 70, cur, b, F["reg"](18), SUB, w - 92, spacing=4)
        cur += 62


def summary_box(draw, x, y, w, title, lines, fill=ACCENT_SOFT, accent=ACCENT):
    box(draw, (x, y, x + w, y + 250), fill, outline=LINE, radius=22)
    draw.text((x + 22, y + 18), title, font=F["bold"](22), fill=INK)
    cur = y + 62
    for line_txt in lines:
        draw.text((x + 22, cur), "•", font=F["bold"](18), fill=accent)
        draw_wrapped(draw, x + 48, cur, line_txt, F["reg"](18), SUB, w - 70, spacing=4)
        cur += 40


def cover_page(role_name: str, role_color, subtitle: str, screenshot: Path, bullets: Sequence[str], footer: str):
    img, d = new_page()
    d.text((M, 50), f"Manual {role_name} Firdan Farma", font=F["bold"](44), fill=INK)
    d.text((M, 108), subtitle, font=F["reg"](18), fill=SUB)
    badge(d, W - M - 220, 52, 220, 40, "Offline + Windows 7 SP1", role_color, WHITE)
    badge(d, W - M - 128, 104, 128, 34, "SQLite lokal", ACCENT_SOFT, ACCENT)
    line(d, (M, 166, W - M, 166), fill=LINE, width=2)

    img.paste(image_panel(screenshot, 890, 1220, "Tampilan asli aplikasi sesuai role ini"), (M, 220))
    bullets_panel(d, 1006, 220, 560, bullets, accent=role_color)
    summary_box(
        d,
        M,
        1508,
        W - M * 2,
        "Cara pakai singkat",
        [
            "Buka aplikasi, login dengan akun role masing-masing, lalu pilih menu yang sesuai pekerjaan.",
            "Semua data tersimpan lokal, jadi aplikasi tetap jalan tanpa internet.",
            "Gunakan laporan untuk melihat hasil kerja, dan backup sebelum pekerjaan besar.",
        ],
        fill=WHITE,
        accent=role_color,
    )
    box(d, (M, 1800, W - M, 2062), WHITE, outline=LINE, radius=22)
    d.text((M + 22, 1822), "Alur yang dipakai di manual ini", font=F["bold"](22), fill=INK)
    draw_wrapped(
        d,
        M + 22,
        1864,
        footer,
        F["reg"](18),
        SUB,
        W - M * 2 - 44,
        spacing=5,
    )
    return img


def feature_page(page_no: int, role: str, role_color, title: str, subtitle: str, screenshot: Path, bullets: Sequence[str], footer_note: str):
    img, d = new_page()
    header(d, title, subtitle, role, page_no, role_color)
    img.paste(image_panel(screenshot, 1468, 1100), (M, 220))
    bullets_panel(d, M, 1360, 1468, bullets, accent=role_color)
    box(d, (M, 1830, W - M, 2100), WHITE, outline=LINE, radius=22)
    d.text((M + 22, 1852), "Catatan", font=F["bold"](22), fill=INK)
    draw_wrapped(d, M + 22, 1894, footer_note, F["reg"](18), SUB, W - M * 2 - 44, spacing=5)
    return img


def dual_feature_page(page_no: int, role: str, role_color, title: str, subtitle: str, left: Path, right: Path, left_cap: str, right_cap: str, bullets: Sequence[str], footer_note: str):
    img, d = new_page()
    header(d, title, subtitle, role, page_no, role_color)
    img.paste(image_panel(left, 700, 950, left_cap), (M, 220))
    img.paste(image_panel(right, 700, 950, right_cap), (866, 220))
    bullets_panel(d, M, 1210, 1468, bullets, accent=role_color)
    box(d, (M, 1830, W - M, 2100), WHITE, outline=LINE, radius=22)
    d.text((M + 22, 1852), "Catatan", font=F["bold"](22), fill=INK)
    draw_wrapped(d, M + 22, 1894, footer_note, F["reg"](18), SUB, W - M * 2 - 44, spacing=5)
    return img


def save_pdf(pages: Sequence[Image.Image], output: Path):
    output.parent.mkdir(parents=True, exist_ok=True)
    rgb_pages = [p.convert("RGB") for p in pages]
    rgb_pages[0].save(output, "PDF", save_all=True, append_images=rgb_pages[1:], resolution=200.0)


def build_karyawan() -> Path:
    pages: List[Image.Image] = []
    pages.append(
        cover_page(
            "Karyawan",
            ACCENT,
            "Panduan kerja harian untuk transaksi, cek stok, dan baca laporan.",
            EMP_DIR / "karyawan_dashboard_clean.png",
            [
                "Fokus utamanya cepat melayani pelanggan tanpa membuka menu yang tidak perlu.",
                "Bisa masuk ke Obat, Penjualan, Kartu Stok, Laporan, Aktivitas, dan Ganti Pengguna.",
                "Menu Stok Masuk tetap tersedia bila hak akses mengizinkan.",
            ],
            "Urutan bacanya: Beranda -> Obat -> Penjualan -> Kartu Stok -> Laporan -> Aktivitas.",
        )
    )
    pages.append(
        feature_page(
            2,
            "Karyawan",
            ACCENT,
            "Beranda",
            "Lihat ringkasan operasional sebelum mulai kerja.",
            EMP_DIR / "karyawan_dashboard_clean.png",
            [
                "Grand Total dan kartu ringkas membantu melihat kondisi harian dengan cepat.",
                "Pilih menu di sidebar sesuai tugas, lalu cek log aktivitas di bawah.",
                "Periode laporan memakai sekat jam 15:00 di header.",
            ],
            "Dashboard dipakai sebagai pintu awal. Dari sini user pindah ke Obat, Penjualan, Kartu Stok, Laporan, atau Aktivitas.",
        )
    )
    pages.append(
        dual_feature_page(
            3,
            "Karyawan",
            ACCENT,
            "Obat dan Penjualan",
            "Menu paling sering dipakai saat melayani pelanggan.",
            EMP_DIR / "karyawan_obat_clean.png",
            EMP_DIR / "karyawan_penjualan_clean.png",
            "Klik Obat",
            "Klik Penjualan",
            [
                "Di Obat, karyawan mencari nama barang, cek stok, lalu melihat status aktif atau menipis.",
                "Di Penjualan, karyawan mencari obat, isi qty, lalu checkout tunai atau QRIS.",
                "Jika stok tidak cukup, transaksi ditolak dulu supaya stok tetap aman.",
            ],
            "Gunakan Obat untuk cek ketersediaan. Gunakan Penjualan untuk memasukkan transaksi final.",
        )
    )
    pages.append(
        feature_page(
            4,
            "Karyawan",
            ACCENT,
            "Kartu Stok",
            "Riwayat masuk dan keluar per obat.",
            EMP_DIR / "karyawan_kartu_stok_clean.png",
            [
                "Kartu stok dipakai untuk mengecek mutasi barang, batch, dan referensi transaksi.",
                "Filter tanggal dan kode membantu mencari histori yang spesifik.",
                "Menu ini penting kalau ingin mengecek selisih atau asal perpindahan stok.",
            ],
            "Kartu stok adalah catatan kerja. Isi di sini harus cocok dengan transaksi penjualan, pembelian, dan koreksi yang sudah disimpan.",
        )
    )
    pages.append(
        feature_page(
            5,
            "Karyawan",
            ACCENT,
            "Laporan",
            "Download laporan manual dan lihat data kerja harian.",
            EMP_DIR / "karyawan_laporan_clean.png",
            [
                "Laporan dipakai untuk melihat penjualan, stok, dan ringkasan yang boleh diakses role karyawan.",
                "Gunakan Download Laporan untuk mengambil file sesuai tanggal atau sekat yang dipilih.",
                "Periksa tanggal dulu sebelum download supaya isi laporan tidak salah periode.",
            ],
            "Karyawan cukup fokus ke laporan operasional. Detail sensitif tetap disembunyikan sesuai hak akses.",
        )
    )
    pages.append(
        feature_page(
            6,
            "Karyawan",
            ACCENT,
            "Aktivitas dan Ganti Pengguna",
            "Cek jejak kerja lalu pindah akun tanpa menutup aplikasi.",
            EMP_DIR / "karyawan_aktivitas_clean.png",
            [
                "Aktivitas menampilkan login, logout, penjualan, stok masuk, dan aksi lain yang dilakukan akun ini.",
                "Jika harus berganti akun, gunakan Ganti Pengguna agar sesi lama ditutup rapi.",
                "Ini membantu kalau satu PC dipakai lebih dari satu user dalam satu shift.",
            ],
            "Hal penting untuk karyawan: kerja cepat, simpan transaksi, cek hasil di laporan, lalu logout atau ganti pengguna dengan benar.",
        )
    )

    out = DIST / "Manual_Karyawan_FirdanFarma.pdf"
    save_pdf(pages, out)
    return out


def build_admin() -> Path:
    pages: List[Image.Image] = []
    pages.append(
        cover_page(
            "Super Admin",
            SUCCESS,
            "Panduan penuh untuk kepala apotek: stok masuk, stok opname, pengguna, master data, backup, dan audit.",
            ADM_DIR / "admin_dashboard.png",
            [
                "Semua menu karyawan tetap tersedia, ditambah kendali operasional dan pengawasan.",
                "Stok Masuk dibuka dari tab Obat, lalu pilih mode Tunggal atau Batch (Faktur).",
                "Super Admin juga memegang Stok Opname, Koreksi Stok, Pengguna, Master Data, dan Backup.",
            ],
            "Urutan bacanya: Dashboard -> Obat/Stok Masuk -> Stok Opname -> Koreksi -> Pengguna -> Master Data -> Backup -> Laporan.",
        )
    )
    pages.append(
        feature_page(
            2,
            "Super Admin",
            SUCCESS,
            "Dashboard Admin",
            "Lihat grand total dan pengawasan operasional.",
            ADM_DIR / "admin_dashboard.png",
            [
                "Kartu grand total menampilkan laba, stok, pembelian, dan terjual.",
                "Menu admin lengkap muncul di sidebar: Pengguna, Master Data, Backup, dan lainnya.",
                "Header memperlihatkan jam live dan sekat laporan 15:00.",
            ],
            "Dashboard admin dipakai sebagai pusat kontrol. Dari sini kamu bisa cek kondisi bisnis sebelum masuk ke menu detail.",
        )
    )
    pages.append(
        dual_feature_page(
            3,
            "Super Admin",
            SUCCESS,
            "Obat dan Stok Masuk",
            "Stok masuk dibuka dari menu Obat, bukan dari menu terpisah.",
            ADM_DIR / "admin_obat.png",
            ADM_DIR / "admin_stok_masuk_mode.png",
            "Klik Obat",
            "Klik Stok Masuk",
            [
                "Di tab Obat, tombol Stok Masuk membuka input stok baru.",
                "Mode Tunggal dipakai untuk satu obat, sedangkan Batch dipakai untuk faktur yang berisi banyak item.",
                "Di mode batch, user isi supplier, no faktur, lalu obat per baris sebelum simpan.",
            ],
            "Stok masuk menjadi pintu utama saat barang datang. Setelah disimpan, stok dan kartu stok ikut berubah.",
        )
    )
    pages.append(
        dual_feature_page(
            4,
            "Super Admin",
            SUCCESS,
            "Stok Masuk Tunggal dan Batch",
            "Dua cara input yang dipakai sesuai kondisi barang datang.",
            ADM_DIR / "admin_stok_masuk_tunggal.png",
            ADM_DIR / "admin_stok_masuk_batch.png",
            "Mode Tunggal",
            "Mode Batch",
            [
                "Tunggal: isi nama obat, qty, harga beli, harga jual, batch, dan expired bila perlu.",
                "Batch: isi header faktur dulu, lalu tambahkan setiap obat satu per satu dari faktur fisik.",
                "Harga jual bisa disesuaikan per item sebelum simpan final.",
            ],
            "Pilih mode sesuai faktur asli. Jangan isi data manual yang tidak ada di dokumen supplier.",
        )
    )
    pages.append(
        dual_feature_page(
            5,
            "Super Admin",
            SUCCESS,
            "Stok Opname dan Koreksi Stok",
            "Gunakan untuk hitung fisik dan perbaiki selisih secara resmi.",
            ADM_DIR / "admin_stok_opname.png",
            ADM_DIR / "admin_koreksi_stok.png",
            "Stok Opname",
            "Koreksi Stok",
            [
                "Stok opname dipakai saat ingin menghitung fisik lalu memposting selisih yang sah.",
                "Koreksi stok dipakai untuk penyesuaian dengan alasan yang jelas.",
                "Sebagai Super Admin, semua tindakan harus tetap masuk audit.",
            ],
            "Kalau ingin data benar-benar cocok dengan stok nyata, stok opname adalah titik awal yang paling aman.",
        )
    )
    pages.append(
        dual_feature_page(
            6,
            "Super Admin",
            SUCCESS,
            "Pengguna, Master Data, dan Backup",
            "Bagian administratif yang menjaga sistem tetap rapi.",
            ADM_DIR / "admin_pengguna.png",
            ADM_DIR / "admin_backup.png",
            "Pengguna",
            "Backup",
            [
                "Pengguna dipakai untuk menambah akun, mengubah role, dan mengatur izin stok masuk atau koreksi.",
                "Master Data dipakai untuk kategori dan supplier agar input obat konsisten.",
                "Backup wajib dilakukan sebelum operasi besar atau restore.",
            ],
            "Administrator memegang kunci utama: akun, data master, dan salinan database.",
        )
    )
    pages.append(
        feature_page(
            7,
            "Super Admin",
            SUCCESS,
            "Laporan dan Aktivitas",
            "Baca hasil kerja, lalu audit jejak operasional.",
            ADM_DIR / "admin_laporan.png",
            [
                "Laporan admin memuat tab stok, tunai, QRIS, gabungan, stok masuk, expired, batch, koreksi, diskon, dan audit.",
                "Gunakan Download Laporan untuk mengambil data sesuai tanggal atau sekat yang dipilih.",
                "Aktivitas dipakai untuk melihat jejak login, logout, penjualan, dan perubahan data.",
            ],
            "Kalau ada masalah di lapangan, laporan dan aktivitas adalah tempat pertama untuk menelusuri penyebabnya.",
        )
    )

    out = DIST / "Manual_SuperAdmin_FirdanFarma.pdf"
    save_pdf(pages, out)
    return out


def main():
    missing = []
    required = [
        EMP_DIR / "karyawan_dashboard_clean.png",
        EMP_DIR / "karyawan_obat_clean.png",
        EMP_DIR / "karyawan_penjualan_clean.png",
        EMP_DIR / "karyawan_kartu_stok_clean.png",
        EMP_DIR / "karyawan_laporan_clean.png",
        EMP_DIR / "karyawan_aktivitas_clean.png",
        ADM_DIR / "admin_dashboard.png",
        ADM_DIR / "admin_obat.png",
        ADM_DIR / "admin_stok_masuk_mode.png",
        ADM_DIR / "admin_stok_masuk_tunggal.png",
        ADM_DIR / "admin_stok_masuk_batch.png",
        ADM_DIR / "admin_stok_opname.png",
        ADM_DIR / "admin_koreksi_stok.png",
        ADM_DIR / "admin_pengguna.png",
        ADM_DIR / "admin_backup.png",
        ADM_DIR / "admin_laporan.png",
    ]
    for p in required:
        if not p.exists():
            missing.append(str(p))
    if missing:
        raise FileNotFoundError("Missing screenshots:\n" + "\n".join(missing))

    out1 = build_karyawan()
    out2 = build_admin()
    print(out1)
    print(out2)


if __name__ == "__main__":
    main()
