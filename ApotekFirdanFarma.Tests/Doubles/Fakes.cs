using System.Collections.Generic;
using ApotekFirdanFarma.Services.Abstractions;

namespace ApotekFirdanFarma.Tests.Doubles
{
    // FAKE: implementasi sederhana yang benar-benar bekerja (in-memory), pengganti SQLite untuk test.

    internal sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, UserCredential> _byUsername = new Dictionary<string, UserCredential>();

        public void Add(UserCredential user) { _byUsername[user.Username] = user; }

        public UserCredential FindByUsername(string username)
        {
            UserCredential found;
            return _byUsername.TryGetValue(username, out found) ? found : null;
        }

        public void UpdatePasswordHash(long userId, string passwordHash, string nowIso)
        {
            foreach (var u in _byUsername.Values)
                if (u.Id == userId) u.PasswordHash = passwordHash;
        }
    }

    /// <summary>Fake hasher: "hash" = "FAKE:" + password. Verify membandingkan sungguhan (bukan jawaban tetap).</summary>
    internal sealed class FakePasswordHasher : Services.Abstractions.IPasswordHasher
    {
        public string Hash(string password) { return "FAKE:" + password; }
        public bool Verify(string password, string encodedHash) { return encodedHash == Hash(password); }
    }

    /// <summary>
    /// Fake unit of work (pengganti transaksi SQLite). Catatan: properti Committed/Disposed dan AuditEntries
    /// berperilaku sebagai SPY (merekam apa yang terjadi supaya bisa dicek test); sisanya Fake.
    /// </summary>
    internal sealed class FakeUnitOfWork : IAuthUnitOfWork
    {
        public FakeUnitOfWork(FakeUserRepository users) { Users = users; Audit = new SpyAuditLogger(); }
        public IUserRepository Users { get; }
        public IAuditLogger Audit { get; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public void Commit() { Committed = true; }
        public void Dispose() { Disposed = true; }
        public List<AuditEntry> AuditEntries { get { return ((SpyAuditLogger)Audit).Entries; } }
    }
}
