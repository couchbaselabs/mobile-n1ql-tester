// 
// LogInterceptor.cs
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

using N1QLQueryHarness.Commands;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Spectre;
using Spectre.Console.Cli;

namespace N1QLQueryHarness.Utilities
{
    internal sealed class SerilogLogger : Couchbase.Lite.Logging.ILogger
    {
        public Couchbase.Lite.Logging.LogLevel Level { get; set; }

        public void Log(Couchbase.Lite.Logging.LogLevel level, Couchbase.Lite.Logging.LogDomain domain, string message)
        {
            var fullMsg = $"[CBL:{domain}] {message}";
            switch (level) {
                // Debug and Verbose are reversed in Serilog
                case Couchbase.Lite.Logging.LogLevel.Debug:
                    Serilog.Log.Verbose(fullMsg);
                    break;
                case Couchbase.Lite.Logging.LogLevel.Info:
                    Serilog.Log.Information(fullMsg);
                    break;
                case Couchbase.Lite.Logging.LogLevel.Warning:
                    Serilog.Log.Warning(fullMsg);
                    break;
                case Couchbase.Lite.Logging.LogLevel.Error:
                    Serilog.Log.Error(fullMsg);
                    break;
                case Couchbase.Lite.Logging.LogLevel.Verbose:
                    Serilog.Log.Debug(fullMsg);
                    break;
            }
        }
    }

    internal sealed class LogInterceptor : ICommandInterceptor
    {
        private Couchbase.Lite.Logging.LogLevel FromSerilogLevel(LogEventLevel level)
        {
            switch(level) {
                case LogEventLevel.Verbose:
                    return Couchbase.Lite.Logging.LogLevel.Debug;
                case LogEventLevel.Fatal:
                case LogEventLevel.Error:
                    return Couchbase.Lite.Logging.LogLevel.Error;
                case LogEventLevel.Information:
                    return Couchbase.Lite.Logging.LogLevel.Info;
                case LogEventLevel.Warning:
                    return Couchbase.Lite.Logging.LogLevel.Warning;
                case LogEventLevel.Debug:
                    return Couchbase.Lite.Logging.LogLevel.Verbose;
                default:
                    return Couchbase.Lite.Logging.LogLevel.None;
            }
        }

        public void Intercept(CommandContext context, CommandSettings settings)
        {
            if(settings is BaseCommandSettings s)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .Enrich.WithThreadId()
                    .WriteTo.Spectre("[<{ThreadId}> {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", levelSwitch: new LoggingLevelSwitch(s.LogLevel))
                    .CreateLogger();

                
                Couchbase.Lite.Database.Log.Custom = new SerilogLogger() { Level = FromSerilogLevel(s.LogLevel) };
            }
        }
    }
}
