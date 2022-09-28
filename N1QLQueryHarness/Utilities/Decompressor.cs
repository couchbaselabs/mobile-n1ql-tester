// 
// Decompressor.cs
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
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;

namespace N1QLQueryHarness.Utilities
{
    /// <summary>
    /// A factory for creating an object to decompress a LiteCore library
    /// from latestbuilds
    /// </summary>
    internal static class Decompressor
    {
        #region Public Methods

        /// <summary>
        /// Returns the appropriate decompressor for the given platform
        /// </summary>
        /// <returns></returns>
        public static IDecompressor GetDecompressor()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return new TarDecompressor();
            } else {
                return new ZipDecompressor();
            }
        }

        #endregion

        #region Nested

        private sealed class ZipDecompressor : IDecompressor
        {
            #region IDecompressor

            public async Task DecompressAsync(Stream input, string outputDirectory)
            {
                using var zin = new ZipArchive(input, ZipArchiveMode.Read, true);
                foreach (var entry in zin.Entries) {
                    var outputPath = Path.Combine(outputDirectory, entry.FullName);
                    if (outputPath.EndsWith("/")) {
                        Directory.CreateDirectory(Path.Combine(outputDirectory, entry.FullName));
                        continue;
                    }

                    await using var entryStream = entry.Open();
                    await using var fout = File.OpenWrite(outputPath);
                    await entryStream.CopyToAsync(fout).ConfigureAwait(false);
                }
            }

            #endregion
        }

        private sealed class TarDecompressor : IDecompressor
        {
            #region IDecompressor

            public Task DecompressAsync(Stream input, string outputDirectory)
            {
                using var gin = new GZipStream(input, CompressionMode.Decompress, true);
                using var tar = TarArchive.CreateInputTarArchive(gin, Encoding.UTF8);
                tar.ExtractContents(outputDirectory);
                var libDir = Path.Combine(outputDirectory, "lib");
                foreach (var f in Directory.EnumerateFiles(libDir)) {
                    // ICSharpCode puzzingly doesn't copy symlinks...
                    var fileName = Path.GetFileName(f);
                    if (fileName.StartsWith("libicu")) {
                        var parts = fileName.Split('.');
                        var symLinkName = string.Join('.', parts.Take(parts.Length - 1));
                        File.CreateSymbolicLink(Path.Combine(libDir, symLinkName), fileName);
                        Console.WriteLine($"Created {symLinkName} -> {fileName}");
                    }
                }

                return Task.CompletedTask;
            }

            #endregion
        }

        #endregion
    }

    /// <summary>
    /// An interface for decompressing a compressed archive for a given
    /// platform (Linux tar.gz, others .zip)
    /// </summary>
    internal interface IDecompressor
    {
        #region Public Methods

        /// <summary>
        /// Perform the decompression
        /// </summary>
        /// <param name="input">The stream to decompress</param>
        /// <param name="outputDirectory">The location to decompress the results into</param>
        /// <returns></returns>
        Task DecompressAsync(Stream input, string outputDirectory);

        #endregion
    }
}