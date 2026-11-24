using System.Collections.Generic;
using ApotekFirdanFarma.Services.Abstractions;

namespace ApotekFirdanFarma.Tests.Doubles
{
    // SPY: tetap berfungsi sebagai pengganti dependency, tetapi MEREKAM interaksi
    // supaya bisa diperiksa test setelah Act (tanpa ekspektasi yang diset di depan seperti Mock).

    /// <summary>Spy audit logger: menyimpan semua AuditEntry yang dicatat.</summary>
    internal sealed class SpyAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = new List<AuditEntry>();
        public void Log(AuditEntry entry) { Entries.Add(entry); }
    }

    /// <summary>Spy repository: meneruskan FindByUsername ke repository di dalamnya dan menghitung panggilan UpdatePasswordHash.</summary>
    internal sealed class SpyUserRepository : IUserRepository
    {
        private readonly IUserRepository _inner;
        public int UpdatePasswordHashCalls { get; private set; }
        public SpyUserRepository(IUserRepository inner) { _inner = inner; }
        public UserCredential FindByUsername(string username) { return _inner.FindByUsername(username); }
        public void UpdatePasswordHash(long userId, string passwordHash, string nowIso)
        {
            UpdatePasswordHashCalls++;
            _inner.UpdatePasswordHash(userId, passwordHash, nowIso);
        }
    }

    /// <summary>Spy unit of work: merekam audit, jumlah Commit, dan jumlah UpdatePasswordHash.</summary>
    internal sealed class SpyUnitOfWork : IAuthUnitOfWork
    {
        private readonly SpyUserRepository _users;
        private readonly SpyAuditLogger _audit = new SpyAuditLogger();
        public SpyUnitOfWork(IUserRepository inner) { _users = new SpyUserRepository(inner); }
        public IUserRepository Users { get { return _users; } }
        public IAuditLogger Audit { get { return _audit; } }
        public int CommitCalls { get; private set; }
        public void Commit() { CommitCalls++; }
        public void Dispose() { }
        public List<AuditEntry> AuditEntries { get { return _audit.Entries; } }
        public int UpdatePasswordHashCalls { get { return _users.UpdatePasswordHashCalls; } }
    }
}
