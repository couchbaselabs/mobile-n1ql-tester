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
using Serilog.Sinks.Spectre;
using Spectre.Console.Cli;

namespace N1QLQueryHarness.Utilities
{
    internal sealed class LogInterceptor : ICommandInterceptor
    {
        public void Intercept(CommandContext context, CommandSettings settings)
        {
            if(settings is BaseCommandSettings s)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .Enrich.WithThreadId()
                    .WriteTo.Spectre("[<{ThreadId}> {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", levelSwitch: new LoggingLevelSwitch(s.LogLevel))
                    .CreateLogger();
            }
        }
    }
}
