using CommandLine;
using CommandLine.Text;

namespace Builder
{
    public enum OptionBool
    {
        False,
        True
    }

    public class Options
    {
        [Option("buildDir", Required = true)]
        public string BuildDir { get; set; }

        [Option("buildArtifactsCacheDir", Required = false)]
        public string BuildArtifactsCacheDir { get; set; }

        [Option("buildpackOrder", Required = false)]
        public string BuildpackOrder { get; set; }

        [Option("buildpacksDir", Required = false)]
        public string BuildpacksDir { get; set; }

        [Option("outputBuildArtifactsCache", Required = false)]
        public string OutputBuildArtifactsCache { get; set; }

        [Option("outputDroplet", Required = true)]
        public string OutputDroplet { get; set; }

        [Option("outputMetadata", Required = true)]
        public string OutputMetadata { get; set; }

        [Option("skipDetect", Required = false, DefaultValue = OptionBool.False)]
        public OptionBool SkipDetect { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
