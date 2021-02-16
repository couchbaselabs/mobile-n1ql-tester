// 
// PrepareCommand.cs
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

#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using N1QLQueryHarness.Utilities;

namespace N1QLQueryHarness.Commands
{
    [Command(Description = "Prepare the specified version of LiteCore for use")]
    internal sealed class PrepareCommand : BaseCommand
    {
        #region Constants

        private static readonly HttpClient HttpClient = new();

        #endregion

        #region Properties

        [Option(Description = "The SHA of the Git commit of LiteCore to use")]
        [Required]
        public string? SHA { get; set; }

        [Option(LongName = "wd",
            Description = "The directory to operate in (should be consistent between all subcommands)")]
        [LegalFilePath]
        public string? WorkingDirectory { get; set; }

        #endregion

        #region Private Methods

        private async Task CopyAsync(Stream from, Stream to)
        {
            Console.WriteLine();
            var buffer = new byte[8192];
            double total = 0;
            int numRead;
            while ((numRead = await from.ReadAsync(buffer).ConfigureAwait(false)) > 0) {
                await to.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, numRead)).ConfigureAwait(false);
                total += numRead;
                var adjusted = total / (1024.0 * 1024.0);

                Console.Write("\r");
                ColorConsole.Write($"{adjusted:F3} MiB finished...");
            }

            Console.WriteLine();
        }

        private async Task DownloadLiteCore(string workingDir)
        {
            var litecoreBinDir = Path.Join(workingDir, "lib")!;

            try {
                Directory.Delete(litecoreBinDir, true);
            } catch (DirectoryNotFoundException) {
            }

            Directory.CreateDirectory(litecoreBinDir);
            ColorConsole.WriteLine("Downloading LiteCore prebuilt binary...");
            string osForUrl, extension;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                osForUrl = "windows-win64";
                extension = "zip";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                osForUrl = "macosx";
                extension = "zip";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                osForUrl = "linux";
                extension = "tar.gz";
            } else {
                ColorConsole.ForceWriteLine($"Unsupported OS: {RuntimeInformation.OSDescription}");
                throw new NotSupportedException($"Unsupported OS: {RuntimeInformation.OSDescription}");
            }

            var urlStr =
                $"http://nexus.build.couchbase.com:8081/nexus/content/repositories/releases/com/couchbase/litecore/couchbase-litecore-{osForUrl}/{SHA}/couchbase-litecore-{osForUrl}-{SHA}.{extension}";
            ColorConsole.WriteLine($"Attempting to download {urlStr}");
            var outputPath = Path.Join(litecoreBinDir, $"litecore.{extension}");
            var downloadStream = await HttpClient.GetStreamAsync(urlStr).ConfigureAwait(false);
            await using (var fout = File.OpenWrite(outputPath)) {
                await CopyAsync(downloadStream, fout).ConfigureAwait(false);
            }

            ColorConsole.WriteLine("Extracting...");
            await using (var fin = File.OpenRead(outputPath)) {
                await Decompressor.GetDecompressor().DecompressAsync(fin, litecoreBinDir).ConfigureAwait(false);
            }

            File.Delete(outputPath);
        }

        // Called via reflection
        // ReSharper disable once UnusedMember.Local
        private async Task<int> OnExecute()
        {
            var workingDir = WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var dataDir = Path.Combine(workingDir, "data");
            if (!Directory.Exists(dataDir)) {
                Console.ForegroundColor = ColorConsole.ErrorColor;
                Console.WriteLine($"Query data not found at {dataDir}");
                Console.WriteLine("Please the repo at https://github.com/couchbase/query/ into that folder");
                return 1;
            }

            await DownloadLiteCore(workingDir).ConfigureAwait(false);
            return 0;
        }

        #endregion
    }
}