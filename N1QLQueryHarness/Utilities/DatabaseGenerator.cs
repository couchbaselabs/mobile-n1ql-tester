// 
// DatabaseGenerator.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Couchbase.Lite;
using N1QLQueryHarness.Commands;
using Newtonsoft.Json;
using Serilog;
using sly.buildresult;
using sly.lexer;

namespace N1QLQueryHarness.Utilities
{
    /// <summary>
    /// The JSON schema of an insert.json entry in the server
    /// test cases
    /// </summary>
    internal sealed class InsertEntry
    {
        #region Properties

        /// <summary>
        /// The INSERT statements to use when creating the database
        /// </summary>
        [JsonProperty("statements")]
        public string? Statements { get; set; }

        #endregion
    }

    /// <summary>
    /// The tokens that are possible to find in an <see cref="InsertEntry"/>
    /// statement (somewhat awkward due to the number of things ignored)
    /// </summary>
    internal enum ValueToken
    {
        None,

        [Lexeme(GenericToken.KeyWord, "VALUES")]
        ValuesKeyword,

        // These are the values we are interested in, as they determine the boundaries
        // between entries in the VALUES clause
        [Lexeme(GenericToken.SugarToken, "(")] LeftParen,
        [Lexeme(GenericToken.SugarToken, ")")] RightParen,
        [Lexeme(GenericToken.SugarToken, ",")] Comma,
        
        // Without registering these, the lexer will be confused when it sees them
        // as it must know how to handle any character
        [Lexeme(GenericToken.String)] String,
        [Lexeme(GenericToken.SugarToken, "}")]
        [Lexeme(GenericToken.SugarToken, "{")]
        [Lexeme(GenericToken.SugarToken, "[")]
        [Lexeme(GenericToken.SugarToken, "]")]
        [Lexeme(GenericToken.SugarToken, ":")]
        [Lexeme(GenericToken.SugarToken, "-")]
        [Lexeme(GenericToken.Identifier, IdentifierType.AlphaNumericDash)]
        [Lexeme(GenericToken.Int)]
        [Lexeme(GenericToken.Double)]
        Misc
    }

    /// <summary>
    /// Parser for the VALUES clause of an INSERT statement
    /// </summary>
    internal sealed class ValueParser
    {
        #region Variables

        private readonly string _input;
        private readonly ILexer<ValueToken> _lexer;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="input">The string to parse</param>
        public ValueParser(string input)
        {
            _input = input;
            var buildResult = LexerBuilder.BuildLexer(new BuildResult<ILexer<ValueToken>>());
            if (buildResult.IsError) {
                throw new ApplicationException(
                    $"Lexer failed to build: {JsonConvert.SerializeObject(buildResult.Errors)}");
            }

            _lexer = buildResult.Result;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Perform the parsing
        /// </summary>
        /// <returns>A list of JSON strings representing documents to insert</returns>
        public IReadOnlyList<string> Parse()
        {
            var results = new List<string>();
            var parenthesisCount = 0;
            var previous = new Token<ValueToken> {TokenID = ValueToken.None};
            var tokenizeResult = _lexer.Tokenize(_input);
            if (tokenizeResult.IsError) {
                throw new ApplicationException(
                    $"Failed to tokenize input {_input}{Environment.NewLine}{tokenizeResult.Error.ErrorMessage}");
            }

            int start = 0;
            var commaFound = false;
            var valuesFound = false;
            foreach (var token in tokenizeResult.Tokens) {
                switch (token.TokenID) {
                    case ValueToken.ValuesKeyword:
                        valuesFound = true;
                        break;
                    case ValueToken.LeftParen:
                        if (previous.TokenID == ValueToken.ValuesKeyword) {
                            start = token.Position.Index + 1;
                            commaFound = false;
                        }

                        parenthesisCount++;

                        break;
                    case ValueToken.RightParen:
                        if (--parenthesisCount == 0 && commaFound) {
                            results.Add(_input.Substring(start, token.Position.Index - start));
                        }

                        break;
                    case ValueToken.Comma:
                        if (!commaFound && valuesFound) {
                            commaFound = true;
                            valuesFound = false;
                            results.Add(_input.Substring(start, token.Position.Index - start));
                            start = token.Position.Index + 1;
                        }

                        break;
                }

                previous = token;
            }

            return results;
        }

        #endregion
    }

