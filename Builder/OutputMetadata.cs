using System;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using System.Collections.Generic;

namespace Builder
{
    // Structure validation here:
    // https://github.com/cloudfoundry/cloud_controller_ng/blob/f418b7cc273a410b39938fc9a46d9d94f591d887/lib/cloud_controller/diego/buildpack/staging_completion_handler.rb#L11-L25
    public class OutputMetadata
    {
        [JsonProperty("lifecycle_type")]
        public string LifecycleType { get; set; }

        [JsonProperty("lifecycle_metadata")]
        public LifecycleMetadata LifecycleMetadata { get; set; }

        [JsonProperty("process_types")]
        public Dictionary<string, string> ProcessTypes { get; set; }

        [JsonProperty("execution_metadata")]
        public string ExecutionMetadata { get; set; }
    }

    public class LifecycleMetadata
    {
        [JsonProperty("buildpack_key")]
        public string BuildpackKey { get; set; }

        [JsonProperty("detected_buildpack")]
        public string DetectedBuildpack { get; set; }
    }

    public class ReleaseInfo
    {
        [YamlMember(Alias = "default_process_types")]
        public Dictionary<string, string> defaultProcessType { get; set; }
    }
}