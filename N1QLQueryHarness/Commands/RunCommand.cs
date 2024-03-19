// 
// RunCommand.cs
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using N1QLQueryHarness.DynamicInterface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace N1QLQueryHarness.Commands
{
    internal sealed class JsonEqualityComparer : IEqualityComparer<IReadOnlyDictionary<string, object>>
    {
        #region Variables

        private readonly bool _ignoreOrder;

        #endregion

        #region Constructors

        public JsonEqualityComparer(bool ignoreOrder)
        {
            _ignoreOrder = ignoreOrder;
        }

        #endregion

        #region Public Methods

        public bool RecursiveEquals(object? x, object? y)
        {
            if (Object.ReferenceEquals(x, null) && Object.ReferenceEquals(y, null)) {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null)) {
                return false;
            }

            if (x is JObject xjo) {
                x = xjo.ToObject<Dictionary<string, object>>();
            } else if (x is JArray xja) {
                x = xja.ToObject<List<object>>();
            }

            if (y is JObject yjo) {
                y = yjo.ToObject<Dictionary<string, object>>();
            } else if (y is JArray yja) {
                y = yja.ToObject<List<object>>();
            }

            if (x is IDictionary<string, object> xd) {
                if (!(y is IDictionary<string, object> yd)) {
                    return false;
                }

                if (xd.Count != yd.Count) {
                    return false;
                }

                foreach (var key in xd.Keys) {
                    if (!yd.ContainsKey(key) || !RecursiveEquals(xd[key], yd[key])) {
                        return false;
                    }
                }

                return true;
            }

            if (y is IDictionary<string, object>) {
                return false;
            }

            if (x is IList xl) {
                if (!(y is IList yl)) {
                    return false;
                }

                if (xl.Count != yl.Count) {
                    return false;
                }

                var usedIndexes = new HashSet<int>();
                for (int i = 0; i < xl.Count; i++) {
                    if (_ignoreOrder) {
                        var before = usedIndexes.Count;
                        for (int j = 0; j < yl.Count; j++) {
                            if (usedIndexes.Contains(j)) continue;
                            if (RecursiveEquals(xl[i], yl[j])) {
                                usedIndexes.Add(j);
                                break;
                            }
                        }

                        if (usedIndexes.Count == before) {
                            return false;
                        }
                    } else {
                        if (!RecursiveEquals(xl[i], yl[i])) {
                            return false;
                        }
                    }
                }

                return true;
            }

            if (y is IList) {
                return false;
            }

            return ValueEquals(x, y);
        }

        #endregion

        #region Private Methods

        private bool ValueEquals(object x, object y)
        {
            if (x.GetType().IsPrimitive && y.GetType().IsPrimitive) {
                // Floating point check the actual result is within 0.1% of expected to account
                // for differences between server and mobile
                var left = (x as IConvertible)!.ToDouble(CultureInfo.InvariantCulture);
                var right = (y as IConvertible)!.ToDouble(CultureInfo.InvariantCulture);
                var diff = Math.Abs(left - right);
                return left == 0.0 ? diff < Double.Epsilon : (diff / left) < 0.001;
            }

            if (x.GetType() != y.GetType()) {
                return false;
            }

            return x.Equals(y);
        }

        #endregion

        #region IEqualityComparer<IReadOnlyDictionary<string,object>>

        public bool Equals(IReadOnlyDictionary<string, object>? x, IReadOnlyDictionary<string, object>? y)
        {
            return RecursiveEquals(x, y);
        }

        public int GetHashCode(IReadOnlyDictionary<string, object> obj)
        {
            // Doesn't really matter here
            return obj.GetHashCode();
        }

        #endregion
    }

    internal sealed class RunCommandSettings : BaseCommandSettings
    {
        [CommandOption("-o|--ignore-order")]
        [Description("Considers result which only differ by ordering equal")]
        public bool IgnoreOrdering { get; set; }

        [CommandOption("-j|--json-report")]
        [Description("If specified, writes a JSON encoded report of the results to the given filename")]
        public string? JsonReportFilename { get; set; }

        [CommandOption("--single-thread")]
        [Description("Run single threaded for debugging")]
        public bool SingleThreaded { get; set; }
    }

    internal sealed class RunCommand : Command<RunCommandSettings>
    {
        #region Constants

        private static readonly Regex CollectionNameRegex = new("FROM ([a-z]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region Variables

        private LiteCoreFunctions? _lc;
        private RunResult _result = new();
        private RunCommandSettings _settings = new();

        #endregion

        #region Public Methods

        public static void AddToApplication(IConfigurator config)
        {
            config.AddCommand<RunCommand>("run")
                .WithDescription("Executes the prepared query data and checks the results");
        }

        #endregion

        #region Private Methods

        private async Task CheckQueries(string dbDirectory, string jsonFilePath)
        {
            using var fin = File.OpenRead(jsonFilePath);
            using var jsonReader = new JsonTextReader(new StreamReader(fin, Encoding.UTF8));

            var serializer = JsonSerializer.CreateDefault();
            var json = serializer.Deserialize<IReadOnlyList<TestQuery>>(jsonReader);
            foreach (var query in json) {
                if (_settings.SingleThreaded) {
                    CheckQuerySync(dbDirectory, query);
                } else {
                    await CheckQueryAsync(dbDirectory, query);
                }
            }
        }

        private Task CheckQueryAsync(string dbDirectory, TestQuery query)
        {
            return Task.Run(() => { CheckQuerySync(dbDirectory, query); });
        }

        private void CheckQuerySets(string directory)
        {
            var tasks = new List<Task>();
            foreach (var json in Directory.EnumerateFiles(directory, "*.json")) {
                tasks.Add(CheckQueries(directory, json));
            }

            Task.WaitAll(tasks.ToArray());
        }

        private unsafe void CheckQuerySync(string dbDirectory, TestQuery query)
        {
            C4Database* db = OpenDatabase(dbDirectory, "data");
            string queryStatements;
            var collNameMatch = CollectionNameRegex.Match(query.Statements);
            if (!collNameMatch.Success) {
                queryStatements = query.Statements + " FROM _";
            } else {
                queryStatements = query.Statements;
            }

            C4Error err = new();
            C4Query* c4query = null;
            C4QueryEnumerator* e = null;
            try {
                c4query = CreateQuery(db, queryStatements);
                if (c4query == null) {
                    return;
                }

                e = _lc!.c4query_run(c4query, new Slice(), ref err);
                if (e == null) {
                    RecordError(queryStatements, err);
                    return;
                }

                var results = new List<IReadOnlyDictionary<string, object>>();
                while (_lc!.c4queryenum_next(e, ref err)) {
                    var result = DeserializeResultRow(c4query, e);
                    results.Add(result);
                }

                if (err.code != 0) {
                    RecordError(queryStatements, err);
                    return;
                }

                if (!ResultsAreEqual(query.Results, results)) {
                    RecordFail(queryStatements, query.Results, results);
                } else {
                    RecordPass(queryStatements);
                }
            } finally {
                _lc!.c4db_release(db);
                _lc!.c4query_release(c4query);
                _lc!.c4queryenum_release(e);
            }
        }

        private unsafe C4Query* CreateQuery(C4Database* db, string statements)
        {
            C4Error err = new();
            using var queryStr = new PinnedString(statements);
            var errorPos = -1;
            var c4query = _lc!.c4query_new2(db, queryStr.AsSlice(), ref errorPos, ref err);
            if (c4query == null) {
                Log.Error("Failed to create query for '{0}'...", statements);
                var startOffset = Math.Min(errorPos, 15);
                Log.Error("\t...{0}", _lc!.c4error_getDescription(err));
                _result.ErrorResults.Add(new ErrorResult
                    {Query = statements, Message = _lc!.c4error_getDescription(err)});
                _result.BumpError();
                if (errorPos >= 0) {
                    Console.WriteLine();
                    var start = errorPos - startOffset;
                    Log.Error("\t...{0}", statements.Substring(start, Math.Min(30, statements.Length - start)));
                    Log.Error("\t...{0}^", new string(' ', startOffset + 3));
                }
            }

            return c4query;
        }

        private unsafe IReadOnlyDictionary<string, object> DeserializeResultRow(C4Query* query, C4QueryEnumerator* e)
        {
            var retVal = new Dictionary<string, object>();
            var count = _lc!.FLArrayIterator_GetCount(&e->columns);
            if (_lc!.FLArrayIterator_GetCount(&e->columns) == 0) {
                return retVal;
            }

            int columnIndex = 0;
            do {
                if ((e->missingColumns & (1UL << columnIndex)) == 0) {
                    var title = _lc!.c4query_columnTitle(query, (uint) columnIndex).CreateString();
                    var flValue = _lc!.FLArrayIterator_GetValue(&e->columns);
                    var json = _lc!.FLValue_ToJSON(flValue);
                    retVal[title] = JsonConvert.DeserializeObject(json);
                }

                columnIndex++;
            } while (_lc!.FLArrayIterator_Next(&e->columns));

            return retVal;
        }

        private string LibraryFilename()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return "LiteCore.dll";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return "libLiteCore.dylib";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return "libLiteCore.so";
            }

            throw new NotSupportedException($"Invalid platform {RuntimeInformation.OSDescription}");
        }

        private unsafe C4Database* OpenDatabase(string dbDirectory, string dbName)
        {
            var err = new C4Error();
            using var parentDir = new PinnedString(dbDirectory);
            using var dbName_ = new PinnedString(dbName);
            var config = new C4DatabaseConfig2
            {
                flags = 0,
                parentDirectory = parentDir.AsSlice()
            };

            var db = _lc!.c4db_openNamed(dbName_.AsSlice(), &config, ref err);
            if (db == null) {
                Log.Fatal("Unable to open database '{0}' in {1}: ({2})", dbName, dbDirectory, _lc!.c4error_getDescription(err));
            }

            return db;
        }

        private void RecordError(string query, C4Error error)
        {
            var message = _lc!.c4error_getDescription(error);
            _result.BumpError();
            _result.ErrorResults.Add(new ErrorResult {Query = query, Message = message});
            AnsiConsole.MarkupLine($"[red][[ERROR]] {query.EscapeMarkup()}: {message}[/]");
        }

        private void RecordFail(string query, IReadOnlyList<IReadOnlyDictionary<string, object>> expected,
            IReadOnlyList<IReadOnlyDictionary<string, object>> actual)
        {
            _result.BumpFail();
            _result.FailResults.Add(new FailResult
            {
                Actual = actual,
                Expected = expected,
                Query = query
            });

            AnsiConsole.MarkupLine($"[yellow][[FAIL]] {query.EscapeMarkup()}[/]");
            AnsiConsole.MarkupLine("[yellow]Expected:[/]");
            AnsiConsole.MarkupLine($"[yellow]{JsonConvert.SerializeObject(expected, Formatting.Indented).EscapeMarkup()}[/]");
            AnsiConsole.MarkupLine("[yellow]Actual:[/]");
            AnsiConsole.MarkupLine($"[yellow]{JsonConvert.SerializeObject(actual, Formatting.Indented).EscapeMarkup()}[/]");
        }

        private void RecordPass(string query)
        {
            _result.BumpPass();
            _result.PassResults.Add(query);
            AnsiConsole.MarkupLine("[green][[PASS]] {0}[/]", query.EscapeMarkup());
        }

        private bool ResultsAreEqual(IReadOnlyList<IReadOnlyDictionary<string, object>> expected,
            IReadOnlyList<IReadOnlyDictionary<string, object>> actual)
        {
            if (expected.Count != actual.Count) {
                return false;
            }

            var comparer = new JsonEqualityComparer(_settings.IgnoreOrdering);
            return !_settings.IgnoreOrdering
                ? expected.SequenceEqual(actual, comparer)
                : comparer.RecursiveEquals(expected, actual);
        }

        #endregion

        #region Command Implementation

        public unsafe override int Execute([NotNull] CommandContext context, [NotNull] RunCommandSettings settings)
        {
            _settings = settings;
            var workingDir = _settings.WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var librarySubDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "bin" : "lib";
            var lcPath = Path.Combine(workingDir, "lib", librarySubDir, LibraryFilename());
            try {
                _lc = new LiteCoreFunctions(lcPath);
                _lc!.c4log_setCallbackLevel(5);
            } catch (FileNotFoundException) {
                return -1;
            }

            var dataPath = Path.Combine(workingDir, "out");
            foreach (var dir in Directory.EnumerateDirectories(dataPath)) {
                CheckQuerySets(dir);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]PASS: {_result.PassCount} [/]");
            AnsiConsole.MarkupLine($"[yellow]FAIL: {_result.FailCount} [/]");
            AnsiConsole.MarkupLine($"[red]ERROR: {_result.ErrorCount} [/]");
            if (_settings.JsonReportFilename != null) {
                using var fout = File.OpenWrite(Path.Combine(workingDir, _settings.JsonReportFilename));
                using var jout = new JsonTextWriter(new StreamWriter(fout, Encoding.UTF8));
                JsonSerializer.CreateDefault(new JsonSerializerSettings { Formatting = Formatting.Indented })
                    .Serialize(jout, _result);
            }

            return _result.ErrorCount + _result.FailCount;
        }

        #endregion
    }
}