// 
// ColorConsole.cs
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
using System.Threading;

namespace N1QLQueryHarness.Utilities
{
    /// <summary>
    /// The level of logging to be performed
    /// </summary>
    public enum LogLevel
    {
        Normal,
        Detailed,
        Verbose
    }

    /// <summary>
    /// A class for adding some color and primitive thread safety to the console
    /// </summary>
    internal static class ColorConsole
    {
        #region Constants

        internal const ConsoleColor ErrorColor = ConsoleColor.Red;
        internal const ConsoleColor NoisyColor = ConsoleColor.Gray;
        internal const ConsoleColor NormalColor = ConsoleColor.White;
        internal const ConsoleColor WarnColor = ConsoleColor.DarkYellow;

        private static readonly object Locker = new();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the level at which log messages should be output
        /// </summary>
        public static LogLevel? Level { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns an object that will allow the calling thread to monopolize
        /// the output until the object is disposed
        /// </summary>
        /// <returns>The disposable token that, when released, allows other threads
        /// to write again</returns>
        public static IDisposable BeginGroup() => new GroupLocker(Locker);

        /// <summary>
        /// Writes the provided text regardless of log level settings
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="color">The color to display the message in</param>
        public static void ForceWrite(string message, ConsoleColor color = ErrorColor)
        {
            lock (Locker) {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Write(message);
                Console.ForegroundColor = oldColor;
            }
        }

        /// <summary>
        /// Writes the provided text regardless of log level settings and goes to the next line
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="color">The color to display the message in</param>
        public static void ForceWriteLine(string message, ConsoleColor color = ErrorColor)
        {
            lock (Locker) {
                var threadId = Thread.CurrentThread.Name ?? $"Thread #{Thread.CurrentThread.ManagedThreadId:D2}";
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine($"{{{threadId}}} {message}");
                Console.ForegroundColor = oldColor;
            }
        }

        /// <summary>
        /// Writes the provided text if the current level allows for it
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="level">The level to log at</param>
        /// <param name="color">The color to display the message in</param>
        public static void Write(string message, LogLevel level = LogLevel.Normal, ConsoleColor color = NormalColor)
        {
            if (!Level.HasValue || level > Level.Value) {
                return;
            }

            ForceWrite(message, color);
        }

        /// <summary>
        /// Writes the provided text if the current level allows for it
        /// and goes to the next line
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="level">The level to log at</param>
        /// <param name="color">The color to display the message in</param>
        public static void WriteLine(string message, LogLevel level = LogLevel.Normal, ConsoleColor color = NormalColor)
        {
            if (!Level.HasValue || level > Level.Value) {
                return;
            }

            ForceWriteLine(message, color);
        }

        #endregion

        #region Nested

        private sealed class GroupLocker : IDisposable
        {
            #region Variables

            private readonly object _lockObject;

            #endregion

            #region Constructors

            public GroupLocker(object lockObject)
            {
                _lockObject = lockObject;
                Monitor.Enter(lockObject);
            }

            ~GroupLocker()
            {
                ReleaseUnmanagedResources();
            }

            #endregion

            #region Public Methods

            public void Dispose()
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }

            #endregion

            #region Private Methods

            private void ReleaseUnmanagedResources()
            {
                Monitor.Exit(_lockObject);
            }

            #endregion
        }

        #endregion
    }
}