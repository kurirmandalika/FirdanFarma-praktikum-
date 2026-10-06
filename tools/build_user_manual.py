from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Sequence, Tuple

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / "dist"
PAGE_DIR = DIST / "user_manual_pages"
PDF_PATH = DIST / "Panduan_Pengguna_Firdan_Farma.pdf"

W, H = 1654, 2339
M = 92

BG = (241, 245, 249)
WHITE = (255, 255, 255)
INK = (15, 23, 42)
SUB = (71, 85, 105)
MUTED = (100, 116, 139)
LINE = (226, 232, 240)
ACCENT = (37, 99, 235)
ACCENT_SOFT = (239, 246, 255)
SIDEBAR = (30, 41, 59)
SUCCESS = (22, 163, 74)
DANGER = (220, 38, 38)
AMBER = (180, 83, 9)
PURPLE = (124, 58, 237)

FONT_REG = r"C:\Windows\Fonts\segoeui.ttf"
FONT_BOLD = r"C:\Windows\Fonts\segoeuib.ttf"
FONT_LIGHT = r"C:\Windows\Fonts\segoeuil.ttf"
FONT_ITALIC = r"C:\Windows\Fonts\segoeuii.ttf"
FONT_MONO = r"C:\Windows\Fonts\consola.ttf"


def f(path: str, size: int):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()


def fonts():
    return {
        "reg": lambda size: f(FONT_REG, size),
        "bold": lambda size: f(FONT_BOLD, size),
        "light": lambda size: f(FONT_LIGHT, size),
        "ital": lambda size: f(FONT_ITALIC, size),
        "mono": lambda size: f(FONT_MONO, size),
    }


F = fonts()


def new_page() -> Tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGB", (W, H), BG)
    return img, ImageDraw.Draw(img)


def box(draw, xy, fill, outline=LINE, radius=24, width=1):
    draw.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)


def line(draw, xy, fill=LINE, width=1):
    draw.line(xy, fill=fill, width=width)


def text_size(draw, txt, font):
    bb = draw.multiline_textbbox((0, 0), txt, font=font, spacing=4)
    return bb[2] - bb[0], bb[3] - bb[1]


def wrap(draw, txt, font, max_width):
    out = []
    for raw in txt.split("\n"):
        if not raw:
            out.append("")
            continue
        words = raw.split(" ")
        cur = words[0]
        for word in words[1:]:
            trial = cur + " " + word
            if draw.textbbox((0, 0), trial, font=font)[2] <= max_width:
                cur = trial
            else:
                out.append(cur)
                cur = word
        out.append(cur)
    return "\n".join(out)


def draw_wrapped(draw, x, y, txt, font, fill, max_width, spacing=6):
    txt = wrap(draw, txt, font, max_width)
    draw.multiline_text((x, y), txt, font=font, fill=fill, spacing=spacing)
    _, h = text_size(draw, txt, font)
    return y + h


def title_block(draw, title, subtitle=None, page_no=None):
    draw.text((M, 54), title, font=F["bold"](42), fill=INK)
    if subtitle:
        draw.text((M, 110), subtitle, font=F["reg"](18), fill=SUB)
    if page_no is not None:
        badge(draw, W - M - 130, 56, 130, 42, f"Hal {page_no}", ACCENT_SOFT, ACCENT)
    line(draw, (M, 166, W - M, 166), fill=LINE, width=2)


def badge(draw, x, y, w, h, txt, fill, fg=WHITE, border=None):
    box(draw, (x, y, x + w, y + h), fill, outline=border or fill, radius=18)
    font = F["bold"](15)
    tw, th = text_size(draw, txt, font)
    draw.text((x + (w - tw) / 2, y + (h - th) / 2 - 2), txt, font=font, fill=fg)


def step_card(draw, x, y, w, h, num, title, body, fill=WHITE, accent=ACCENT):
    box(draw, (x, y, x + w, y + h), fill, outline=LINE, radius=22)
    badge(draw, x + 18, y + 18, 46, 46, str(num), accent, WHITE)
    draw.text((x + 78, y + 19), title, font=F["bold"](23), fill=INK)
    draw_wrapped(draw, x + 18, y + 74, body, F["reg"](18), SUB, w - 36, spacing=5)


def list_box(draw, x, y, w, title, items, accent=ACCENT, subtitle=None):
    box(draw, (x, y, x + w, y + 520), WHITE, outline=LINE, radius=24)
    draw.text((x + 22, y + 18), title, font=F["bold"](24), fill=INK)
    if subtitle:
        draw.text((x + 22, y + 55), subtitle, font=F["reg"](16), fill=MUTED)
        top = y + 90
    else:
        top = y + 60
    cur = top
    for idx, item in enumerate(items, 1):
        box(draw, (x + 22, cur, x + w - 22, cur + 64), ACCENT_SOFT if idx % 2 else (248, 250, 252), outline=(229, 231, 235), radius=16)
        badge(draw, x + 34, cur + 12, 40, 40, str(idx), accent, WHITE)
        draw.text((x + 90, cur + 19), item, font=F["reg"](19), fill=INK)
        cur += 74


