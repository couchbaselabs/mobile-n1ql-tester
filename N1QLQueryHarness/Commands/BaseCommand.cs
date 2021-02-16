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
using McMaster.Extensions.CommandLineUtils;
using N1QLQueryHarness.Utilities;
using LogLevel = N1QLQueryHarness.Utilities.LogLevel;

namespace N1QLQueryHarness.Commands
{
    [HelpOption("--help")]
    public abstract class BaseCommand
    {
        #region Properties

        [Option(Description = "The (optional) directory to log Couchbase Lite logs to")]
        public string? CouchbaseLogDirectory
        {
            get => Database.Log.File.Config.Directory;
            set
            {
                if (value != null) {
                    Database.Log.File.Config = new LogFileConfiguration(value);
                    Database.Log.File.Level = Couchbase.Lite.Logging.LogLevel.Verbose;
                } else {
                    Database.Log.File.Config = null;
                    Database.Log.File.Level = Couchbase.Lite.Logging.LogLevel.None;
                }
            }
        }

        [Option(Description = "Specifies the level of output to write at (normal|detailed|verbose)")]
        public (bool HasValue, LogLevel Level) Trace
        {
            get => (ColorConsole.Level.HasValue, ColorConsole.Level ?? LogLevel.Normal);
            set => ColorConsole.Level = value.HasValue ? value.Level : null;
        }

        #endregion

        #region Constructors

        protected BaseCommand()
        {
            Database.Log.Console.Level = Couchbase.Lite.Logging.LogLevel.None;
        }

        #endregion
    }
}