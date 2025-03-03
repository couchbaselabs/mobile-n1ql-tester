// 
// UnixLiteCoreLoader.cs
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
using Serilog;

namespace N1QLQueryHarness.DynamicInterface
{
    /// <summary>
    /// The Unix variant of <see cref="ILiteCoreLoader"/>
    /// </summary>
    internal sealed class UnixLiteCoreLoader : ILiteCoreLoader
    {
        #region Constants

        private const int RTLD_LAZY = 1;

        #endregion

        #region Variables

        private IntPtr _library;

        #endregion

        #region Public Methods
        
        public void Load(string filename)
        {
            var opened = Native.dlopen(filename, RTLD_LAZY);
            var old = Interlocked.Exchange(ref _library, opened);
            if (old != IntPtr.Zero) {
                Native.dlclose(old);
            }
        }
        
        public IntPtr Lookup(string name)
        {
            Log.Debug($"Looking up symbol {name} using dlsym...");
            var retVal = Native.dlsym(_library, name);
            Log.Debug($"\t...found 0x{retVal:08X}");
            return retVal;
        }
        
        public void Unload()
        {
            var old = Interlocked.Exchange(ref _library, IntPtr.Zero);
            Native.dlclose(old);
        }

        #endregion

        #region Nested

        private static class Native
        {
            #region Public Methods

            [DllImport("libdl")]
            public static extern int dlclose(IntPtr handle);

            [DllImport("libdl")]
            public static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);

            #endregion
        }

        #endregion
    }
}