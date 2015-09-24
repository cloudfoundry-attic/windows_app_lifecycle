using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Builder
{
    public class OutputMetadata
    {
        public ExecutionMetadata ExecutionMetadata { get; set; }

        [JsonProperty("execution_metadata")]
        public string execution_metadata
        {
            get { return JsonConvert.SerializeObject(ExecutionMetadata); }
        }

        [JsonProperty("process_types")]
        public ProcessTypes ProcessTypes {
            get
            {
                return new ProcessTypes()
                {
                    Web =
                        (ExecutionMetadata.StartCommand + " " + String.Join(" ", ExecutionMetadata.StartCommandArgs))
                            .Trim(),
                };
            }
        }

        [JsonProperty("lifecycle_metadata")]
        public string LifecycleMetadata{ 
        {
            get { return new LifecycleMetadata() }
        }
    }

    public class ProcessTypes 
    {
        [JsonProperty("web")]
        public string Web { get; set; }
    }

    public class ExecutionMetadata
    {
        public ExecutionMetadata()
        {
            StartCommand = "";
            StartCommandArgs = new string[] { };
        }

        [JsonProperty("start_command")]
        public string StartCommand
        {
            get;
            set;
        }

        [JsonProperty("start_command_args")]
        public string[] StartCommandArgs
        {
            get;
            set;
        }
    }

    public class LifecycleMetadata 
    {
        public LifecycleMetadata()
        {
            DetectedBuildpack = "windows";
            BuildpackKey = ""; 
        }

        [JsonProperty("detected_buildpack")]
        public string DetectedBuildpack 
        {
            get;
        }

        [JsonProperty("buildpack_key")]
        public string BuildpackKey
        {
            get;
        }
    }
}
