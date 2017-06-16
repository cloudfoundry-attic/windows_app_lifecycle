
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Builder.Properties;

namespace Builder
{
    public class TarGZFile
    {
        private static string TarArchiverPath(string filename)
        {
            var uri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            return Path.Combine(Path.GetDirectoryName(uri.LocalPath), filename);
        }

        public static void CreateFromDirectory(string fullSourcePath, string destinationArchiveFileName)
        {
            var parentPath = Path.GetDirectoryName(fullSourcePath);
            var baseName = Path.GetFileName(fullSourcePath);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = ".";
            }

            var process = new Process();
            var processStartInfo = process.StartInfo;
            processStartInfo.FileName = TarArchiverPath("tar.exe");
            processStartInfo.Arguments = "czf " + destinationArchiveFileName + " -C " + parentPath + " " + baseName;
            processStartInfo.UseShellExecute = false;
            process.Start();
            process.WaitForExit();
            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                throw new Exception("Failed to create archive");
            }
        }
    }
}
