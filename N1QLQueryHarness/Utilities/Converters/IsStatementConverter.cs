// 
// IsStatementConverter.cs
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

using System;

namespace N1QLQueryHarness.Utilities.Converters
{
    internal sealed class IsStatementConverter : IStatementConverter
    {
        #region IStatementConverter

        public string Convert(string statement)
        {
            var words = statement.Split(new [] {' ', '('}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words) {
                switch (word.ToLowerInvariant()) {
                    case "is_array":
                        return statement.Replace(word, "isarray");
                    case "is_atom":
                        return statement.Replace(word, "isatom");
                    case "is_bool":
                    case "is_boolean":
                    case "isbool":
                        return statement.Replace(word, "isboolean");
                    case "is_num":
                    case "is_number":
                    case "isnum":
                        return statement.Replace(word, "isnumber");
                    case "is_obj":
                    case "is_object":
                    case "isobj":
                        return statement.Replace(word, "isobject");
                    case "is_str":
                    case "is_string":
                    case "isstr":
                        return statement.Replace(word, "isstring");
                }
            }

            return statement;
        }

        public bool ShouldConvert(string statement) => statement.Contains(" is_", StringComparison.OrdinalIgnoreCase);

        #endregion
    }
}