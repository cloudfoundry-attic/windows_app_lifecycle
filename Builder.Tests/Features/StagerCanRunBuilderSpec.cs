using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSpec;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace Builder.Tests.Specs.Features
{
    class StagerCanRunBuilderSpec : nspec
    {
        private void describe_()
        {
            string builderBinary = null;
            var arguments = new Dictionary<string, string>();
            Process process = null;
            string stdout = null;
            string stderr = null;

            string tmpZip = null;
            string currentDirectory = null;
            string workingDirectory = null;
            string appDir = null;
            string tmpDir = null;
            string buildpacksDir = null;

            act = () =>
            {
                process = new Process
                {
                    StartInfo =
                    {
                        FileName = builderBinary,
                        Arguments = arguments.Select(x => x.Key + " " + x.Value).Aggregate((x, y) => x + " " + y),
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    }
                };

                process.StartInfo.WorkingDirectory = workingDirectory;
                process.Start();
                stdout = process.StandardOutput.ReadToEnd();
                stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
            };

            before = () =>
            {
                workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                tmpDir = Path.Combine(workingDirectory, "tmp");
                appDir = Path.Combine(tmpDir, "app");
                buildpacksDir = Path.Combine(tmpDir, "buildpacks");
                Directory.CreateDirectory(tmpDir);
                Directory.CreateDirectory(appDir);
                Directory.CreateDirectory(buildpacksDir);

                currentDirectory =
                    Path.GetFullPath(
                        Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().CodeBase, "..", "..", "..",
                            "..").Replace("file:///", ""));
                builderBinary = Path.Combine(currentDirectory, "Builder", "bin", "Builder.exe");

                arguments = new Dictionary<string, string>
                {
                    {"-buildDir", "/tmp/app"},
                    {"-buildArtifactsCacheDir", "/tmp/cache"},
                    {"-buildpacksDir", "/tmp/buildpacks"},
                    {"-outputDroplet", "/tmp/droplet"},
                    {"-outputMetadata", "/tmp/result.json"},
                    {"-outputBuildArtifactsCache", "/tmp/output-cache"},
                    {"-skipDetect", "false"}                };
            };

            after = () =>
            {
                DeleteFileSystemInfo(new DirectoryInfo(workingDirectory));
                try
                {
                    if (tmpZip != null) {
                        File.Delete(tmpZip);
                    }
                }
                catch{}
            };

            context["given no buildpacks"] = () =>
            {
                string resultFile = null;

                before = () =>
                {
                    resultFile = Path.Combine(tmpDir, "result.json");
                    arguments.Remove("-buildpackOrder");
                };

                it["Does not create the result.json"] = () =>
                {
                    File.Exists(resultFile).should_be_false();
                };

                it["Exit code is 222"] = () =>
                {
                    process.ExitCode.should_be(222);
                };
            };

            context["given a buildpack and a non-valid app"] = () =>
            {
                string resultFile = null;

                before = () =>
                {
                    resultFile = Path.Combine(tmpDir, "result.json");
                    CopyDirectory(
                        Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "buildpacks", "run-buildpack"),
                        Path.Combine(buildpacksDir, MD5Hash("run-buildpack"))
                    );

                    CopyDirectory(Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "apps", "no-app"), appDir);
                    arguments["-buildpackOrder"] = "run-buildpack";
                };

                it["Does not create the result.json"] = () =>
                {
                    File.Exists(resultFile).should_be_false();
                };

                it["Exit code is 0"] = () =>
                {
                    process.ExitCode.should_be(222);
                };
            };

            context["given valid buildpacks and a valid app"] = () =>
            {
                string resultFile = null;

                before = () =>
                {
                    resultFile = Path.Combine(tmpDir, "result.json");
                    CopyDirectory(
                        Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "buildpacks", "run-buildpack"),
                        Path.Combine(buildpacksDir, MD5Hash("run-buildpack"))
                    );

                    CopyDirectory(
                        Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "buildpacks", "nop-buildpack"),
                        Path.Combine(buildpacksDir, MD5Hash("nop-buildpack"))
                    );

                    CopyDirectory(Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "apps", "run"), appDir);
                    arguments["-buildpackOrder"] = "run-buildpack";
                };

                it["Exit code is 0"] = () =>
                {
                    process.ExitCode.should_be(0);
                };

                it["Compile stdout and stderr is redirected"] = () =>
                {
                    stdout.should_contain("Nothing to do ...");
                    stderr.should_contain("No error");
                };

                it["Creates the result.json"] = () =>
                {
                    File.Exists(resultFile).should_be_true();
                };              

                it["Creates an empty build artifacts cache dir"] = () =>
                {
                    var fileName = Path.Combine(tmpDir, "cache");
                    Directory.Exists(fileName).should_be_true();
                };

                it["compresses the build artifacts cache dir into the output-cache file"] = () =>
                {
                    var fileName = Path.Combine(tmpDir, "output-cache");
                    File.Exists(fileName).should_be_true();
                    using (var file = File.OpenRead(fileName))
                    {
                        file.Length.should_be_greater_than(0);
                    }
                };

                it["Creates a droplet"] = () =>
                {
                    var fileName = Path.Combine(tmpDir, "droplet");
                    File.Exists(fileName).should_be_true();
                };

                context["the result.json file"] = () =>
                {
                    JObject result = null;

                    act = () =>
                    {
                        result = JObject.Parse(File.ReadAllText(resultFile));
                    };

                    it["includes the start command for 'web' from buildpack release script"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        var webStartCommand = processTypes["web"].Value<string>();
                        webStartCommand.should_be(@"run.bat");
                    };

                    it["doesn't have any other process types"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        processTypes.Count.should_be(1);
                    };

                    it["includes lifecycle metadata fields"] = () =>
                    {
                        result["lifecycle_type"].Value<string>().should_be("buildpack");
                        var metadata = result["lifecycle_metadata"].Value<JObject>();
                        metadata["detected_buildpack"].Value<string>().should_be("Run Buildpack");
                        metadata["buildpack_key"].Value<string>().should_be("run-buildpack");
                    };
                };
            };

            context["given a buildpack with skipdetect and a valid procfile app"] = () =>
            {
                string resultFile = null;

                before = () =>
                {
                    resultFile = Path.Combine(tmpDir, "result.json");
                    CopyDirectory(
                        Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "buildpacks", "run-buildpack"),
                        Path.Combine(buildpacksDir, MD5Hash("run-buildpack"))
                    );

                    CopyDirectory(Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "apps", "run-procfile"), appDir);
                    arguments["-buildpackOrder"] = "run-buildpack";
                    arguments["-skipDetect"] = "true";
                };

                it["Exit code is 0"] = () =>
                {
                    process.ExitCode.should_be(0);
                };

                it["Creates the result.json"] = () =>
                {
                    File.Exists(resultFile).should_be_true();
                };

                context["the result.json file"] = () =>
                {
                    JObject result = null;

                    act = () =>
                    {
                        result = JObject.Parse(File.ReadAllText(resultFile));
                    };

                    it["includes the start command form Procfile"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        var webStartCommand = processTypes["web"].Value<string>();
                        webStartCommand.should_be(@"custom.bat");
                    };

                    it["doesn't have any other process types"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        processTypes.Count.should_be(1);
                    };
                };
            };

            context["given a zip url buildpack and a valid app"] = () =>
            {
                string resultFile = null;
                HttpListener zipServer = null;

                before = () =>
                {
                    var port = GetFreeTcpPort();
                    tmpZip = Path.GetTempFileName();
                    File.Delete(tmpZip);
                    zipServer = StartZipServer("127.0.0.1", port, Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "buildpacks", "run-buildpack"), tmpZip);

                    resultFile = Path.Combine(tmpDir, "result.json");
                     
                    CopyDirectory(Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "apps", "run"), appDir);
                    arguments["-buildpackOrder"] = "\"http://localhost:" + port + "/buildpack.zip\"";
                };

                after = () =>
                {
                    zipServer.Stop();
                };

                it["Exit code is 0"] = () =>
                {
                    process.ExitCode.should_be(0);
                };

                it["Creates the result.json"] = () =>
                {
                    File.Exists(resultFile).should_be_true();
                };

                context["the result.json file"] = () =>
                {
                    JObject result = null;

                    act = () =>
                    {
                        result = JObject.Parse(File.ReadAllText(resultFile));
                    };

                    it["includes the start command form Procfile"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        var webStartCommand = processTypes["web"].Value<string>();
                        webStartCommand.should_be(@"run.bat");
                    };

                    it["doesn't have any other process types"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        processTypes.Count.should_be(1);
                    };

                    it["includes lifecycle metadata fields"] = () =>
                    {
                        result["lifecycle_type"].Value<string>().should_be("buildpack");
                        var metadata = result["lifecycle_metadata"].Value<JObject>();
                        metadata["detected_buildpack"].Value<string>().should_be("Run Buildpack");
                    };
                };
            };

            context["given a git url buildpack and a valid app"] = () =>
            {
                string resultFile = null;

                before = () =>
                {
                    resultFile = Path.Combine(tmpDir, "result.json");

                    CopyDirectory(Path.Combine(currentDirectory, "Builder.Tests", "Fixtures", "apps", "run"), appDir);
                    arguments["-buildpackOrder"] = "https://github.com/stefanschneider/dummy-buildpack#test";
                };

                it["Exit code is 0"] = () =>
                {
                    process.ExitCode.should_be(0);
                };

                it["Creates the result.json"] = () =>
                {
                    File.Exists(resultFile).should_be_true();
                };

                context["the result.json file"] = () =>
                {
                    JObject result = null;

                    act = () =>
                    {
                        result = JObject.Parse(File.ReadAllText(resultFile));
                    };

                    it["includes the start command form Procfile"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        var webStartCommand = processTypes["web"].Value<string>();
                        webStartCommand.should_be(@"dummy");
                    };

                    it["doesn't have any other process types"] = () =>
                    {
                        var processTypes = result["process_types"].Value<JObject>();
                        processTypes.Count.should_be(1);
                    };

                    it["includes lifecycle metadata fields"] = () =>
                    {
                        result["lifecycle_type"].Value<string>().should_be("buildpack");
                        var metadata = result["lifecycle_metadata"].Value<JObject>();
                        metadata["detected_buildpack"].Value<string>().should_be("Dummy");
                    };
                };
            };
        }

        private static void DeleteFileSystemInfo(FileSystemInfo fileSystemInfo)
        {
            var di = fileSystemInfo as DirectoryInfo;
            if (di != null)
            {
                foreach (var ci in di.GetFileSystemInfos())
                {
                    DeleteFileSystemInfo(ci);
                }
            }

            fileSystemInfo.Attributes = FileAttributes.Normal;
            fileSystemInfo.Delete();
        }

        private int GetFreeTcpPort()
        {
            var tcpl = new TcpListener(IPAddress.Any, 0);
            tcpl.Start();

            var freePort = (tcpl.LocalEndpoint as IPEndPoint).Port;
            tcpl.Stop();

            return freePort;
        }

        private static string MD5Hash(string input)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(data).Replace("-", "");
            }
        }

        private static HttpListener StartZipServer(string host, int port, string contentDir, string tmpZip)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(String.Format("http://{0}:{1}/", host, port));
            listener.Start();

            ZipFile.CreateFromDirectory(contentDir, tmpZip, CompressionLevel.Fastest, false);

            var listenThread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    for (; ; )
                    {
                        var httpContext = listener.GetContext();
                        WriteFile(httpContext, tmpZip);
                    }
                }
                catch (Exception)
                {
                    // ignore the exception and exit
                }
            }));
            listenThread.Start();
            return listener;
        }

        private static void WriteFile(HttpListenerContext ctx, string path)
        {
            // Source http://stackoverflow.com/questions/13385633/serving-large-files-with-c-sharp-httplistener
            var response = ctx.Response;
            using (FileStream fs = File.OpenRead(path))
            {
                string filename = Path.GetFileName(path);

                response.ContentLength64 = fs.Length;
                response.SendChunked = false;
                response.ContentType = System.Net.Mime.MediaTypeNames.Application.Zip;
                response.AddHeader("Content-disposition", "attachment; filename=" + filename);

                byte[] buffer = new byte[64 * 1024];
                int read;
                using (BinaryWriter bw = new BinaryWriter(response.OutputStream))
                {
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        bw.Write(buffer, 0, read);
                    }

                    bw.Close();
                }

                response.StatusCode = (int)HttpStatusCode.OK;
                response.StatusDescription = "OK";
                response.OutputStream.Close();
            }
        }

        static public void CopyDirectory(string sourcePath, string destiationPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, destiationPath));
            }

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, destiationPath), true);
            }
        }
    }
}
