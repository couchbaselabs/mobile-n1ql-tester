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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using N1QLQueryHarness.DynamicInterface;
using N1QLQueryHarness.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    [Command(Description = "Executes the prepared query data and checks the results")]
    internal sealed class RunCommand : BaseCommand
    {
        #region Constants

        private static readonly Regex DbNameRegex = new("FROM ([a-z]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region Variables

        private LiteCoreFunctions? _lc;
        private RunResult _result = new();
        private unsafe C4Database* _scratchDb;

        #endregion

        #region Properties

        [Option("-o|--ignore-order", CommandOptionType.NoValue,
            Description = "Considers result which only differ by ordering equal")]
        public bool IgnoreOrdering { get; set; }

        [Option("-j|--json-report",
            Description = "If specified, writes a JSON encoded report of the results to the given filename")]
        public string? JsonReportFilename { get; set; }

        [Option(CommandOptionType.NoValue, Description = "Run single threaded for debugging")]
        public bool SingleThreaded { get; set; }

        [Option(LongName = "wd",
            Description = "The directory to operate in (should be consistent between all subcommands)")]
        [LegalFilePath]
        public string? WorkingDirectory { get; set; }

        #endregion

        #region Private Methods

        private async Task CheckQueries(string dbDirectory, string jsonFilePath)
        {
            using var fin = File.OpenRead(jsonFilePath);
            using var jsonReader = new JsonTextReader(new StreamReader(fin, Encoding.UTF8));

            var serializer = JsonSerializer.CreateDefault();
            var json = serializer.Deserialize<IReadOnlyList<TestQuery>>(jsonReader);
            foreach (var query in json) {
                if (SingleThreaded) {
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
            C4Database* db;
            bool isScratch = false;
            string queryStatements;
            var dbNameMatch = DbNameRegex.Match(query.Statements);
            if (!dbNameMatch.Success) {
                db = _scratchDb;
                isScratch = true;
                queryStatements = query.Statements + " FROM scratch";
            } else {
                var dbName = dbNameMatch.Groups[1].Value;
                db = OpenDatabase(dbDirectory, dbName);
                if (db == null) {
                    return;
                }

                queryStatements = query.Statements;
            }

            C4Error err = new();
            C4Query* c4query = null;
            C4QueryEnumerator* e = null;
            try {
                C4QueryOptions opts = new();
                c4query = CreateQuery(db, queryStatements);
                if (c4query == null) {
                    return;
                }

                e = _lc!.c4query_run(c4query, &opts, new Slice(), ref err);
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
                if (!isScratch) {
                    _lc!.c4db_release(db);
                }

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
                using var group = ColorConsole.BeginGroup();
                ColorConsole.ForceWriteLine(
                    $"Failed to create query for '{statements}'...");
                var startOffset = Math.Min(errorPos, 15);
                ColorConsole.ForceWriteLine($"\t...{_lc!.c4error_getDescription(err)}");
                _result.ErrorResults.Add(new ErrorResult
                    {Query = statements, Message = _lc!.c4error_getDescription(err)});
                _result.ErrorCount++;
                _result.Total++;
                if (errorPos >= 0) {
                    Console.WriteLine();
                    var start = errorPos - startOffset;
                    ColorConsole.ForceWriteLine(
                        $"\t...{statements.Substring(start, Math.Min(30, statements.Length - start))}...");
                    ColorConsole.ForceWriteLine($"\t{new string(' ', startOffset + 3)}^");
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

        // Called via reflection
        // ReSharper disable once UnusedMember.Local
        private unsafe int OnExecute()
        {
            var workingDir = WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var lcPath = Path.Combine(workingDir, "lib", LibraryFilename());
            try {
                _lc = new LiteCoreFunctions(lcPath);
                _lc!.c4log_setCallbackLevel(5);
            } catch (FileNotFoundException) {
                return -1;
            }

            try {
                var dataPath = Path.Combine(workingDir, "out");
                _scratchDb = OpenDatabase(dataPath, "scratch");
                foreach (var dir in Directory.EnumerateDirectories(dataPath)) {
                    CheckQuerySets(dir);
                }
            } finally {
                _lc!.c4db_release(_scratchDb);
                _scratchDb = null;
            }

            using var g = ColorConsole.BeginGroup();
            Console.WriteLine();
            ColorConsole.ForceWrite($"PASS: {_result.PassCount} ", ConsoleColor.Green);
            ColorConsole.ForceWrite($"FAIL: {_result.FailCount} ", ColorConsole.WarnColor);
            ColorConsole.ForceWrite($"ERROR: {_result.ErrorCount}");
            if (JsonReportFilename != null) {
                using var fout = File.OpenWrite(Path.Combine(workingDir, JsonReportFilename));
                using var jout = new JsonTextWriter(new StreamWriter(fout, Encoding.UTF8));
                JsonSerializer.CreateDefault(new JsonSerializerSettings {Formatting = Formatting.Indented})
                    .Serialize(jout, _result);
            }

            return _result.ErrorCount + _result.FailCount;
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
                ColorConsole.ForceWriteLine(
                    $"Unable to open database '{dbName}': ({_lc!.c4error_getDescription(err)})");
            }

            return db;
        }

        private void RecordError(string query, C4Error error)
        {
            var message = _lc!.c4error_getDescription(error);
            _result.ErrorCount++;
            _result.Total++;
            _result.ErrorResults.Add(new ErrorResult {Query = query, Message = message});
            ColorConsole.ForceWriteLine($"[ERROR] {query}: {message}");
        }

        private void RecordFail(string query, IReadOnlyList<IReadOnlyDictionary<string, object>> expected,
            IReadOnlyList<IReadOnlyDictionary<string, object>> actual)
        {
            _result.FailCount++;
            _result.Total++;
            _result.FailResults.Add(new FailResult
            {
                Actual = actual,
                Expected = expected,
                Query = query
            });

            using var g = ColorConsole.BeginGroup();
            ColorConsole.ForceWriteLine($"[FAIL] {query}", ColorConsole.WarnColor);
            ColorConsole.ForceWriteLine("Expected:", ColorConsole.WarnColor);
            ColorConsole.ForceWriteLine(JsonConvert.SerializeObject(expected, Formatting.Indented),
                ColorConsole.WarnColor);
            ColorConsole.ForceWriteLine("Actual:", ColorConsole.WarnColor);
            ColorConsole.ForceWriteLine(JsonConvert.SerializeObject(actual, Formatting.Indented),
                ColorConsole.WarnColor);
        }

        private void RecordPass(string query)
        {
            _result.PassCount++;
            _result.Total++;
            _result.PassResults.Add(query);
            ColorConsole.ForceWriteLine($"[PASS] {query}", ConsoleColor.Green);
        }

        private bool ResultsAreEqual(IReadOnlyList<IReadOnlyDictionary<string, object>> expected,
            IReadOnlyList<IReadOnlyDictionary<string, object>> actual)
        {
            if (expected.Count != actual.Count) {
                return false;
            }

            var comparer = new JsonEqualityComparer(IgnoreOrdering);
            return !IgnoreOrdering
                ? expected.SequenceEqual(actual, comparer)
                : comparer.RecursiveEquals(expected, actual);
        }

        #endregion
    }
}