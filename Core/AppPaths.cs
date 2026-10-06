using System;
using System.IO;

namespace ApotekFirdanFarma.Core
{
    public static class AppPaths
    {
        public static string DataDirectory
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FirdanFarma");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string DatabasePath => Path.Combine(DataDirectory, "firdan_farma.db");
        public static string BackupDirectory
        {
            get
            {
                var dir = Path.Combine(DataDirectory, "backup");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }
}
