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
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using N1QLQueryHarness.Utilities;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace N1QLQueryHarness.Commands
{
    internal sealed class PrepareCommandSettings : BaseCommandSettings
    {
        [CommandArgument(0, "[SHA]")]
        [Description("The SHA of the Git commit of LiteCore to use")]
        public string SHA { get; set; } = "";
    }

    internal sealed class PrepareCommand : AsyncCommand<PrepareCommandSettings>
    {
        #region Constants

        private static readonly HttpClient HttpClient = new();

        #endregion

        #region Variables

        private PrepareCommandSettings _settings = new();

        #endregion

        #region Public Methods

        public static void AddToApplication(IConfigurator config)
        {
            config.AddCommand<PrepareCommand>("prepare")
                .WithDescription("Prepare the specified version of LiteCore for use")
                .WithExample(new[] { "prepare", "b9a487021eadcf0539f993dd4aeeba699721f580" });
        }

        #endregion

        #region Private Methods

        private Task CopyAsync(Stream from, Stream to)
        {
            return AnsiConsole.Status().StartAsync("Downloading (0.00 MiB)", async ctx =>
            {
                var buffer = new byte[8192];
                double total = 0;
                int numRead;
                while ((numRead = await from.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await to.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, numRead)).ConfigureAwait(false);
                    total += numRead;
                    var adjusted = total / (1024.0 * 1024.0);
                    ctx.Spinner(Spinner.Known.Shark);
                    ctx.Status($"Downloading ({adjusted:G2} MiB)");
                }
            });
        }

        private async Task DownloadLiteCore(string workingDir)
        {
            var litecoreBinDir = Path.Join(workingDir, "lib")!;

            try {
                Directory.Delete(litecoreBinDir, true);
            } catch (DirectoryNotFoundException) {
            }

            Directory.CreateDirectory(litecoreBinDir);
            Log.Information("Downloading LiteCore prebuilt binary...");
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
                Log.Error("Unsupported OS: {0}", RuntimeInformation.OSDescription);
                throw new NotSupportedException($"Unsupported OS: {RuntimeInformation.OSDescription}");
            }

            var shaPrefix = _settings.SHA.Substring(0, 2);
            var urlStr =
                $"http://latestbuilds.service.couchbase.com/builds/latestbuilds/couchbase-lite-core/sha/{shaPrefix}/{_settings.SHA}/couchbase-lite-core-{osForUrl}.{extension}";
            Log.Information("Attempting to download {0}", urlStr);
            var outputPath = Path.Join(Path.GetTempPath(), $"litecore.{extension}");
            { 
                using var downloadStream = await HttpClient.GetStreamAsync(urlStr).ConfigureAwait(false);
                await using var fout = File.OpenWrite(outputPath);
                await CopyAsync(downloadStream, fout).ConfigureAwait(false);
                await fout.FlushAsync().ConfigureAwait(false);
            }

            Log.Information("Extracting...");
            await using (var fin = File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                await Decompressor.GetDecompressor().DecompressAsync(fin, litecoreBinDir).ConfigureAwait(false);
            }

            File.Delete(outputPath);
        }

        #endregion

        #region AsyncCommand Implementation

        public override async Task<int> ExecuteAsync(CommandContext context, PrepareCommandSettings settings)
        {
            _settings = settings;
            var workingDir = _settings.WorkingDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var dataDir = Path.Combine(workingDir, "data");
            if (!Directory.Exists(dataDir)) {
                Log.Fatal($"Query data not found at {dataDir}");
                Log.Fatal("Please clone the repo at https://github.com/couchbase/query/ into that folder");
                return 1;
            }

            await DownloadLiteCore(workingDir).ConfigureAwait(false);
            return 0;
        }

        #endregion
    }
}