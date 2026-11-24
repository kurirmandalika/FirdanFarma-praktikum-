using System;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services.Abstractions
{
    // Abstraksi akses data untuk fitur reset password.
    // Tujuan: logika & decision di AuthService tetap sama, hanya akses data yang bisa diganti test double.

    /// <summary>Data akun apa adanya dari penyimpanan (termasuk status aktif).</summary>
    internal sealed class UserCredential
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>Satu catatan audit. Dipetakan 1:1 ke AuditService.LogDetailed oleh adapter asli.</summary>
    internal sealed class AuditEntry
    {
        public AppUser User { get; set; }
        public string Action { get; set; }
        public string Entity { get; set; }
        public long? EntityId { get; set; }
        public string ReferenceCode { get; set; }
        public string NewValue { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; }
        public string UsernameOverride { get; set; }
        public string RoleOverride { get; set; }
    }

    internal interface IUserRepository
    {
        /// <summary>Mengembalikan akun apa adanya (aktif maupun nonaktif), atau null kalau tidak ada.</summary>
        UserCredential FindByUsername(string username);
        void UpdatePasswordHash(long userId, string passwordHash, string nowIso);
    }

    internal interface IAuditLogger
    {
        void Log(AuditEntry entry);
    }

    internal interface IPasswordHasher
    {
        bool Verify(string password, string encodedHash);
        string Hash(string password);
    }

    internal interface IClock
    {
        DateTime Now { get; }
    }

    /// <summary>
    /// Satu unit kerja = satu transaksi. Repository dan audit memakai transaksi yang sama.
    /// Dispose tanpa Commit() berarti rollback (sama seperti perilaku semula).
    /// </summary>
    internal interface IAuthUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IAuditLogger Audit { get; }
        void Commit();
    }
}
