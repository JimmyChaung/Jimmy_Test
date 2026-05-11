using System;
using System.IO;

namespace admin_web.Infrastructure
{
    public static class JarDownloader
    {
        private static readonly string SourceDir =
            @"\\192.168.1.20\Rmc\System Admin\共用\個人存放區\Jimmy\useful";

        public static string EnsureJar(string jarName)
        {
            string localDir = Path.Combine(Directory.GetCurrentDirectory(), "ExternalTools");
            string localPath = Path.Combine(localDir, jarName);
            string sourcePath = Path.Combine(SourceDir, jarName);

            Directory.CreateDirectory(localDir);

            if (!File.Exists(localPath))
            {
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("找不到來源 jar", sourcePath);

                File.Copy(sourcePath, localPath, true);
            }

            return localPath;
        }
    }
}