def draw_sidebar(draw, x, y, w, h, items, active_index=0, compact=False):
    box(draw, (x, y, x + w, y + h), SIDEBAR, outline=SIDEBAR, radius=18)
    draw.text((x + 22, y + 20), "FIRDAN", font=F["bold"](23), fill=WHITE)
    draw.text((x + 22, y + 48), "FARMA", font=F["bold"](23), fill=WHITE)
    draw.text((x + 22, y + 82), "Offline app", font=F["reg"](15), fill=(148, 163, 184))
    yy = y + 132
    for i, item in enumerate(items):
        fh = 38 if compact else 42
        fill = (71, 85, 105) if i == active_index else SIDEBAR
        badge_fill = ACCENT if i == active_index else (51, 65, 85)
        box(draw, (x + 10, yy, x + w - 10, yy + fh), fill, outline=fill, radius=14)
        draw.text((x + 24, yy + 10), item, font=F["reg"](16), fill=WHITE)
        yy += fh + 8


def draw_login_mock(draw, x, y, w, h):
    box(draw, (x, y, x + w, y + h), WHITE, outline=LINE, radius=24)
    line(draw, (x, y + 54, x + w, y + 54), fill=LINE, width=1)
    draw.text((x + 22, y + 18), "Firdan Farma - Login", font=F["bold"](18), fill=INK)
    draw.text((x + 22, y + 82), "Username", font=F["reg"](15), fill=SUB)
    box(draw, (x + 22, y + 110, x + w - 22, y + 154), (250, 250, 251), outline=LINE, radius=12)
    draw.text((x + 22, y + 180), "Password", font=F["reg"](15), fill=SUB)
    box(draw, (x + 22, y + 208, x + w - 22, y + 252), (250, 250, 251), outline=LINE, radius=12)
    badge(draw, x + 22, y + 286, w - 44, 50, "MASUK", ACCENT, WHITE)
    draw.text((x + 22, y + h - 46), "Aplikasi langsung masuk ke menu utama setelah login.", font=F["reg"](14), fill=MUTED)


