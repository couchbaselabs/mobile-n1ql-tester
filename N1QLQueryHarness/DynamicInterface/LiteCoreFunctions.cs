// 
// LiteCoreFunctions.cs
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
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using N1QLQueryHarness.Utilities;
using Serilog;

// ReSharper disable InconsistentNaming
#pragma warning disable 649

namespace N1QLQueryHarness.DynamicInterface
{
    // NOTE: Must mirror what is in Couchbase Lite
    // BEGIN DEFINITIONS FROM COUCHBASE LITE
    [Flags]
    internal enum C4DatabaseFlags : uint
    {
        Create = 0x01,
        ReadOnly = 0x02,
        AutoCompact = 0x04,
        VersionVectors = 0x08,
        NoUpgrade = 0x20,
        NonObservable = 0x40
    }

    internal struct C4Error
    {
        public uint domain;
        public int code;
        public int internal_info;
    }

    internal struct C4Database
    {
    }

    internal struct C4Query
    {
    }

    internal struct FLValue
    {
    }

    internal unsafe struct C4QueryEnumerator
    {
        public FLArrayIterator columns;
        public ulong missingColumns;
        public uint fullTextMatchCount;
        public void* fullTextMatches;
    }

    internal unsafe struct Slice
    {
        public void* buffer;
        public ulong size;

        public string CreateString() => Encoding.UTF8.GetString((byte*) buffer, (int) size);
    }

    internal unsafe struct SliceResult
    {
        public void* buffer;
        public ulong size;

        public string CreateString() => Encoding.UTF8.GetString((byte*) buffer, (int) size);
    }

    internal unsafe struct C4DatabaseConfig2
    {
        public Slice parentDirectory;
        public C4DatabaseFlags flags;
        public fixed byte unused[36];
    }

    internal unsafe struct FLArrayIterator
    {
        private void* unused1;
        private uint unused2;
        private byte unused3;
        private void* unused4;
    }
    // END DEFINITIONS FROM COUCHBASE LITE

    /// <summary>
    /// A class that wraps the LiteCore shared library and provides wrappers
    /// for calling its functions
    /// </summary>
    internal sealed unsafe class LiteCoreFunctions
    {
        #region Variables

        private readonly ILiteCoreLoader _liteCoreLoader;
        private readonly ConcurrentDictionary<string, IntPtr> _symbolMap = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filename">The path to the LiteCore shared library</param>
        public LiteCoreFunctions(string filename)
        {
            if (!File.Exists(filename)) {
                Log.Fatal("LiteCore.dll not found, run the prepare command first!");
                throw new FileNotFoundException();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                _liteCoreLoader = new WindowsLiteCoreLoader();
            } else {
                _liteCoreLoader = new UnixLiteCoreLoader();
            }

            _liteCoreLoader.Load(filename);
        }

        #endregion

        #region Public Methods

        // Wrappers to native calls

        public C4Database* c4db_openNamed(Slice name, C4DatabaseConfig2* config, ref C4Error err)
        {
            var callable = GetSymbol<OpenNamedDelegate>(nameof(c4db_openNamed));
            var retVal = callable(name, (IntPtr) config, ref err);
            return (C4Database*) retVal;
        }

        public void c4db_release(C4Database* db) => c4base_release(db);

        public string c4error_getDescription(C4Error err)
        {
            var callable = GetSymbol<ErrorMessageDelegate>(nameof(c4error_getDescription));
            var s = callable(err);
            var retVal = s.CreateString();
            FLSliceResult_Release(s);
            return retVal;
        }

        public void c4log_setCallbackLevel(sbyte level)
        {
            var callable = GetSymbol<SetLogLevelDelegate>(nameof(c4log_setCallbackLevel));
            callable(level);
        }

        public Slice c4query_columnTitle(C4Query* query, uint column)
        {
            var callable = GetSymbol<ColumnTitleDelegate>(nameof(c4query_columnTitle));
            return callable((IntPtr) query, column);
        }

