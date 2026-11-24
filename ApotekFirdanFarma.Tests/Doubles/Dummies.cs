using System;
using ApotekFirdanFarma.Services.Abstractions;

namespace ApotekFirdanFarma.Tests.Doubles
{
    // DUMMY: hanya mengisi parameter yang wajib ada. Kalau sampai dipakai, test langsung gagal
    // (membuktikan kode yang diuji tidak menyentuh dependency ini).
    internal static class Dummies
    {
        public static readonly Func<IAuthUnitOfWork> UnitOfWorkFactory =
            () => { throw new NotSupportedException("DUMMY: unit of work tidak boleh dibuat pada test ini."); };

        public static readonly IPasswordHasher Hasher = new DummyPasswordHasher();
        public static readonly IClock Clock = new DummyClock();
    }

    internal sealed class DummyPasswordHasher : IPasswordHasher
    {
        public bool Verify(string password, string encodedHash) { throw new NotSupportedException("DUMMY: hasher tidak boleh dipakai."); }
        public string Hash(string password) { throw new NotSupportedException("DUMMY: hasher tidak boleh dipakai."); }
    }

    internal sealed class DummyClock : IClock
    {
        public DateTime Now { get { throw new NotSupportedException("DUMMY: clock tidak boleh dipakai."); } }
    }
}