def draw_dashboard_mock(draw, x, y, w, h, role="Karyawan"):
    box(draw, (x, y, x + w, y + h), WHITE, outline=LINE, radius=24)
    sidebar_w = 165 if role == "Karyawan" else 185
    box(draw, (x, y, x + sidebar_w, y + h), SIDEBAR, outline=SIDEBAR, radius=24)
    draw.text((x + 18, y + 18), "FIRDAN", font=F["bold"](20), fill=WHITE)
    draw.text((x + 18, y + 43), "FARMA", font=F["bold"](20), fill=WHITE)
    menus = ["Beranda", "Obat", "Penjualan", "Pembelian", "Kartu Stok", "Laporan", "Aktivitas", "Ganti Pengguna"]
    if role == "Super Admin":
        menus += ["Stok Opname", "Koreksi Stok", "Pengguna", "Master Data", "Backup"]
    my = y + 92
    for i, menu in enumerate(menus):
        active = i == 0
        box(draw, (x + 10, my, x + sidebar_w - 10, my + 34), (71, 85, 105) if active else SIDEBAR, outline=SIDEBAR, radius=10)
        draw.text((x + 20, my + 9), menu, font=F["reg"](13), fill=WHITE)
        my += 38
    draw.text((x + sidebar_w + 26, y + 20), "Beranda", font=F["bold"](24), fill=INK)
    draw.text((x + sidebar_w + 26, y + 52), "Periode: 26 Aug 2026 | Cut-off 15:00", font=F["reg"](14), fill=MUTED)
    cx = x + sidebar_w + 26
    cy = y + 95
    cw = w - sidebar_w - 50
    cards = [
        ("Penjualan", "Rp 1.250.000", "Tunai + QRIS"),
        ("Stok", "18 menipis", "Perlu cek batch"),
        ("Aktivitas", "Terbaru", "Lihat log"),
        ("Laporan", "Siap", "Export Excel"),
    ]
    for i, (t, v, s) in enumerate(cards):
        rx = cx + (i % 2) * (cw / 2 + 12)
        ry = cy + (i // 2) * 108
        ww = cw / 2 - 12
        box(draw, (rx, ry, rx + ww, ry + 92), ACCENT_SOFT if i % 2 == 0 else (248, 250, 252), outline=LINE, radius=18)
        draw.text((rx + 16, ry + 14), t, font=F["bold"](18), fill=SUB)
        draw.text((rx + 16, ry + 40), v, font=F["bold"](28), fill=INK)
        draw.text((rx + 16, ry + 68), s, font=F["reg"](14), fill=MUTED)


def draw_sales_mock(draw, x, y, w, h):
    box(draw, (x, y, x + w, y + h), WHITE, outline=LINE, radius=24)
    left_w = int(w * 0.68)
    box(draw, (x, y, x + left_w, y + h), WHITE, outline=LINE, radius=24)
    box(draw, (x + 16, y + 16, x + left_w - 16, y + 76), WHITE, outline=LINE, radius=16)
    draw.text((x + 30, y + 29), "Cari Obat (F1)", font=F["reg"](18), fill=SUB)
    box(draw, (x + 16, y + 92, x + left_w - 16, y + 250), (248, 250, 252), outline=LINE, radius=16)
    draw.text((x + 30, y + 104), "Daftar Obat", font=F["bold"](18), fill=INK)
    items = ["PARACETAMOL", "SANMOL TB", "PROMAG TAB", "CTM"]
    for i, item in enumerate(items):
        yy = y + 138 + i * 28
        draw.text((x + 30, yy), item, font=F["reg"](16), fill=SUB)
        draw.text((x + left_w - 86, yy), "Stok", font=F["reg"](16), fill=MUTED)
    box(draw, (x + 16, y + 266, x + left_w - 16, y + h - 16), WHITE, outline=LINE, radius=16)
    draw.text((x + 30, y + 278), "Keranjang", font=F["bold"](18), fill=INK)
    draw.text((x + 30, y + 315), "2 x PARACETAMOL", font=F["reg"](16), fill=SUB)
    draw.text((x + 30, y + 343), "Qty x Harga -> Subtotal", font=F["reg"](15), fill=MUTED)
    draw.text((x + 30, y + h - 56), "Total Belanja: Rp 6.000", font=F["bold"](22), fill=ACCENT)
    right_x = x + left_w + 18
    rw = w - left_w - 34
    box(draw, (right_x, y + 16, right_x + rw, y + h - 16), (248, 250, 252), outline=LINE, radius=16)
    draw.text((right_x + 18, y + 30), "Checkout", font=F["bold"](20), fill=INK)
    rows = [
        ("Diskon", "Rp 0"),
        ("Pembayaran", "TUNAI"),
        ("Uang Bayar", "Rp 10.000"),
        ("Kembali", "Rp 4.000"),
    ]
    yy = y + 78
    for t, v in rows:
        draw.text((right_x + 18, yy), t, font=F["reg"](16), fill=SUB)
        draw.text((right_x + 18, yy + 24), v, font=F["bold"](18), fill=INK)
        yy += 82
    badge(draw, right_x + 18, y + h - 74, rw - 36, 48, "BAYAR / CHECKOUT (F5)", SUCCESS, WHITE)


def draw_purchase_mock(draw, x, y, w, h):
    box(draw, (x, y, x + w, y + h), WHITE, outline=LINE, radius=24)
    draw.text((x + 20, y + 18), "Pembelian Supplier", font=F["bold"](24), fill=INK)
    draw.text((x + 20, y + 54), "Isi supplier, faktur, batch, expired, lalu simpan.", font=F["reg"](16), fill=MUTED)
    box(draw, (x + 20, y + 92, x + w - 20, y + 160), (248, 250, 252), outline=LINE, radius=16)
    draw.text((x + 36, y + 110), "Supplier: PT Sehat Sentosa", font=F["reg"](18), fill=SUB)
    draw.text((x + 36, y + 136), "Faktur: INV-SIM-001", font=F["reg"](18), fill=SUB)
    items = [
        ("PARACETAMOL", "Batch SIM-PCT-2026A", "Expired 2027-03-31"),
        ("PARACETAMOL", "Batch SIM-PCT-2026B", "Expired 2028-01-31"),
        ("CTM", "Batch SIM-CTM-2026A", "Expired 2027-02-28"),
    ]
    yy = y + 182
    for title, batch, exp in items:
        box(draw, (x + 20, yy, x + w - 20, yy + 106), ACCENT_SOFT, outline=LINE, radius=16)
        draw.text((x + 36, yy + 16), title, font=F["bold"](18), fill=INK)
        draw.text((x + 36, yy + 44), batch, font=F["reg"](16), fill=SUB)
        draw.text((x + 36, yy + 70), exp, font=F["reg"](16), fill=AMBER)
        yy += 122
    badge(draw, x + 20, y + h - 82, w - 40, 50, "SIMPAN FAKTUR", PURPLE, WHITE)


def draw_admin_mock(draw, x, y, w, h):
    box(draw, (x, y, x + w, y + h), WHITE, outline=LINE, radius=24)
    draw_sidebar(draw, x + 0, y + 0, 250, h, ["Beranda", "Obat", "Penjualan", "Pembelian", "Kartu Stok", "Stok Opname", "Koreksi Stok", "Laporan", "Aktivitas", "Pengguna", "Master Data", "Backup", "Ganti Pengguna"], 0, compact=True)
    dx = x + 274
    draw.text((dx, y + 18), "Super Admin", font=F["bold"](24), fill=INK)
    draw.text((dx, y + 54), "Bisa melihat semua menu dan menjaga data.", font=F["reg"](16), fill=MUTED)
    boxes = [
        ("Pengguna", "Tambah / ubah akun"),
        ("Master Data", "Kategori dan supplier"),
        ("Stok Opname", "Hitung fisik"),
        ("Backup", "Salin database"),
    ]
    for i, (t, s) in enumerate(boxes):
        xx = dx + (i % 2) * 210
        yy = y + 104 + (i // 2) * 120
        box(draw, (xx, yy, xx + 186, yy + 92), ACCENT_SOFT if i % 2 == 0 else (248, 250, 252), outline=LINE, radius=18)
        draw.text((xx + 16, yy + 14), t, font=F["bold"](18), fill=INK)
        draw_wrapped(draw, xx + 16, yy + 42, s, F["reg"](15), SUB, 150, spacing=4)
    box(draw, (dx, y + 368, x + w - 20, y + h - 20), WHITE, outline=LINE, radius=18)
    draw.text((dx + 18, y + 386), "Contoh kerja admin", font=F["bold"](19), fill=INK)
    notes = [
        "1. Cek dashboard.",
        "2. Input pembelian atau koreksi bila perlu.",
        "3. Buka laporan dan audit.",
        "4. Backup sebelum pekerjaan besar.",
    ]
    for i, n in enumerate(notes):
        draw.text((dx + 18, y + 424 + i * 28), n, font=F["reg"](16), fill=SUB)


def page_cover():
    img, d = new_page()
    d.text((M, 58), "Panduan Pengguna Firdan Farma", font=F["bold"](46), fill=INK)
    d.text((M, 118), "Untuk Karyawan dan Super Admin", font=F["reg"](20), fill=SUB)
    badge(d, W - M - 182, 58, 182, 42, "Offline + Win7 SP1", ACCENT_SOFT, ACCENT)
    badge(d, W - M - 114, 106, 114, 38, "SQLite lokal", (243, 244, 246), SUB)
    line(d, (M, 170, W - M, 170), fill=LINE, width=2)

    d.text((M, 212), "Begitu aplikasi dibuka, alurnya sederhana:", font=F["bold"](24), fill=INK)
    steps = [
        ("1", "Buka aplikasi", "Jalankan aplikasi desktop di Windows."),
        ("2", "Login", "Masukkan username dan password akun masing-masing."),
        ("3", "Pilih menu", "Gunakan menu sesuai peran: karyawan atau super admin."),
        ("4", "Simpan kerja", "Penjualan, pembelian, laporan, dan backup tersimpan lokal."),
    ]
    sx = M
    sy = 260
    for i, (n, t, b) in enumerate(steps):
        step_card(d, sx, sy + i * 128, 640, 112, n, t, b, fill=WHITE, accent=ACCENT if i < 2 else SUCCESS)

    draw_login_mock(d, 770, 245, 360, 400)
    draw_dashboard_mock(d, 1148, 245, 416, 400, role="Karyawan")

    box(d, (M, 806, W - M, 1028), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 828), "Kenapa manual ini dibuat begini?", font=F["bold"](24), fill=INK)
    bullets = [
        "Menu karyawan dibuat singkat supaya tidak membingungkan.",
        "Super Admin tetap punya semua fitur pengelolaan dan audit.",
        "Contoh penggunaan memakai bahasa kerja harian, bukan istilah teknis.",
        "Semua data disimpan lokal, jadi aplikasi tetap jalan tanpa internet.",
    ]
    yy = 870
    for b in bullets:
        d.text((M + 24, yy), "-", font=F["bold"](22), fill=ACCENT)
        draw_wrapped(d, M + 54, yy, b, F["reg"](18), SUB, W - M * 2 - 80, spacing=4)
        yy += 44

    box(d, (M, 1080, W - M, 1312), ACCENT_SOFT, outline=LINE, radius=24)
    d.text((M + 24, 1102), "Hal yang perlu diingat", font=F["bold"](24), fill=INK)
    draw_wrapped(d, M + 24, 1144, "Pastikan tanggal dan jam komputer benar, karena periode laporan memakai cut-off pukul 15:00.", F["reg"](19), SUB, W - M * 2 - 48, spacing=5)
    draw_wrapped(d, M + 24, 1206, "Gunakan menu Ganti Pengguna kalau mau pindah dari karyawan ke super admin tanpa menutup aplikasi manual.", F["reg"](19), SUB, W - M * 2 - 48, spacing=5)

    d.text((M, 1470), "Panduan ini dibagi menjadi dua jalur:", font=F["bold"](24), fill=INK)
    box(d, (M, 1510, W / 2 - 18, 1784), WHITE, outline=LINE, radius=24)
    box(d, (W / 2 + 18, 1510, W - M, 1784), WHITE, outline=LINE, radius=24)
    d.text((M + 22, 1530), "Karyawan", font=F["bold"](24), fill=INK)
    d.text((W / 2 + 40, 1530), "Super Admin", font=F["bold"](24), fill=INK)
    left = ["Beranda", "Obat", "Penjualan", "Kartu Stok", "Laporan", "Aktivitas", "Ganti Pengguna"]
    right = ["Semua menu karyawan", "Pembelian", "Stok Opname", "Koreksi Stok", "Pengguna", "Master Data", "Backup", "Audit"]
    for i, item in enumerate(left):
        d.text((M + 22, 1580 + i * 26), "- " + item, font=F["reg"](18), fill=SUB)
    for i, item in enumerate(right):
        d.text((W / 2 + 40, 1580 + i * 26), "- " + item, font=F["reg"](18), fill=SUB)

    badge(d, M, 1860, 420, 52, "Fokus: cepat dipahami saat pertama buka", ACCENT, WHITE)
    badge(d, M + 438, 1860, 400, 52, "Bahasa sederhana + contoh nyata", SUCCESS, WHITE)
    badge(d, M + 866, 1860, 300, 52, "Siap dipakai offline", PURPLE, WHITE)
    return img


def page_role_comparison():
    img, d = new_page()
    title_block(d, "Siapa pakai menu apa?", "Perbedaan akses dibuat singkat supaya client langsung paham.", 2)

    list_box(d, M, 214, 700, "Karyawan", [
        "Beranda untuk lihat ringkasan harian.",
        "Obat untuk cari dan cek stok.",
        "Penjualan untuk input transaksi.",
        "Kartu Stok untuk cek mutasi barang.",
        "Laporan untuk lihat hasil kerja.",
        "Aktivitas untuk riwayat akun sendiri.",
        "Ganti Pengguna bila mau pindah akun.",
    ], accent=ACCENT, subtitle="Cukup untuk kerja harian.")

    list_box(d, 862, 214, 700, "Super Admin", [
        "Semua menu karyawan tetap bisa dibuka.",
        "Pembelian untuk stok masuk dari supplier.",
        "Stok Opname untuk hitung fisik.",
        "Koreksi Stok untuk selisih atau perbaikan.",
        "Pengguna untuk akun dan hak akses.",
        "Master Data untuk kategori dan supplier.",
        "Backup untuk salin database.",
    ], accent=SUCCESS, subtitle="Punya kendali penuh atas data.")

    box(d, (M, 770, W - M, 1080), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 792), "Gambaran visual", font=F["bold"](24), fill=INK)
    draw_sidebar(d, M + 24, 840, 500, 200, ["Beranda", "Obat", "Penjualan", "Kartu Stok", "Laporan", "Aktivitas"], 0, compact=True)
    draw_sidebar(d, 620, 840, 500, 200, ["Beranda", "Obat", "Penjualan", "Pembelian", "Kartu Stok", "Stok Opname", "Koreksi Stok", "Laporan", "Aktivitas", "Pengguna", "Master Data", "Backup"], 0, compact=True)
    draw_wrapped(d, 1150, 848, "Karyawan hanya melihat menu yang perlu. Super Admin melihat semua menu agar bisa menjaga data, stok, dan audit.", F["reg"](18), SUB, 420, spacing=5)

    box(d, (M, 1120, W - M, 1580), ACCENT_SOFT, outline=LINE, radius=24)
    d.text((M + 24, 1142), "Urutan kerja yang paling aman", font=F["bold"](24), fill=INK)
    steps = [
        ("1", "Login", "Masuk dengan akun sendiri."),
        ("2", "Buka menu", "Pilih menu sesuai tugas."),
        ("3", "Simpan", "Isi data, lalu simpan."),
        ("4", "Cek hasil", "Lihat kartu stok, laporan, atau audit."),
    ]
    for i, (n, t, b) in enumerate(steps):
        step_card(d, M + 24 + i * 370, 1194, 330, 290, n, t, b, fill=WHITE, accent=[ACCENT, SUCCESS, PURPLE, AMBER][i])

    return img


