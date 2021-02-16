// 
// Program.cs
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

using System.Reflection;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using N1QLQueryHarness.Commands;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace N1QLQueryHarness
{
    // ReSharper disable once ClassNeverInstantiated.Global

    [VersionOptionFromMember("--version", MemberName = nameof(GetVersion))]
    [HelpOption("--help")]
    [Command(Description = "A suite of tools for running N1QL tests using Couchbase Server test data")]
    [Subcommand(
        typeof(MigrateCommand),
        typeof(PrepareCommand),
        typeof(RunCommand)
    )]
    public sealed class Program
    {
        #region Private Methods

        private static string GetVersion()
        {
            var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "<no version>";
            return $"{nameof(N1QLQueryHarness)} v{version}";
        }

        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 0) {
                return await CommandLineApplication.ExecuteAsync<Program>("--help");
            } 
            
            return await CommandLineApplication.ExecuteAsync<Program>(args);
        }

        #endregion
    }
}