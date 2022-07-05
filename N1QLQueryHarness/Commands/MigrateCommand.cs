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
using Spectre.Console.Cli;
using N1QLQueryHarness.Utilities;
using Serilog;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;

namespace N1QLQueryHarness.Commands
{
    internal sealed class MigrateCommand : AsyncCommand<BaseCommandSettings>
    {
        #region Variables

        private readonly DatabaseGenerator _databaseGenerator = new();
        private readonly QueryDataMigrator _dataMigrator = new();
        private int _used, _total;
        private BaseCommandSettings _settings = new();

        #endregion

        #region Properties

        public string InputDirectory
        {
            get {
                _settings!.WorkingDirectory ??= _settings.WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                return Path.Combine(_settings.WorkingDirectory, "data", "test", "filestore", "test_cases");
            }
        }

        public string OutputDirectory
        {
            get {
                _settings!.WorkingDirectory ??= _settings.WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                return Path.Combine(_settings.WorkingDirectory, "out");
            }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, BaseCommandSettings settings)
        {
            _settings = settings;
            var tasks = Directory.EnumerateDirectories(InputDirectory!).Select(ProcessDirectory);
            await Task.WhenAll(tasks);
            Log.Information("== Processed {0} queries, of which {1} were used ==", _total, _used);
            return 0;
        }

        #endregion

        #region Public Methods

        public static void AddToApplication(IConfigurator config)
        {
            config.AddCommand<MigrateCommand>("migrate")
                .WithDescription("Migrate server query test data to mobile format");
        }

        #endregion

        #region Private Methods

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