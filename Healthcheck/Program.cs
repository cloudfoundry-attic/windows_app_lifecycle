using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Healthcheck
{
    class Program
    {
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
            var jsonInstancePorts = Environment.GetEnvironmentVariable("CF_INSTANCE_PORTS");
            if (jsonInstancePorts == null)
            {
                throw new Exception("CF_INSTANCE_PORTS is not defined");
            }

            var externalPort = getExternalPort(jsonInstancePorts, options.Port);
            if (externalPort == 0)
            {
                Console.WriteLine("healthcheck failed, port mapping not found for " + options.Port.ToString() + " in " + jsonInstancePorts);
                Environment.Exit(1);
            }

            if (options.Network != "tcp")
            {
                Console.WriteLine("healthcheck failed, network type '{0}' not supported", options.Network);
            }

            int port = externalPort;
            TimeSpan timeout = ParseTimeoutFromArgs(options.Timeout);

            var instanceIp = Environment.GetEnvironmentVariable("CF_INSTANCE_IP");


            if (options.UriSuffix.Length > 0)
            {
                httpHealthCheck(instanceIp, port, options.UriSuffix, timeout);
            }
            else
            {
                tcpHealthCheck(instanceIp, port, timeout);
            }

            System.Console.WriteLine("healthcheck failed");
            System.Environment.Exit(1);
        }

        private static void tcpHealthCheck(string address, int port, TimeSpan timeout)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    IAsyncResult connectResult = tcpClient.BeginConnect(address, port, null, null);
                    if (!connectResult.AsyncWaitHandle.WaitOne(timeout, false))
                    {
                        tcpClient.EndConnect(connectResult);
                        return;
                    }

                    if (tcpClient.Connected)
                    {
                        tcpClient.Close();

                        Console.WriteLine("healthcheck passed");
                        Environment.Exit(0);
                    }
                }
            }
            catch { }
        }

        private static void httpHealthCheck(string address, int port, string uriSuffix, TimeSpan timeout)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var task = httpClient.GetAsync(String.Format("http://{0}:{1}{2}", address, port, uriSuffix));
                    if (task.Wait(timeout))
                    {
                        if (task.Result.IsSuccessStatusCode)
                        {
                            Console.WriteLine("healthcheck passed");
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.Error.WriteLine("Got error response: " + task.Result.Content.ReadAsStringAsync().Result);
                        }
                    }
                    else
                    {
                        Console.WriteLine("waiting for process to start up");
                    }

                }
            }
            catch { }
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

        private static int getExternalPort(string jsonInstancePorts, int internalPort)
        {
            var instancePorts = JsonConvert.DeserializeObject<List<Dictionary<string, int>>>(jsonInstancePorts);
            var match = instancePorts.FirstOrDefault(x => x["internal"] == internalPort);
            if (match == null)
            {
                return 0;
            }
            return match["external"];
        }

        private static TimeSpan ParseTimeoutFromArgs(string timeout)
        {
            if (timeout.EndsWith("ms"))
            {
                var milliseconds = int.Parse(timeout.Substring(0, timeout.Length - 2));
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            if (timeout.EndsWith("s"))
            {
                var seconds = int.Parse(timeout.Substring(0, timeout.Length - 1));
                return TimeSpan.FromSeconds(seconds);
            }

            throw new ArgumentException(String.Format("Unable to parse duration: '{0}'", timeout), "timeout");
        }
    }
}
