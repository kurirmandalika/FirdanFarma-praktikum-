using System;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Security;

namespace ApotekFirdanFarma.Services.Abstractions
{
    // Implementasi asli (production) dari abstraksi di AuthAbstractions.cs.
    // SQL dan argumen audit sama persis dengan kode sebelum refactor.

    internal sealed class SqliteAuthUnitOfWork : IAuthUnitOfWork
    {
        private readonly SQLiteConnection _connection;
        private readonly SQLiteTransaction _transaction;

        public SqliteAuthUnitOfWork()
        {
            _connection = Database.Open();
            try
            {
                _transaction = _connection.BeginTransaction();
            }
            catch
            {
                _connection.Dispose();
                throw;
            }
            Users = new SqliteUserRepository(_connection, _transaction);
            Audit = new AuditServiceLogger(_connection, _transaction);
        }

        public IUserRepository Users { get; }
        public IAuditLogger Audit { get; }

        public void Commit()
        {
            _transaction.Commit();
        }

        public void Dispose()
        {
            _transaction.Dispose();
            _connection.Dispose();
        }
    }

    internal sealed class SqliteUserRepository : IUserRepository
    {
        private readonly SQLiteConnection _c;
        private readonly SQLiteTransaction _tx;

        public SqliteUserRepository(SQLiteConnection c, SQLiteTransaction tx)
        {
            _c = c;
            _tx = tx;
        }

        public UserCredential FindByUsername(string username)
        {
            using (var cmd = new SQLiteCommand("SELECT id, username, display_name, role, password_hash, is_active FROM users WHERE username=@u", _c, _tx))
            {
                cmd.Parameters.AddWithValue("@u", username);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new UserCredential
                    {
                        Id = Convert.ToInt64(r["id"]),
                        Username = Convert.ToString(r["username"]),
                        DisplayName = Convert.ToString(r["display_name"]),
                        Role = Convert.ToString(r["role"]),
                        PasswordHash = Convert.ToString(r["password_hash"]),
                        IsActive = Convert.ToInt32(r["is_active"]) == 1
                    };
                }
            }
        }

        public void UpdatePasswordHash(long userId, string passwordHash, string nowIso)
        {
            using (var cmd = new SQLiteCommand("UPDATE users SET password_hash=@p, updated_at=@now WHERE id=@id", _c, _tx))
            {
                cmd.Parameters.AddWithValue("@p", passwordHash);
                cmd.Parameters.AddWithValue("@now", nowIso);
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.ExecuteNonQuery();
            }
        }
    }

    internal sealed class AuditServiceLogger : IAuditLogger
    {
        private readonly SQLiteConnection _c;
        private readonly SQLiteTransaction _tx;

        public AuditServiceLogger(SQLiteConnection c, SQLiteTransaction tx)
        {
            _c = c;
            _tx = tx;
        }

        public void Log(AuditEntry e)
        {
            AuditService.LogDetailed(e.User, e.Action, e.Entity, e.EntityId, e.ReferenceCode, null, e.NewValue, null, null, null,
                e.Reason, e.Notes, e.Status, null, null, 0, 0, _c, _tx, e.UsernameOverride, e.RoleOverride);
        }
    }

    internal sealed class DefaultPasswordHasher : IPasswordHasher
    {
        public bool Verify(string password, string encodedHash)
        {
            return PasswordHasher.Verify(password, encodedHash);
        }

        public string Hash(string password)
        {
            return PasswordHasher.Hash(password);
        }
    }

    internal sealed class SystemClock : IClock
    {
        public DateTime Now
        {
            get { return DateTime.Now; }
        }
    }
}
