// 
// BaseCommand.cs
// 
// Copyright (c) 2021 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

#nullable enable

using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Spectre.Console.Cli;
using Serilog.Events;
using System.ComponentModel;
using Spectre.Console;
using System.IO;

namespace N1QLQueryHarness.Commands
{
    public class BaseCommandSettings : CommandSettings
    {
        [CommandOption("-d|--log-directory")]
        [Description("The (optional) directory to log Couchbase Lite logs to")]
        public string? CouchbaseLogDirectory
        {
            get => Database.Log.File.Config?.Directory;
            set
            {
                if (value != null)
                {
                    Database.Log.File.Config = new LogFileConfiguration(value);
                    Database.Log.File.Level = Couchbase.Lite.Logging.LogLevel.Verbose;
                }
                else
                {
                    Database.Log.File.Config = null;
                    Database.Log.File.Level = Couchbase.Lite.Logging.LogLevel.None;
                }
            }
        }

        [CommandOption("-l|--log-level")]
        [Description("Specifies the level of output to write at")]
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;

        [CommandOption("-w|--working-dir")]
        [Description("The directory to operate in (should be consistent between all subcommands)")]
        public string? WorkingDirectory { get; set; }

        public override ValidationResult Validate()
        {
            return ValidateFilePath(WorkingDirectory);
        }

        protected ValidationResult ValidateFilePath(string? filePath)
        {
            if (filePath == null) {
                return ValidationResult.Success();
            }

            try {
                var fi = new FileInfo(filePath);
                return ValidationResult.Success();
            } catch {

            }

            return ValidationResult.Error("Must be a valid file path");
        }
    }
}