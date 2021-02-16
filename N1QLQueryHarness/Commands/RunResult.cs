// 
// RunResult.cs
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

using System.Collections.Generic;
using Newtonsoft.Json;

namespace N1QLQueryHarness.Commands
{
    internal sealed class RunResult
    {
        #region Properties

        [JsonProperty("errorCount")]
        public int ErrorCount { get; set; }

        [JsonProperty("errorResults")]
        public IList<ErrorResult> ErrorResults { get; set; } = new List<ErrorResult>();

        [JsonProperty("failCount")]
        public int FailCount { get; set; }

        [JsonProperty("failResults")]
        public IList<FailResult> FailResults { get; set; } = new List<FailResult>();

        [JsonProperty("passCount")]
        public int PassCount { get; set; }

        [JsonProperty("passResults")]
        public IList<string> PassResults { get; set; } = new List<string>();

        [JsonProperty("total")]
        public int Total { get; set; }

        #endregion
    }

    internal sealed class FailResult
    {
        #region Properties

        [JsonProperty("actual")]
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Actual { get; set; }

        [JsonProperty("expected")]
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Expected { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; }

        #endregion
    }

    internal sealed class ErrorResult
    {
        #region Properties

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; }

        #endregion
    }
}