    /// <summary>
    /// Generates a database based on an input insert.json from
    /// the server query test cases
    /// </summary>
    internal sealed class DatabaseGenerator
    {
        #region Constants

        private static readonly Regex DbNameRegex = new("^INSERT INTO ([a-z]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region Public Methods

        /// <summary>
        /// Performs the database generation
        /// </summary>
        /// <param name="parent">The calling object</param>
        /// <param name="jsonSourcePath">The relative path of the json from the input directory</param>
        /// <param name="outputDirectory">The path to write the output into</param>
        public void Generate(MigrateCommand parent, string jsonSourcePath, string outputDirectory)
        {
            var relativePath = Path.GetRelativePath(parent.InputDirectory!, jsonSourcePath);
            using var inputStream =
                new StreamReader(new FileStream(jsonSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read));
            using var jsonReader = new JsonTextReader(inputStream);
            var serializerSettings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };

            var serializer = JsonSerializer.Create(serializerSettings);

            var inputData = serializer.Deserialize<IReadOnlyList<InsertEntry>>(jsonReader);
            if (inputData == null) {
                throw new ApplicationException($"Failed to deserialize {jsonSourcePath}");
            }

            var dbConfig = new DatabaseConfiguration
            {
                Directory = outputDirectory
            };

            // BUG: Somehow this is reset to Info by the time we reach here...
            Database.Log.Console.Level = Couchbase.Lite.Logging.LogLevel.None;

            Log.Information($"Beginning db generation for {relativePath}...");

            var dbMap = new Dictionary<string, Database>();
            foreach (var entry in inputData) {
                var statements = entry.Statements!;
                var dbNameMatch = DbNameRegex.Match(statements).Groups;
                if (dbNameMatch.Count != 2) {
                    throw new InvalidDataException($"Invalid insert, missing db name: {statements}");
                }

                var dbName = dbNameMatch[1].Value;
                if (!dbMap.ContainsKey(dbName)) {
                    Database.Delete(dbName, outputDirectory);
                    var dbToInsert = new Database(dbName, dbConfig);
                    var dbRelativePath = Path.GetRelativePath(parent.OutputDirectory!, dbToInsert.Path!);
                    Log.Verbose("   ...Created database {0}", dbRelativePath);
                    dbMap[dbName] = dbToInsert;
                }

                var db = dbMap[dbName]!;
                var keyValueMatches = new ValueParser(statements).Parse();
                if (keyValueMatches.Count == 0) {
                    Log.Fatal($"Invalid insert, missing key / value: {statements}");
                    throw new InvalidDataException($"Invalid insert, missing key / value: {statements}");
                }

                for (int i = 0; i < keyValueMatches.Count; i += 2) {
                    var key = keyValueMatches[i];
                    key = key.StartsWith("\"") ? key.Trim('"') : KeyGenerator(key);
                    var val = JsonConvert.DeserializeObject<IDictionary<string, object>>(keyValueMatches[i + 1],
                                  serializerSettings)
                              ?? throw new InvalidDataException(
                                  $"Failed to deserialize value {keyValueMatches[i + 1]}");

                    using var doc = new MutableDocument(key, val);
                    db.Save(doc);
                }
            }

            Log.Information($"Finished db generation for {relativePath}!");
            Log.Verbose("\t...Databases found {0}", JsonConvert.SerializeObject(dbMap.Values.Select(x => x.Name)));

            foreach (var entry in dbMap) {
                entry.Value.Dispose();
            }
        }

        #endregion

        #region Private Methods

        // Handles the case where UUID() is found in the keys clause
        private string KeyGenerator(string input)
        {
            var parenIndex = input.IndexOf('(');
            if (parenIndex == -1) {
                throw new InvalidDataException($"Unknown operator in clause {input}");
            }

            var fName = input.Substring(0, parenIndex);
            switch (fName.ToLowerInvariant()) {
                case "uuid":
                    return Guid.NewGuid().ToString();
                default:
                    throw new InvalidDataException($"Unknown function '{fName}'");
            }
        }

        #endregion
    }
}