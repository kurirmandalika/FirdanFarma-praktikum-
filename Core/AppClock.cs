using System;
using System.Diagnostics;

namespace ApotekFirdanFarma.Core
{
    internal static class AppClock
    {
        private static readonly object Sync = new object();
        private static DateTime _startupLocalTime;
        private static DateTime _lastObservedLocalTime;
        private static long _lastObservedTicks;
        private static bool _initialized;
        private static bool _warningRaised;

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized) return;
                _startupLocalTime = DateTime.Now;
                _lastObservedLocalTime = _startupLocalTime;
                _lastObservedTicks = Stopwatch.GetTimestamp();
                _initialized = true;
            }
        }

        public static DateTime StartupLocalTime
        {
            get
            {
                EnsureInitialized();
                lock (Sync) return _startupLocalTime;
            }
        }

        public static string StartupText => StartupLocalTime.ToString("dd MMM yyyy HH:mm:ss");

        public static string CurrentText => DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");

        public static bool Observe(out string warning)
        {
            EnsureInitialized();
            warning = null;
            var now = DateTime.Now;
            var ticks = Stopwatch.GetTimestamp();
            lock (Sync)
            {
                var wallClockSeconds = (now - _lastObservedLocalTime).TotalSeconds;
                var stopwatchSeconds = (ticks - _lastObservedTicks) / (double)Stopwatch.Frequency;
                var clockDriftSeconds = wallClockSeconds - stopwatchSeconds;
                if (!_warningRaised && System.Math.Abs(clockDriftSeconds) >= 60)
                {
                    warning = clockDriftSeconds < 0
                        ? "Jam Windows mundur berubah saat aplikasi berjalan. Cek pengaturan waktu client."
                        : "Jam Windows maju berubah saat aplikasi berjalan. Cek pengaturan waktu client.";
                    _warningRaised = true;
                }

                _lastObservedLocalTime = now;
                _lastObservedTicks = ticks;
            }
            return warning == null;
        }

        public static void ResetObservation()
        {
            lock (Sync)
            {
                _lastObservedLocalTime = DateTime.Now;
                _lastObservedTicks = Stopwatch.GetTimestamp();
                _warningRaised = false;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_initialized) Initialize();
        }
    }
}
