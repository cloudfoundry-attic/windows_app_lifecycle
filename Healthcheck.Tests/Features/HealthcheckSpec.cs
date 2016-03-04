using NSpec;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Healthcheck.Tests.Specs
{
    class HealthcheckSpecs : nspec
    {
        public void describe_()
        {
            int externalPort = -1;
            Process process = null;
            string processOutputData = null;
            string processErrorData = null;
            string arguments = "";

            before = () =>
            {
                externalPort = GetFreeTcpPort();
                arguments = "";
            };

            act = () =>
            {
                arguments += "-port=8080 ";

                var workingDir = Path.GetFullPath(Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().CodeBase, "..", "..", "..", "..", "Healthcheck", "bin").Replace("file:///", ""));
                process = new Process
                {
                    StartInfo =
                    {
                        FileName = Path.Combine(workingDir, "Healthcheck.exe"),
                        Arguments = arguments,
                        WorkingDirectory = workingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };

                process.StartInfo.EnvironmentVariables["CF_INSTANCE_PORTS"] =
                    String.Format("[{{\"external\": {0}, \"internal\": 8080}}]", externalPort);
                process.StartInfo.EnvironmentVariables["CF_INSTANCE_IP"] = "127.0.0.1";

                process.Start();
                processOutputData = process.StandardOutput.ReadToEnd();
                processErrorData = process.StandardError.ReadToEnd();
                process.WaitForExit();
            };

            describe["when the HTTP server is returning non success status code"] = () =>
            {
                HttpListener httpListener = null;
                var stacktrace = "BOOOOOOM";
                before = () =>
                {
                    arguments += "-uri=/ ";
                    httpListener = startHttpServer("*", externalPort, 500, stacktrace);
                };
                after = () => httpListener.Stop();

                it["exits 1 and logs the stack trace"] = () =>
                {
                    processOutputData.should_contain("healthcheck failed\r\n");
                    processErrorData.should_contain(stacktrace);
                    process.ExitCode.should_be(1);
                };
            };

            describe["when the HTTP server is timing out"] = () =>
            {
                HttpListener httpListener = null;

                before = () =>
                {
                    arguments += "-uri=/ ";
                    arguments += "-timeout=100ms ";
                    httpListener = startHttpServer("*", externalPort, 200, "ok", TimeSpan.FromMilliseconds(500));
                };
                after = () => httpListener.Stop();

                it["exits 1 and logs the stack trace"] = () =>
                {
                    processOutputData.should_contain("healthcheck failed");
                    processOutputData.should_contain("waiting for process to start up");
                    process.ExitCode.should_be(1);
                };
            };

            describe["when the HTTP server is returning a success status code"] = () =>
            {
                HttpListener httpListener = null;
                before = () =>
                {
                    arguments += "-uri=/ ";
                    httpListener = startHttpServer("*", externalPort);
                };
                after = () => httpListener.Stop();

                it["exits 0 and logs it succeeded"] = () =>
                {
                    processOutputData.should_be("healthcheck passed\r\n");
                    process.ExitCode.should_be(0);
                };
            };

            describe["when the address is not listening and using TCP check"] = () =>
            {
                it["exits 1 and logs it failed"] = () =>
                {
                    processOutputData.should_contain("healthcheck failed\r\n");
                    process.ExitCode.should_be(1);
                };
            };

            describe["when the address is not listening and using HTTP check"] = () =>
            {
                before = () =>
                {
                    arguments += "-uri=/ ";
                };

                it["exits 1 and logs it failed"] = () =>
                {
                    processOutputData.should_contain("healthcheck failed\r\n");
                    process.ExitCode.should_be(1);
                };
            };

            describe["when the TCP server is listening"] = () =>
            {
                TcpListener tcpListener = null;
                before = () => tcpListener = startTcpServer(IPAddress.Any, externalPort);
                after = () => tcpListener.Stop();

                it["exits 0 and logs it succeeded"] = () =>
                {
                    processOutputData.should_contain("healthcheck passed\r\n");
                    process.ExitCode.should_be(0);
                };
            };

            describe["when the network argument is invalid"] = () =>
            {
                before = () =>
                {
                    arguments += "-network=unix ";
                };

                it["exits 1 and logs it failed"] = () =>
                {
                    processOutputData.should_contain("'unix' not supported");
                    process.ExitCode.should_be(1);
                };
            };
        }

        private int GetFreeTcpPort()
        {
            var tcpl = new TcpListener(IPAddress.Any, 0);
            tcpl.Start();

            var freePort = (tcpl.LocalEndpoint as IPEndPoint).Port;
            tcpl.Stop();

            return freePort;
        }

        private HttpListener startHttpServer(string host, int port, int statusCode = 200, string content = "Hello!", TimeSpan? wait = null)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(String.Format("http://{0}:{1}/", host, port));
            listener.Start();
            var listenThread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    for (; ; )
                    {
                        var httpContext = listener.GetContext();
                        httpContext.Response.StatusCode = statusCode;
                        var resp = UTF8Encoding.UTF8.GetBytes(content);
                        httpContext.Response.OutputStream.Write(resp, 0, resp.Length);
                        if (wait.HasValue)
                        {
                            Thread.Sleep(wait.Value);
                        }

                        httpContext.Response.OutputStream.Close();
                    }
                }
                catch (Exception e)
                {
                    // ignore the exception and exit
                }
            }));
            listenThread.Start();
            return listener;
        }

        private TcpListener startTcpServer(IPAddress host, int port)
        {
            var tcpListener = new TcpListener(host, port);
            var listenThread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    tcpListener.Start();
                    tcpListener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    // ignore the exception
                }
            }));
            listenThread.Start();
            return tcpListener;
        }
    }
}