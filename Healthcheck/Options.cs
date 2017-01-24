using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace Healthcheck
{
    public enum OptionBool
    {
        False,
        True
    }

    public class Options
    {
        [Option("network", Required = false, DefaultValue = "tcp", HelpText = "network type to dial with (only 'tcp' is supported)")]
        public string Network { get; set; }

        [Option("uri", Required = false, DefaultValue = "")]
        public string UriSuffix { get; set; }

        [Option("port", Required = false, DefaultValue = 8080)]
        public int Port { get; set; }

        [Option("timeout", Required = false, DefaultValue = "10s")]
        public string Timeout { get; set; }


        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
