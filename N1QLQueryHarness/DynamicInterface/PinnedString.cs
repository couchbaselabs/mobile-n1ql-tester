// 
// PinnedString.cs
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
using System.Text;

namespace N1QLQueryHarness.DynamicInterface
{
    /// <summary>
    /// A construct for pinning the address of a managed C# string so that it
    /// may be safely passed to unmanaged code
    /// </summary>
    internal unsafe struct PinnedString : IDisposable
    {
        #region Variables

        private int _byteCount;
        private byte* _bytes;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="s">The string to save in memory</param>
        public PinnedString(string s)
        {
            _byteCount = 0;
            _bytes = null;
            if (s != null) {
                _byteCount = Encoding.UTF8.GetByteCount(s);
                _bytes = (byte*) Marshal.AllocHGlobal(_byteCount);
                fixed (char* c = s) {
                    Encoding.UTF8.GetBytes(c, s.Length, _bytes, _byteCount);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the slice representation of the string
        /// </summary>
        /// <returns>The slice representation of the string</returns>
        public Slice AsSlice()
        {
            return new Slice
            {
                buffer = _bytes,
                size = (ulong) _byteCount
            };
        }

        #endregion

        #region IDisposable
        
#pragma warning disable 1591
        public void Dispose()
        {
            Marshal.FreeHGlobal((IntPtr) _bytes);
            _bytes = null;
        }
#pragma warning restore 1591

        #endregion
    }
}