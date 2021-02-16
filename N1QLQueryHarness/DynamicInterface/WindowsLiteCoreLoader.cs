// 
// WindowsLiteCoreLoader.cs
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
using System.Runtime.InteropServices;
using System.Threading;

namespace N1QLQueryHarness.DynamicInterface
{
    /// <summary>
    /// The Windows variant of <see cref="ILiteCoreLoader"/>
    /// </summary>
    internal sealed class WindowsLiteCoreLoader : ILiteCoreLoader
    {
        #region Variables

        private IntPtr _library;

        #endregion

        #region Public Methods

        public void Load(string filename)
        {
            var opened = Native.LoadLibrary(filename);
            var old = Interlocked.Exchange(ref _library, opened);
            Native.FreeLibrary(old);
        }

        public IntPtr Lookup(string name)
        {
            return Native.GetProcAddress(_library, name);
        }

        public void Unload()
        {
            var old = Interlocked.Exchange(ref _library, IntPtr.Zero);
            Native.FreeLibrary(old);
        }

        #endregion

        #region Nested

        private static class Native
        {
            #region Public Methods

            [DllImport("kernel32")]
            public static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("kernel32")]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procname);

            [DllImport("kernel32")]
            public static extern IntPtr LoadLibrary(string filename);

            #endregion
        }

        #endregion
    }
}