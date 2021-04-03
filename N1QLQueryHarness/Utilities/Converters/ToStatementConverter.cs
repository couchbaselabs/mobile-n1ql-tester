// 
// ToStatementConverter.cs
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
    internal sealed class ToStatementConverter : IStatementConverter
    {
        #region IStatementConverter

        public string Convert(string statement)
        {
            var words = statement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words) {
                switch (word.ToLowerInvariant()) {
                    case "to_array":
                        return statement.Replace(word, "toarray");
                    case "to_atom":
                        return statement.Replace(word, "toatom");
                    case "to_bool":
                    case "to_boolean":
                    case "tobool":
                        return statement.Replace(word, "toboolean");
                    case "to_num":
                    case "to_number":
                    case "tonum":
                        return statement.Replace(word, "tonumber");
                    case "to_obj":
                    case "to_object":
                    case "toobj":
                        return statement.Replace(word, "toobject");
                    case "to_str":
                    case "to_string":
                    case "tostr":
                        return statement.Replace(word, "tostring");
                }
            }

            return statement;
        }

        public bool ShouldConvert(string statement) => statement.Contains(" to_", StringComparison.OrdinalIgnoreCase);

        #endregion
    }
}