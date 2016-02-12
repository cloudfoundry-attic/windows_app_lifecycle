using LibGit2Sharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Serialization;

namespace Builder
{
    public class Program
    {
        private static string[] buildpackBinaries = new string[] { "detect", "compile", "release" };

        private static string[] binariesExtensions = new string[] { ".EXE", ".BAT", ".CMD" };

        private static bool IsWindowsBuildpack(string buildpackDir)
        {
            foreach (var app in buildpackBinaries)
            {
                bool found = false;
                foreach (string ext in binariesExtensions)
                {
                    if (File.Exists(Path.Combine(buildpackDir, "bin", app + ext)))
                    {
                        found = true;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            return true;
        }

        private static string GetExecutable(string path, string file)
        {
            foreach (string ext in binariesExtensions)
            {
                if (File.Exists(Path.Combine(path, file + ext)))
                {
                    return Path.Combine(path, file + ext);
                }
            }

            throw new Exception(String.Format("No executable found for '{0}' in '{1}'", file, path));
        }

        private static int RunBuildpackProcess(string path, string args, TextWriter outputStream, TextWriter errorStream)
        {
            var p = new Process();
            p.StartInfo.FileName = path;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            p.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                outputStream.WriteLine(e.Data);
            };
            p.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                errorStream.WriteLine(e.Data);
            };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();

            return p.ExitCode;
        }

        private static string GetBuildpackDirName(string buildpackName)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(buildpackName));
                return BitConverter.ToString(data).Replace("-", "");
            }
        }

        private static void SanitizeArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-") && !args[i].StartsWith("--"))
                {
                    args[i] = "-" + args[i];
                }
            }
        }

        private static Dictionary<string, string> GetProcfileProcessTypes(List<string> files)
        {
            var procfiles = files.Where(x => Path.GetFileName(x).ToLower() == "procfile").ToList();

            if (procfiles.Any())
            {
                using (var procfileStream = new StreamReader(procfiles.First()))
                {
                    return new Deserializer().Deserialize<Dictionary<string, string>>(procfileStream);
                }
            }
            else
            {
                return null;
            }
        }

        private static void DownloadAndExtractZip(Uri source, string destination)
        {
            var tlsIgnoreFailureCallback = new RemoteCertificateValidationCallback(delegate { return true; });
            string tempFile = null;

            try
            {
                tempFile = Path.GetTempFileName();

                using (var webClient = new WebClient())
                {
                    webClient.DownloadFile(source, tempFile);
                }

                ZipFile.ExtractToDirectory(tempFile, destination);
            }
            finally
            {
                if (tempFile != null)
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static bool IsZipBuildpack(string filename)
        {
            return filename.EndsWith(".zip", false, CultureInfo.InvariantCulture);
        }

        private static void DownloadBuildpacks(string[] buildpacks, string buildpacksDir)
        {
            foreach (var buildpackName in buildpacks)
            {
                Uri downloadUri = null;
                var downloadableBuildoack = Uri.TryCreate(buildpackName, UriKind.Absolute, out downloadUri);
                if (downloadableBuildoack)
                {
                    var buildpackDir = Path.Combine(buildpacksDir, GetBuildpackDirName(buildpackName));
                    if (IsZipBuildpack(buildpackName))
                    {
                        DownloadAndExtractZip(downloadUri, buildpackDir);
                    }
                    else
                    {
                        var cloneOptions = new CloneOptions();
                        if (!string.IsNullOrEmpty(downloadUri.Fragment))
                        {
                            cloneOptions.BranchName = downloadUri.Fragment.Substring(1);
                        }

                        var cloneUri = new UriBuilder(downloadUri);
                        cloneUri.Fragment = "";
                        Repository.Clone(cloneUri.ToString(), buildpackDir, cloneOptions);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            SanitizeArgs(args);
            var options = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                Environment.Exit(1);
            }

            Run(options);
        }

        private static void Run(Options options)
        {
            var rootDir = Directory.GetCurrentDirectory();

            var appPath = rootDir + options.BuildDir;
            var buildpacksDir = rootDir + options.BuildpacksDir;

            var buildCacheDir = rootDir + options.BuildArtifactsCacheDir;
            Directory.CreateDirectory(buildCacheDir);

            var outputCache = rootDir + options.OutputBuildArtifactsCache;
            var outputDropletPath = rootDir + options.OutputDroplet;

            string detectedBuildpack = "";
            string detectedBuildpackDir = "";
            string detectOutput = "";
            bool buildpackDetected = false;

            var buildpacks = new string[0];
            if (options.BuildpackOrder != null)
            {
                buildpacks = options.BuildpackOrder.Split(new char[] { ',' });
            }

            DownloadBuildpacks(buildpacks, buildpacksDir);

            foreach (var buildpackName in buildpacks)
            {
                var buildpackDir = Path.Combine(buildpacksDir, GetBuildpackDirName(buildpackName));

                if (!IsWindowsBuildpack(buildpackDir))
                {
                    continue;
                }

                if (options.SkipDetect == OptionBool.False)
                {
                    var detectPath = GetExecutable(Path.Combine(buildpackDir, "bin"), "detect");

                    var outputStream = new StringWriter();
                    var exitCode = RunBuildpackProcess(detectPath, appPath, outputStream, Console.Error);
                    detectOutput = outputStream.ToString();

                    detectOutput = detectOutput.TrimEnd(new char[] { '\n', '\r' });

                    if (exitCode == 0)
                    {
                        detectedBuildpack = buildpackName;
                        detectedBuildpackDir = buildpackDir;
                        buildpackDetected = true;
                        break;
                    }
                }
                else
                {
                    detectedBuildpack = buildpackName;
                    detectedBuildpackDir = buildpackDir;
                    buildpackDetected = true;
                    break;
                }
            }

            if (!buildpackDetected)
            {
                Console.WriteLine("None of the buildpacks detected a compatible application");
                Environment.Exit(222);
            }

            var compilePath = GetExecutable(Path.Combine(detectedBuildpackDir, "bin"), "compile");

            var compoileExitCode = RunBuildpackProcess(compilePath, appPath + " " + buildCacheDir, Console.Out, Console.Error);
            if (compoileExitCode != 0)
            {
                Console.WriteLine("Failed to compile droplet");
                Environment.Exit(223);
            }

            Dictionary<string, string> procfileProcessTypes = GetProcfileProcessTypes(Directory.EnumerateFiles(appPath).ToList());

            var releaseBinPath = GetExecutable(Path.Combine(detectedBuildpackDir, "bin"), "release");

            var releaseStream = new StringWriter();
            var releaseExitCode = RunBuildpackProcess(releaseBinPath, appPath, releaseStream, Console.Error);
            if (releaseExitCode != 0)
            {
                Console.WriteLine("Failed to build droplet release");
                Environment.Exit(224);
            }

            var releaseOutput = releaseStream.ToString();
            ReleaseInfo releaseInfo = new Deserializer(ignoreUnmatched: true).Deserialize<ReleaseInfo>(new StringReader(releaseOutput));

            var outputMetadata = new OutputMetadata()
            {
                LifecycleType = "buildpack",
                LifecycleMetadata = new LifecycleMetadata()
                {
                    BuildpackKey = detectedBuildpack,
                    DetectedBuildpack = detectOutput
                },
                ProcessTypes = procfileProcessTypes ?? releaseInfo.defaultProcessTypes,
                ExecutionMetadata = ""
            };

            File.WriteAllText(rootDir + options.OutputMetadata, JsonConvert.SerializeObject(outputMetadata));

            TarGZFile.CreateFromDirectory(buildCacheDir + "\\", outputCache);
            TarGZFile.CreateFromDirectory(appPath, outputDropletPath);
        }
    }
}
