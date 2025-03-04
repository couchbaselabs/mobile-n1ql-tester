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
using System.IO;
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
            private static readonly bool UseAlternate;

            private static bool FindFileRecursively(string directory, string targetFile)
            {
                foreach (var file in Directory.GetFiles(directory)) {
                    if (Path.GetFileName(file) == targetFile) {
                        Log.Information($"Found {targetFile} in {directory}");
                        return true;
                    }
                }

                foreach (var subDirectory in Directory.GetDirectories(directory)) {
                    var foundFile = FindFileRecursively(subDirectory, targetFile);
                    if (foundFile) {
                        return true;
                    }
                }

                return false;
            }

            static Native()
            {
                if(!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                    return;
                }

                var linuxArch = "";
                if(RuntimeInformation.ProcessArchitecture == Architecture.X64) {
                    linuxArch = "x86_64";
                } else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                    linuxArch = "aarch64";
                } else {
                    throw new Exception("Invalid executing process architecture");
                }

                var triple = $"{linuxArch}-linux-gnu";
                Log.Information($"Searching for libdl.so.2 in /lib/{triple}/...");
                UseAlternate = FindFileRecursively($"/lib/{triple}", "libdl.so.2");
            }

            public static int dlclose(IntPtr handle)
            {
                if (UseAlternate) {
                    return dlclose2(handle);
                }
                return dlclose1(handle);
            }

            public static IntPtr dlopen(string filename, int flags)
            {
                if (UseAlternate) {
                    return dlopen2(filename, flags);
                }
                return dlopen1(filename, flags);
            }

            public static IntPtr dlsym(IntPtr handle, string symbol)
            {
                if (UseAlternate) {
                    return dlsym2(handle, symbol);
                }
                return dlsym1(handle, symbol);
            }

            [DllImport("libdl", EntryPoint = "dlclose")]
            private static extern int dlclose1(IntPtr handle);

            [DllImport("libdl", EntryPoint = "dlopen")]
            private static extern IntPtr dlopen1(string filename, int flags);

            [DllImport("libdl", EntryPoint = "dlsym")]
            private static extern IntPtr dlsym1(IntPtr handle, string symbol);

            [DllImport("libdl.so.2", EntryPoint = "dlclose")]
            private static extern int dlclose2(IntPtr handle);

            [DllImport("libdl.so.2", EntryPoint = "dlopen")]
            private static extern IntPtr dlopen2(string filename, int flags);

            [DllImport("libdl.so.2", EntryPoint = "dlsym")]
            private static extern IntPtr dlsym2(IntPtr handle, string symbol);
        }

        #endregion
    }
}