def page_employee_flow():
    img, d = new_page()
    title_block(d, "Alur kerja karyawan", "Fokusnya cepat: cari barang, jual, simpan, selesai.", 3)

    step_card(d, M, 218, 480, 126, 1, "Login", "Gunakan username dan password akun karyawan.")
    step_card(d, 596, 218, 480, 126, 2, "Cari obat", "Ketik nama obat, pilih item, lalu tentukan qty.")
    step_card(d, 1192, 218, 370, 126, 3, "Checkout", "Pilih tunai atau QRIS, isi diskon bila ada.")

    draw_sales_mock(d, M, 390, 1030, 1200)
    box(d, (1128, 390, W - M, 1590), WHITE, outline=LINE, radius=24)
    d.text((1150, 412), "Contoh cepat", font=F["bold"](24), fill=INK)
    flow = [
        ("Cari", "PARACETAMOL"),
        ("Qty", "2"),
        ("Bayar", "TUNAI"),
        ("Hasil", "Kode T1, total Rp 6.000"),
    ]
    yy = 468
    colors = [ACCENT, SUCCESS, PURPLE, AMBER]
    for i, (t, v) in enumerate(flow):
        box(d, (1150, yy, W - M - 22, yy + 172), ACCENT_SOFT if i % 2 == 0 else (248, 250, 252), outline=LINE, radius=18)
        badge(d, 1166, yy + 18, 60, 44, str(i + 1), colors[i], WHITE)
        d.text((1240, yy + 18), t, font=F["bold"](19), fill=INK)
        draw_wrapped(d, 1240, yy + 56, v, F["reg"](17), SUB, 320, spacing=4)
        yy += 192

    box(d, (M, 1620, W - M, 2100), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 1642), "Contoh penggunaan", font=F["bold"](24), fill=INK)
    left_txt = [
        "1. Buka menu Penjualan.",
        "2. Cari PARACETAMOL.",
        "3. Tambahkan qty 2 ke keranjang.",
        "4. Pilih TUNAI atau QRIS.",
        "5. Isi diskon kalau ada.",
        "6. Klik Simpan Transaksi.",
        "7. Cetak struk bila printer tersedia.",
    ]
    for i, t in enumerate(left_txt):
        d.text((M + 24, 1692 + i * 42), t, font=F["reg"](18), fill=SUB)
    box(d, (842, 1670, W - M - 24, 2020), ACCENT_SOFT, outline=LINE, radius=20)
    d.text((864, 1690), "Hasil yang diharapkan", font=F["bold"](20), fill=INK)
    outcome = [
        "Stok berkurang otomatis.",
        "Kartu stok mencatat mutasi keluar.",
        "Laporan tunai atau QRIS bertambah.",
        "Audit menyimpan waktu, user, dan nominal.",
    ]
    for i, t in enumerate(outcome):
        d.text((864, 1740 + i * 50), "• " + t, font=F["reg"](17), fill=SUB)
    badge(d, 864, 1928, 360, 48, "Diskon boleh 0 jika tidak ada", SUCCESS, WHITE)
    badge(d, 1234, 1928, 320, 48, "Kode transaksi T1 / Q1", PURPLE, WHITE)

    return img


