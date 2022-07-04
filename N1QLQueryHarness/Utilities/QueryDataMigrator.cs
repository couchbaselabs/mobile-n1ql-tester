// 
// QueryDataMigrator.cs
// 
// Copyright (c) 2020 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using N1QLQueryHarness.Commands;
using N1QLQueryHarness.Utilities.Converters;
using Serilog;

namespace N1QLQueryHarness.Utilities
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class CaseEntry
    {
        #region Properties

        [JsonPropertyName("results")]
        public IReadOnlyCollection<IReadOnlyDictionary<string, object>>? Results { get; set; }

        [JsonPropertyName("statements")] public string? Statements { get; set; }

        #endregion
    }

    internal sealed class QueryDataMigrator
    {
        #region Constants

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        private static readonly IStatementConverter[] Converters =
        {
            new IsStatementConverter(),
            new ToStatementConverter(),
            new TrigDivideConverter(),
            new RoundConverter()
        };

        #endregion

        #region Public Methods

        public async Task<(int used, int total)> Migrate(MigrateCommand parent, string inputJsonPath, string outputJsonPath, string exclusionPath)
        {
            int used, total;
            Log.Information($"Processing {Path.GetRelativePath(parent.InputDirectory!, inputJsonPath)}...");
            IReadOnlyCollection<string>? excludeData;

            await using var inputStream = new FileStream(inputJsonPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var inputData = await JsonSerializer.DeserializeAsync<IList<CaseEntry>>(inputStream)
                .ConfigureAwait(false);
            if (inputData == null) {
                throw new ApplicationException($"Unable to read data at {inputJsonPath}");
            }

            if (File.Exists(exclusionPath)) {
                await using var excludeStream = new FileStream(exclusionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                try {
                    excludeData = await JsonSerializer.DeserializeAsync<IReadOnlyCollection<string>>(excludeStream)
                        .ConfigureAwait(false);
                } catch (Exception) {
                    Log.Error(exclusionPath);
                    throw;
                }
            } else {
                excludeData = new List<string>(0);
            }

            if (excludeData == null) {
                throw new ApplicationException($"Unable to read data at {exclusionPath}");
            }

            if (excludeData.Contains("*")) {
                return (0, inputData.Count); // Skip the entire file
            }

            used = total = inputData.Count;
            for (int i = inputData.Count - 1; i >= 0; i--) {
                var entry = inputData[i];
                if (excludeData.Contains(entry.Statements) || entry.Statements == null || entry.Results == null) {
                    used--;
                    inputData.RemoveAt(i);
                } else {
                    foreach (var c in Converters) {
                        if (c.ShouldConvert(entry.Statements)) {
                            var before = entry.Statements;
                            entry.Statements = c.Convert(entry.Statements);
                            Log.Information($"Converted {before} to {entry.Statements}...");
                        }
                    }
                }
            }

            await using var outputStream =
                new FileStream(outputJsonPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(outputStream, inputData, JsonOptions).ConfigureAwait(false);
            Log.Information($"Finished {Path.GetRelativePath(parent.InputDirectory!, inputJsonPath)} after processing {inputData.Count} entries!");
            return (used, total);
        }

        #endregion
    }
}