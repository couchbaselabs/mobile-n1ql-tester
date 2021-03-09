// 
// MigrateCommand.cs
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

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Couchbase.Lite;
using McMaster.Extensions.CommandLineUtils;
using N1QLQueryHarness.Utilities;

namespace N1QLQueryHarness.Commands
{
    [Command(Description = "Migrate server query test data to mobile format")]
    internal sealed class MigrateCommand : BaseCommand
    {
        #region Variables

        private readonly DatabaseGenerator _databaseGenerator = new();
        private readonly QueryDataMigrator _dataMigrator = new();
        private int _used, _total;
        private string? _workingDir;

        #endregion

        #region Properties

        public string InputDirectory
        {
            get
            {
                _workingDir ??= WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                return Path.Combine(_workingDir, "data", "test", "filestore", "test_cases");
            }
        }

        public string OutputDirectory
        {
            get
            {
                _workingDir ??= WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                return Path.Combine(_workingDir, "out");
            }
        }

        [Option(Description = "The directory to operate in (should be consistent between all subcommands)")]
        [LegalFilePath]
        public string? WorkingDirectory { get; set; }

        #endregion

        #region Private Methods

        // Called via reflection
        // ReSharper disable once UnusedMember.Local
        private async Task OnExecute()
        {
            var tasks = Directory.EnumerateDirectories(InputDirectory!).Select(ProcessDirectory);
            await Task.WhenAll(tasks);
            Console.WriteLine();
            Console.WriteLine($"== Processed {_total} queries, of which {_used} were used ==");
        }

        private async Task ProcessDirectory(string path)
        {
            foreach (var jsonFile in Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly)) {
                var dirName = new DirectoryInfo(jsonFile).Parent!.Name;
                var outputDirectory = Path.Combine(OutputDirectory!, dirName);
                Directory.CreateDirectory(outputDirectory);

                if (jsonFile.EndsWith("insert.json")) {
                    _databaseGenerator.Generate(this, jsonFile, outputDirectory);
                } else {
                    var fileName = Path.GetFileName(jsonFile);
                    var exclusionPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                        "filters", dirName, fileName);
                    var result =
                        await _dataMigrator.Migrate(this, jsonFile, Path.Combine(outputDirectory, fileName),
                            exclusionPath);
                    _used += result.used;
                    _total += result.total;
                }
            }

            var dbConfig = new DatabaseConfiguration
            {
                Directory = OutputDirectory
            };

            if (!Database.Exists("scratch", OutputDirectory!)) {
                using var db = new Database("scratch", dbConfig);
                using var doc = new MutableDocument();
                doc.SetString("foo", "bar");
                db.Save(doc);
            }
        }

        #endregion
    }
}