def page_admin_flow():
    img, d = new_page()
    title_block(d, "Alur kerja Super Admin", "Semua data dan kontrol penting ada di sini.", 4)

    step_card(d, M, 218, 370, 126, 1, "Login", "Masuk sebagai Super Admin.")
    step_card(d, 474, 218, 370, 126, 2, "Master Data", "Atur kategori, supplier, dan pengguna.")
    step_card(d, 856, 218, 370, 126, 3, "Pembelian", "Catat stok masuk, batch, dan expired.")
    step_card(d, 1238, 218, 324, 126, 4, "Backup", "Salin database sebelum pekerjaan besar.")

    draw_admin_mock(d, M, 390, W - M * 2, 1050)
    box(d, (M, 1480, W - M, 2100), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 1502), "Ringkasnya Super Admin mengerjakan ini:", font=F["bold"](24), fill=INK)
    bullets = [
        "Membuka semua menu yang dibutuhkan operasional.",
        "Menambah atau mengubah user, kategori, dan supplier.",
        "Mengelola pembelian, stok opname, koreksi stok, dan audit.",
        "Mengecek laporan, aktivitas, dan melakukan backup data.",
    ]
    y = 1550
    for b in bullets:
        d.text((M + 24, y), "•", font=F["bold"](22), fill=SUCCESS)
        draw_wrapped(d, M + 54, y, b, F["reg"](18), SUB, W - M * 2 - 80, spacing=4)
        y += 46
    box(d, (M + 24, 1746, W - M - 24, 2050), ACCENT_SOFT, outline=LINE, radius=20)
    d.text((M + 48, 1770), "Contoh keputusan aman", font=F["bold"](21), fill=INK)
    safe = [
        "Backup dulu sebelum restore atau operasi besar.",
        "Gunakan Koreksi Stok untuk selisih, bukan ubah angka manual.",
        "Gunakan Stok Opname kalau ingin hitung fisik terstruktur.",
        "Pakai Aktivitas dan Audit untuk cek jejak perubahan.",
    ]
    for i, t in enumerate(safe):
        d.text((M + 48, 1820 + i * 44), "-> " + t, font=F["reg"](17), fill=SUB)
    return img


