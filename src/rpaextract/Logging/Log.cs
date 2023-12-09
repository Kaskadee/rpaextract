// rpaextract - Log.cs
// Copyright (C) 2023 Fabian Creutz.
// 
// Licensed under the EUPL, Version 1.2 or – as soon they will be approved by the
// European Commission - subsequent versions of the EUPL (the "Licence");
// 
// You may not use this work except in compliance with the Licence.
// You may obtain a copy of the Licence at:
// 
// https://joinup.ec.europa.eu/software/page/eupl
// 
// Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the Licence for the specific language governing permissions and limitations under the Licence.

using System;
using Spectre.Console;

namespace rpaextract.Logging;

/// <summary>
/// Provides a simple logging interface for printing messages to the standard output and standard error streams.
/// </summary>
public static class Log {
    private static LogMode CurrentLogMode = LogMode.Normal;

    /// <summary>
    /// Sets the current log mode for logging purposes.
    /// </summary>
    /// <param name="mode">The log mode to be set.</param>
    public static void SetLogMode(LogMode mode) {
        CurrentLogMode = mode;
    }
    
    /// <summary>
    /// Logs an informational message to the console.
    /// If quiet mode is enabled, the message will not be logged.
    /// </summary>
    /// <param name="message">The message to be logged.</param>
    public static void Info(string message) {
        if (CurrentLogMode == LogMode.Quiet)
            return;
        AnsiConsole.MarkupInterpolated($"{message}");
    }

    /// <summary>
    /// Logs an verbose message to the console.
    /// If verbose mode is not enabled, the message will not be logged.
    /// </summary>
    /// <param name="message">The verbose message to be logged.</param>
    public static void Verbose(string message) {
        if (CurrentLogMode != LogMode.Verbose)
            return;
        AnsiConsole.MarkupInterpolated($"[wheat1]{message}[/]");
    }

    /// <summary>
    /// Logs an error message to the console.
    /// If quiet mode is enabled, the message will not be logged.
    /// </summary>
    /// <param name="message">The error message to be logged.</param>
    public static void Error(string message) {
        if (CurrentLogMode == LogMode.Quiet)
            return;
        AnsiConsole.MarkupInterpolated($"[bold red3](Error)[/] [indianred]{message}[/]");
    }

    /// <summary>
    /// Logs an exception message to the console.
    /// If quiet mode is enabled, the message will not be logged.
    /// </summary>
    /// <param name="message">The error message to be logged.</param>
    /// <param name="exception">The exception to be logged.</param>
    public static void Exception(string message, Exception exception) {
        if (CurrentLogMode == LogMode.Quiet)
            return;
        Error(message);
        AnsiConsole.WriteException(exception);
    }
}
