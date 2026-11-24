using System;
using System.Collections.Generic;
using ApotekFirdanFarma.Services;
using ApotekFirdanFarma.Services.Abstractions;
using ApotekFirdanFarma.Tests.Doubles;
using Moq;
using Xunit;

namespace ApotekFirdanFarma.Tests
{
    /// <summary>
    /// Unit test AuthService.ResetPasswordWithOldPassword (overload internal).
    /// Nomor TC mengacu ke docs/whitebox-testing-report.md (Pre-Lab).
    /// </summary>
    public class ResetPasswordTests
    {
        private static UserCredential Kasir(bool aktif = true, string hash = "HASH-LAMA")
        {
            return new UserCredential { Id = 7, Username = "kasir", DisplayName = "Kasir Satu", Role = "KARYAWAN", PasswordHash = hash, IsActive = aktif };
        }

        private static void Reset(string user, string oldPw, string newPw, string confirm,
            Func<IAuthUnitOfWork> factory, IPasswordHasher hasher, IClock clock)
        {
            AuthService.ResetPasswordWithOldPassword(user, oldPw, newPw, confirm, factory, hasher, clock);
        }

        // ============================================================
        // A. VALIDASI INPUT (TC1-TC6, TC10, TC13): memakai DUMMY
        //    Dummy factory melempar NotSupportedException kalau dipanggil,
        //    jadi test lulus hanya kalau validasi berhenti SEBELUM menyentuh database.
        // ============================================================

        // [CONTOH UTAMA: Dummy]
        [Fact] // TC1 | negatif | D1-True (B1)
        public void ResetPassword_UsernameKosong_MelemparUsernameWajibDiisi()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory; // Dummy: tidak boleh dipakai

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("", "lama123", "baru123", "baru123", factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Username wajib diisi.", ex.Message);
        }

        [Fact] // TC2 | negatif | D2-True (B3)
        public void ResetPassword_PasswordLamaKosong_MelemparPasswordSebelumnyaWajibDiisi()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory;

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "", "baru123", "baru123", factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Password sebelumnya wajib diisi.", ex.Message);
        }