def page_examples_sales():
    img, d = new_page()
    title_block(d, "Contoh 1: Penjualan cepat", "Contoh ini paling sering dipakai karyawan.", 5)

    box(d, (M, 220, 760, 840), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 244), "Langkah singkat", font=F["bold"](24), fill=INK)
    steps = [
        ("1", "Cari PARACETAMOL", ACCENT),
        ("2", "Masukkan qty 2", SUCCESS),
        ("3", "Pilih TUNAI", PURPLE),
        ("4", "Klik Simpan Transaksi", AMBER),
    ]
    yy = 300
    for n, t, c in steps:
        badge(d, M + 24, yy, 48, 48, n, c, WHITE)
        d.text((M + 88, yy + 10), t, font=F["reg"](20), fill=SUB)
        yy += 92

    box(d, (M, 884, 760, 1500), ACCENT_SOFT, outline=LINE, radius=24)
    d.text((M + 24, 908), "Contoh isi transaksi", font=F["bold"](24), fill=INK)
    sample = [
        "Obat: PARACETAMOL",
        "Qty: 2",
        "Harga: Rp 3.000",
        "Subtotal: Rp 6.000",
        "Diskon: Rp 0",
        "Metode: TUNAI",
        "Bayar: Rp 10.000",
        "Kembali: Rp 4.000",
    ]
    y = 968
    for s in sample:
        d.text((M + 24, y), s, font=F["reg"](19), fill=SUB)
        y += 52
    badge(d, M + 24, 1364, 300, 48, "Kode transaksi: T1", SUCCESS, WHITE)
    badge(d, 338, 1364, 250, 48, "Periode cut-off 15:00", PURPLE, WHITE)

    box(d, (844, 220, W - M, 1500), WHITE, outline=LINE, radius=24)
    d.text((866, 244), "Gambar alur kerja", font=F["bold"](24), fill=INK)
    draw_sales_mock(d, 866, 294, 700, 1120)

    box(d, (M, 1540, W - M, 2100), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 1564), "Yang akan terlihat setelah simpan", font=F["bold"](24), fill=INK)
    result = [
        "Stok obat langsung berkurang.",
        "Kartu stok mencatat mutasi keluar.",
        "Laporan tunai atau QRIS ikut bertambah.",
        "Struk bisa dicetak kalau printer siap.",
    ]
    for i, t in enumerate(result):
        d.text((M + 24, 1620 + i * 50), "• " + t, font=F["reg"](18), fill=SUB)
    box(d, (840, 1580, W - M, 2030), ACCENT_SOFT, outline=LINE, radius=20)
    d.text((862, 1604), "Tips kecil", font=F["bold"](21), fill=INK)
    tips = [
        "Diskon boleh diisi 0 kalau tidak ada potongan.",
        "QRIS tidak butuh uang kembali.",
        "Kalau stok kurang, transaksi ditolak dulu.",
    ]
    for i, t in enumerate(tips):
        d.text((862, 1654 + i * 44), "-> " + t, font=F["reg"](17), fill=SUB)
    return img


