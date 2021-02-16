// 
// ILiteCoreLoader.cs
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

namespace N1QLQueryHarness.DynamicInterface
{
    /// <summary>
    /// An interface that allows loading and unloading of the native LiteCore
    /// shared library at runtime, as well as the ability to lookup symbols inside
    /// </summary>
    internal interface ILiteCoreLoader
    {
        #region Public Methods

        /// <summary>
        /// Loads the LiteCore shared native library (i.e. dlopen)
        /// </summary>
        /// <param name="filename">The path to the shared library file</param>
        void Load(string filename);

        /// <summary>
        /// Looks up a symbol by name inside of the shared library (i.e. dlsym)
        /// </summary>
        /// <param name="name">The name of the symbol</param>
        /// <returns>A handle to the address of the symbol</returns>
        IntPtr Lookup(string name);

        /// <summary>
        /// Unloads the shared native library
        /// </summary>
        void Unload();

        #endregion
    }
}