        public C4Query* c4query_new2(C4Database* db, Slice expression, ref int outErrorPos, ref C4Error outErr)
        {
            var callable = GetSymbol<NewQueryDelegate>(nameof(c4query_new2));
            var retVal = callable((IntPtr) db, 1, expression, ref outErrorPos, ref outErr);
            return (C4Query*) retVal;
        }

        public void c4query_release(C4Query* query) => c4base_release(query);

        public C4QueryEnumerator* c4query_run(C4Query* query, Slice parameters, ref C4Error err)
        {
            var callable = GetSymbol<RunQueryDelegate>(nameof(c4query_run));
            var retVal = callable((IntPtr) query, parameters, ref err);
            return (C4QueryEnumerator*) retVal;
        }

        public bool c4queryenum_next(C4QueryEnumerator* e, ref C4Error err)
        {
            var callable = GetSymbol<QueryNextDelegate>(nameof(c4queryenum_next));
            var retVal = callable((IntPtr) e, ref err);
            return Convert.ToBoolean(retVal);
        }

        public void c4queryenum_release(C4QueryEnumerator* e)
        {
            var callable = GetSymbol<FreeDelegate>(nameof(c4queryenum_release));
            callable((IntPtr) e);
        }

        public uint FLArrayIterator_GetCount(FLArrayIterator* iter)
        {
            var callable = GetSymbol<ArrayIteratorCountDelgate>(nameof(FLArrayIterator_GetCount));
            return callable((IntPtr) iter);
        }

        public FLValue* FLArrayIterator_GetValue(FLArrayIterator* iter)
        {
            var callable = GetSymbol<ArrayIteratorValueDelgate>(nameof(FLArrayIterator_GetValue));
            var retVal = callable((IntPtr) iter);
            return (FLValue*) retVal;
        }

        public bool FLArrayIterator_Next(FLArrayIterator* iter)
        {
            var callable = GetSymbol<ArrayIteratorNextDelegate>(nameof(FLArrayIterator_Next));
            var retVal = callable((IntPtr) iter);
            return Convert.ToBoolean(retVal);
        }

        public void FLSliceResult_Release(SliceResult r) => _FLBuf_Release(r.buffer);

        public string FLValue_ToJSON(FLValue* v)
        {
            var callable = GetSymbol<ToJsonDelegate>(nameof(FLValue_ToJSON));
            var s = callable((IntPtr) v);
            var retVal = s.CreateString();
            FLSliceResult_Release(s);
            return retVal;
        }

        #endregion

        #region Private Methods

        private void _FLBuf_Release(void* buf)
        {
            var callable = GetSymbol<FreeDelegate>(nameof(_FLBuf_Release));
            callable((IntPtr) buf);
        }

        private void c4base_release(void* ptr)
        {
            var callable = GetSymbol<FreeDelegate>(nameof(c4base_release));
            callable((IntPtr) ptr);
        }

        // The heart of the trick, casting the function address to a callable function
        private T GetSymbol<T>(string name)
        {
            var address = _symbolMap.GetOrAdd(name, s => _liteCoreLoader.Lookup(s));
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        #endregion

        #region Nested

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr OpenNamedDelegate(Slice name, IntPtr config, ref C4Error err);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FreeDelegate(IntPtr ptr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr NewQueryDelegate(IntPtr db, uint lang, Slice expression, ref int outErrorPos,
            ref C4Error err);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr RunQueryDelegate(IntPtr query, Slice parameters, ref C4Error err);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate byte QueryNextDelegate(IntPtr queryEnum, ref C4Error err);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate byte ArrayIteratorNextDelegate(IntPtr arrayIter);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint ArrayIteratorCountDelgate(IntPtr arrayIter);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ArrayIteratorValueDelgate(IntPtr arrayIter);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Slice ColumnTitleDelegate(IntPtr query, uint column);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate SliceResult ToJsonDelegate(IntPtr v);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate SliceResult ErrorMessageDelegate(C4Error err);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetLogLevelDelegate(sbyte level);

        #endregion
    }
}