def page_examples_purchase():
    img, d = new_page()
    title_block(d, "Contoh 2: Pembelian dan stok masuk", "Bagian ini khusus Super Admin atau user yang punya izin pembelian.", 6)

    box(d, (M, 220, 770, 1410), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 244), "Langkah pembelian", font=F["bold"](24), fill=INK)
    steps = [
        "Isi supplier dan nomor faktur.",
        "Tambahkan obat satu per satu.",
        "Isi qty, harga beli, batch, dan expired.",
        "Simpan faktur.",
    ]
    y = 302
    for i, t in enumerate(steps, 1):
        badge(d, M + 24, y, 46, 46, str(i), [ACCENT, SUCCESS, PURPLE, AMBER][i - 1], WHITE)
        draw_wrapped(d, M + 86, y + 6, t, F["reg"](19), SUB, 620, spacing=4)
        y += 104
    box(d, (M + 24, 772, 720, 1320), ACCENT_SOFT, outline=LINE, radius=20)
    d.text((M + 48, 796), "Contoh faktur", font=F["bold"](21), fill=INK)
    example = [
        "Supplier: PT Sehat Sentosa",
        "Faktur: INV-SIM-001",
        "Item 1: PARACETAMOL - 50",
        "Item 2: PARACETAMOL - 30",
        "Item 3: CTM - 60",
        "Batch dan expired wajib jelas.",
    ]
    yy = 846
    for e in example:
        d.text((M + 48, yy), e, font=F["reg"](17), fill=SUB)
        yy += 40

    box(d, (852, 220, W - M, 1410), WHITE, outline=LINE, radius=24)
    d.text((874, 244), "Gambar pembelian", font=F["bold"](24), fill=INK)
    draw_purchase_mock(d, 874, 294, 680, 1080)

    box(d, (M, 1462, W - M, 2100), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 1486), "Hasil yang diharapkan", font=F["bold"](24), fill=INK)
    outcomes = [
        "Stok bertambah di MSK dan SISA.",
        "Batch baru muncul di menu Lihat Batch.",
        "Expired date masuk ke kartu stok dan laporan.",
        "Harga beli dan harga jual ikut tersimpan.",
        "Penjualan nanti akan memakai batch terdekat dulu (FEFO).",
    ]
    for i, t in enumerate(outcomes):
        d.text((M + 24, 1540 + i * 48), "• " + t, font=F["reg"](18), fill=SUB)
    box(d, (834, 1518, W - M, 2020), ACCENT_SOFT, outline=LINE, radius=20)
    d.text((856, 1542), "Kalau ada selisih", font=F["bold"](21), fill=INK)
    notes = [
        "Jangan ubah angka master secara manual.",
        "Pakai Koreksi Stok atau Stok Opname.",
        "Tuliskan alasan yang singkat dan jelas.",
        "Audit akan menyimpan jejak perubahan.",
    ]
    for i, t in enumerate(notes):
        d.text((856, 1592 + i * 44), "-> " + t, font=F["reg"](17), fill=SUB)
    return img


