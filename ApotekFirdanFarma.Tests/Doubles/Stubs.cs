using System;
using ApotekFirdanFarma.Services.Abstractions;

namespace ApotekFirdanFarma.Tests.Doubles
{
    // STUB: mengembalikan jawaban tetap yang sudah disiapkan; tidak ada logika dan tidak diverifikasi.

    /// <summary>Stub repository: selalu mengembalikan user yang diberikan (atau null = "tidak ditemukan").</summary>
    internal sealed class StubUserRepository : IUserRepository
    {
        private readonly UserCredential _user;
        public StubUserRepository(UserCredential userOrNull) { _user = userOrNull; }
        public UserCredential FindByUsername(string username) { return _user; }
        public void UpdatePasswordHash(long userId, string passwordHash, string nowIso) { }
    }

    /// <summary>Stub hasher: hasil Verify ditentukan dari luar, Hash selalu mengembalikan nilai tetap.</summary>
    internal sealed class StubPasswordHasher : IPasswordHasher
    {
        private readonly bool _verifyResult;
        public const string FixedHash = "HASH-BARU";
        public StubPasswordHasher(bool verifyResult) { _verifyResult = verifyResult; }
        public bool Verify(string password, string encodedHash) { return _verifyResult; }
        public string Hash(string password) { return FixedHash; }
    }

    /// <summary>Stub clock: waktu selalu 2026-01-15 10:00:00.</summary>
    internal sealed class StubClock : IClock
    {
        public static readonly DateTime Fixed = new DateTime(2026, 1, 15, 10, 0, 0);
        public DateTime Now { get { return Fixed; } }
    }

    /// <summary>Stub unit of work: membungkus repository + audit logger yang diberikan, tanpa transaksi.</summary>
    internal sealed class StubUnitOfWork : IAuthUnitOfWork
    {
        public StubUnitOfWork(IUserRepository users, IAuditLogger audit) { Users = users; Audit = audit; }
        public IUserRepository Users { get; }
        public IAuditLogger Audit { get; }
        public void Commit() { }
        public void Dispose() { }
    }

    /// <summary>Stub audit logger: menerima catatan dan membuangnya (test ini tidak peduli audit).</summary>
    internal sealed class NullAuditLogger : IAuditLogger
    {
        public void Log(AuditEntry entry) { }
    }
}