        [Fact] // TC3 | negatif | D3-True (B5)
        public void ResetPassword_PasswordBaruKosong_MelemparPasswordBaruWajibDiisi()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory;

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "lama123", "", "", factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Password baru wajib diisi.", ex.Message);
        }

        [Fact] // TC4 | negatif | D4-True (B7)
        public void ResetPassword_PasswordBaruTigaKarakter_MelemparMinimalEnamKarakter()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory;

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "lama123", "abc", "abc", factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Password baru minimal 6 karakter.", ex.Message);
        }

        [Fact] // TC5 | negatif | D5-True (B9)
        public void ResetPassword_KonfirmasiBeda_MelemparKonfirmasiTidakCocok()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory;

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "lama123", "baru123", "beda999", factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Konfirmasi password baru tidak cocok.", ex.Message);
        }

        [Fact] // TC6 | negatif | D6-True (B11)
        public void ResetPassword_PasswordBaruSamaDenganLama_MelemparTidakBolehSama()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory;

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "lama123", "lama123", "lama123", factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Password baru tidak boleh sama dengan password sebelumnya.", ex.Message);
        }

        [Fact] // TC10 | boundary | D4-True pada panjang 5 (batas bawah - 1)
        public void ResetPassword_PasswordBaruLimaKarakter_DitolakKarenaKurangDariEnam()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory;

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "lama123", "12345", "12345", factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Password baru minimal 6 karakter.", ex.Message);
        }

        [Fact] // TC13 | negatif | semua parameter null -> dinormalisasi "" -> D1-True
        public void ResetPassword_SemuaParameterNull_DianggapKosongDanDitolakDiUsername()
        {
            // Arrange
            var factory = Dummies.UnitOfWorkFactory;

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset(null, null, null, null, factory, Dummies.Hasher, Dummies.Clock));

            // Assert
            Assert.Equal("Username wajib diisi.", ex.Message);
        }

        // ============================================================
        // B. AKUN NONAKTIF (TC12): STUB menyuplai data, SPY merekam hasilnya
        // ============================================================

        // [CONTOH UTAMA: Stub] [CONTOH UTAMA: Spy]
        // Stub = StubUserRepository (input: akun nonaktif). Spy = SpyUnitOfWork (merekam audit, Commit, UpdatePasswordHash).
        [Fact] // TC12 | negatif | D7-False karena IsActive=false (sisi kedua kondisi &&) -> D8-True
        public void ResetPassword_AkunNonaktif_DitolakDenganAuditGagalDanTidakMengubahPassword()
        {
            // Arrange
            var stubRepo = new StubUserRepository(Kasir(aktif: false)); // Stub: selalu mengembalikan akun nonaktif
            var spyUow = new SpyUnitOfWork(stubRepo);                   // Spy: merekam interaksi

            // Act
            // Hasher = Dummy: kalau Verify dipanggil test gagal -> membuktikan D8 menghentikan alur sebelum D9
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "lama123", "baru123", "baru123", () => spyUow, Dummies.Hasher, new StubClock()));

            // Assert
            Assert.Equal("Username tidak ditemukan atau akun nonaktif.", ex.Message);
            var audit = Assert.Single(spyUow.AuditEntries);
            Assert.Equal("GAGAL", audit.Status);
            Assert.Equal("Username tidak ditemukan/nonaktif", audit.Reason);
            Assert.Equal(1, spyUow.CommitCalls);
            Assert.Equal(0, spyUow.UpdatePasswordHashCalls);
        }

        // ============================================================
        // C. ALUR DATABASE DENGAN MOCK (TC7, TC8, TC9): verifikasi interaksi + URUTAN panggilan
        // ============================================================

        private sealed class MockSet
        {
            public readonly List<string> Calls = new List<string>();
            public readonly List<AuditEntry> Entries = new List<AuditEntry>();
            public readonly Mock<IAuthUnitOfWork> Uow = new Mock<IAuthUnitOfWork>();
            public readonly Mock<IUserRepository> Users = new Mock<IUserRepository>();
            public readonly Mock<IAuditLogger> Audit = new Mock<IAuditLogger>();

            public MockSet(UserCredential userOrNull)
            {
                Users.Setup(u => u.FindByUsername(It.IsAny<string>())).Returns(userOrNull);
                Users.Setup(u => u.UpdatePasswordHash(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()))
                     .Callback(() => Calls.Add("UpdatePasswordHash"));
                Audit.Setup(a => a.Log(It.IsAny<AuditEntry>()))
                     .Callback<AuditEntry>(e => { Calls.Add("Audit.Log:" + e.Status); Entries.Add(e); });
                Uow.Setup(u => u.Users).Returns(Users.Object);
                Uow.Setup(u => u.Audit).Returns(Audit.Object);
                Uow.Setup(u => u.Commit()).Callback(() => Calls.Add("Commit"));
            }
        }

        [Fact] // TC7 | negatif | D7-False (user null) -> D8-True (B14, B15)
        public void ResetPassword_UserTidakAda_MencatatAuditGagalLaluCommitLaluMelempar()
        {
            // Arrange
            var m = new MockSet(null); // repository mengembalikan null (user tidak ada)

            // Act
            // Hasher = Dummy: tidak boleh dipanggil karena alur berhenti di D8
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("hantu", "lama123", "baru123", "baru123", () => m.Uow.Object, Dummies.Hasher, new StubClock()));

            // Assert
            Assert.Equal("Username tidak ditemukan atau akun nonaktif.", ex.Message);
            Assert.Equal(new[] { "Audit.Log:GAGAL", "Commit" }, m.Calls); // urutan: audit -> commit (throw terjadi setelah commit)
            m.Audit.Verify(a => a.Log(It.IsAny<AuditEntry>()), Times.Once());
            m.Uow.Verify(u => u.Commit(), Times.Once());
            m.Users.Verify(u => u.UpdatePasswordHash(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            var e = Assert.Single(m.Entries);
            Assert.Equal("RESET_PASSWORD", e.Action);
            Assert.Equal("GAGAL", e.Status);
            Assert.Equal("Username tidak ditemukan/nonaktif", e.Reason);
            Assert.Equal("hantu", e.UsernameOverride);
            Assert.Null(e.EntityId);
            Assert.Null(e.RoleOverride);
        }

        [Fact] // TC8 | negatif | D7-True -> D8-False -> D9-True (B13, B16, B17)
        public void ResetPassword_PasswordLamaSalah_MencatatAuditGagalLaluCommitLaluMelemparDanTidakMengubahHash()
        {
            // Arrange
            var m = new MockSet(Kasir());
            var hasher = new StubPasswordHasher(verifyResult: false); // Stub: password lama dianggap salah

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Reset("kasir", "salah!", "baru123", "baru123", () => m.Uow.Object, hasher, new StubClock()));

            // Assert
            Assert.Equal("Password sebelumnya salah.", ex.Message);
            Assert.Equal(new[] { "Audit.Log:GAGAL", "Commit" }, m.Calls);
            m.Users.Verify(u => u.UpdatePasswordHash(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            var e = Assert.Single(m.Entries);
            Assert.Equal("Password sebelumnya salah", e.Reason);
            Assert.Equal("GAGAL", e.Status);
            Assert.Equal(7L, e.EntityId);
            Assert.Equal("kasir", e.ReferenceCode);
            Assert.Equal("kasir", e.UsernameOverride);
            Assert.Equal("KARYAWAN", e.RoleOverride);
        }

        // [CONTOH UTAMA: Mock]
        [Fact] // TC9 | positif | D7-True -> D8-False -> D9-False (B13, B16, B18)
        public void ResetPassword_DataValid_UpdateHashLaluAuditBerhasilLaluCommit()
        {
            // Arrange
            var m = new MockSet(Kasir());
            var hasher = new StubPasswordHasher(verifyResult: true); // Stub: password lama benar, Hash() = "HASH-BARU"

            // Act
            Reset("kasir", "lama123", "baru123", "baru123", () => m.Uow.Object, hasher, new StubClock());

            // Assert
            Assert.Equal(new[] { "UpdatePasswordHash", "Audit.Log:BERHASIL", "Commit" }, m.Calls);
            m.Users.Verify(u => u.UpdatePasswordHash(7L, "HASH-BARU", "2026-01-15T10:00:00"), Times.Once());
            m.Audit.Verify(a => a.Log(It.IsAny<AuditEntry>()), Times.Once());
            m.Uow.Verify(u => u.Commit(), Times.Once());
            var e = Assert.Single(m.Entries);
            Assert.Equal("RESET_PASSWORD", e.Action);
            Assert.Equal("BERHASIL", e.Status);
            Assert.Equal(7L, e.EntityId);
            Assert.Equal("kasir", e.ReferenceCode);
            Assert.Equal("kasir", e.User.Username);
            Assert.Equal("KARYAWAN", e.User.Role);
            Assert.Equal("Reset password berhasil", e.Notes);
        }

        // ============================================================
        // D. ALUR END-TO-END DENGAN FAKE (TC9, TC11, TC14): memeriksa STATE akhir
        //    (uow.Committed/AuditEntries = bagian Spy dari FakeUnitOfWork)
        // ============================================================

        // [CONTOH UTAMA: Fake]
        [Fact] // TC9 (versi state) | positif
        public void ResetPassword_DataValid_HashDiRepositoryBerubahDanPasswordBaruBisaDiverifikasi()
        {
            // Arrange
            var hasher = new FakePasswordHasher();                      // Fake: Hash/Verify sungguhan versi sederhana
            var repo = new FakeUserRepository();                        // Fake: pengganti tabel users
            repo.Add(Kasir(hash: hasher.Hash("lama123")));
            var uow = new FakeUnitOfWork(repo);

            // Act
            Reset("kasir", "lama123", "baru123", "baru123", () => uow, hasher, new StubClock());

            // Assert
            var tersimpan = repo.FindByUsername("kasir").PasswordHash;
            Assert.Equal("FAKE:baru123", tersimpan);
            Assert.True(hasher.Verify("baru123", tersimpan));
            Assert.False(hasher.Verify("lama123", tersimpan));
            Assert.True(uow.Committed);
            Assert.True(uow.Disposed);
            Assert.Equal("BERHASIL", Assert.Single(uow.AuditEntries).Status);
        }

        [Fact] // TC11 | boundary | D4-False pada panjang tepat 6 -> lolos validasi
        public void ResetPassword_PasswordBaruTepatEnamKarakter_DiterimaDanTersimpan()
        {
            // Arrange
            var hasher = new FakePasswordHasher();
            var repo = new FakeUserRepository();
            repo.Add(Kasir(hash: hasher.Hash("lama123")));
            var uow = new FakeUnitOfWork(repo);

            // Act
            Reset("kasir", "lama123", "123456", "123456", () => uow, hasher, new StubClock());

            // Assert
            Assert.Equal("FAKE:123456", repo.FindByUsername("kasir").PasswordHash);
        }

        [Fact] // TC14 | positif | S1 trim: "  kasir  " dianggap "kasir"
        public void ResetPassword_UsernameBerspasi_DiTrimLaluDitemukan()
        {
            // Arrange
            var hasher = new FakePasswordHasher();
            var repo = new FakeUserRepository();
            repo.Add(Kasir(hash: hasher.Hash("lama123")));
            var uow = new FakeUnitOfWork(repo);

            // Act
            Reset("  kasir  ", "lama123", "baru123", "baru123", () => uow, hasher, new StubClock());

            // Assert
            Assert.Equal("FAKE:baru123", repo.FindByUsername("kasir").PasswordHash);
            Assert.Equal("kasir", uow.AuditEntries[0].ReferenceCode);
        }
    }
}