def page_reports_and_help():
    img, d = new_page()
    title_block(d, "Laporan, backup, dan hal penting", "Penutup yang paling sering dicari saat operasional.", 7)

    box(d, (M, 220, 770, 980), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 244), "Laporan yang tersedia", font=F["bold"](24), fill=INK)
    items = [
        "Penjualan Tunai",
        "Penjualan QRIS",
        "Gabungan penjualan",
        "Pembelian supplier",
        "Stok dan batch",
        "Expired dan diskon",
        "Kartu stok dan audit",
    ]
    for i, it in enumerate(items):
        d.text((M + 24, 300 + i * 44), "• " + it, font=F["reg"](18), fill=SUB)

    box(d, (852, 220, W - M, 980), WHITE, outline=LINE, radius=24)
    d.text((874, 244), "Backup dan restore", font=F["bold"](24), fill=INK)
    backups = [
        "Backup salinan database lokal.",
        "Restore hanya untuk Super Admin.",
        "Lakukan backup sebelum pekerjaan besar.",
        "Setelah restore, buka ulang aplikasi.",
    ]
    for i, it in enumerate(backups):
        d.text((874, 302 + i * 54), "• " + it, font=F["reg"](18), fill=SUB)
    badge(d, 874, 578, 420, 50, "Database tetap lokal dan offline", ACCENT, WHITE)

    box(d, (M, 1040, 770, 1680), ACCENT_SOFT, outline=LINE, radius=24)
    d.text((M + 24, 1064), "Masalah yang sering muncul", font=F["bold"](24), fill=INK)
    problems = [
        ("Login gagal", "Cek username dan password."),
        ("Stok tidak cukup", "Lihat batch dan expired."),
        ("Laporan kosong", "Periksa tanggal dan cut-off 15:00."),
        ("Printer belum jalan", "Cek driver dan printer default."),
    ]
    yy = 1120
    for title, desc in problems:
        d.text((M + 24, yy), title, font=F["bold"](18), fill=INK)
        d.text((M + 210, yy), desc, font=F["reg"](18), fill=SUB)
        yy += 56

    box(d, (852, 1040, W - M, 1680), WHITE, outline=LINE, radius=24)
    d.text((874, 1064), "Checklist sebelum operasional", font=F["bold"](24), fill=INK)
    checklist = [
        "Tanggal dan jam komputer benar.",
        "Akun dipakai sesuai role.",
        "Master obat dan supplier sudah rapi.",
        "Batch dan expired diisi saat barang masuk.",
        "Backup dibuat sebelum hari kerja besar.",
    ]
    for i, it in enumerate(checklist):
        badge(d, 874, 1124 + i * 74, 42, 42, "✓", SUCCESS, WHITE)
        draw_wrapped(d, 934, 1130 + i * 74, it, F["reg"](18), SUB, 520, spacing=4)

    box(d, (M, 1740, W - M, 2100), WHITE, outline=LINE, radius=24)
    d.text((M + 24, 1764), "Catatan akhir", font=F["bold"](24), fill=INK)
    final = [
        "Karyawan fokus ke transaksi harian.",
        "Super Admin menjaga data, stok, dan backup.",
        "Semua data tersimpan lokal, jadi tetap jalan tanpa internet.",
    ]
    for i, it in enumerate(final):
        d.text((M + 24, 1820 + i * 46), "• " + it, font=F["reg"](18), fill=SUB)
    badge(d, 970, 1820, 554, 54, "Siap dipakai di laptop client / Windows 7 SP1", PURPLE, WHITE)
    return img


def save_pages(pages: Sequence[Image.Image]):
    DIST.mkdir(parents=True, exist_ok=True)
    PAGE_DIR.mkdir(parents=True, exist_ok=True)
    pngs: List[Path] = []
    for i, img in enumerate(pages, 1):
        path = PAGE_DIR / f"page_{i:02d}.png"
        img.save(path)
        pngs.append(path)
    pdf_images = [Image.open(p).convert("RGB") for p in pngs]
    pdf_images[0].save(
        PDF_PATH,
        "PDF",
        resolution=200.0,
        save_all=True,
        append_images=pdf_images[1:],
    )
    return pngs


def main():
    pages = [
        page_cover(),
        page_role_comparison(),
        page_employee_flow(),
        page_admin_flow(),
        page_examples_sales(),
        page_examples_purchase(),
        page_reports_and_help(),
    ]
    save_pages(pages)
    print(PDF_PATH)


if __name__ == "__main__":
    main()
