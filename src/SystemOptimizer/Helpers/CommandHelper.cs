using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SystemOptimizer.Helpers;

public static class CommandHelper
{
    public sealed record CommandResult(bool Started, bool TimedOut, int? ExitCode, string StdOut, string StdErr)
    {
        public bool IsSuccess => Started && !TimedOut && ExitCode == 0;
    }

    public static string RunCommand(string fileName, string arguments, int timeoutMs = 5000)
    {
        var result = RunCommandDetailed(fileName, arguments, timeoutMs);

        if (!result.Started)
        {
            return string.Empty;
        }

        if (result.TimedOut)
        {
            return "[TIMEOUT CMD]";
        }

        if (!result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                Logger.Log($"Command returned error: {result.StdErr}", "CMD_STDERR");
                return $"[ERRO CMD] {result.StdErr}";
            }

            return string.Empty;
        }

        return result.StdOut;
    }

    public static CommandResult RunCommandDetailed(string fileName, string arguments, int timeoutMs = 5000)
    {
        return RunCommandDetailedAsync(fileName, arguments, timeoutMs).GetAwaiter().GetResult();
    }

    public static Task<CommandResult> RunCommandDetailedAsync(string fileName, string arguments, int timeoutMs = 5000)
    {
        return RunCommandDetailedAsync(fileName, arguments, null, timeoutMs);
    }

    public static Task<CommandResult> RunCommandDetailedAsync(string fileName, IEnumerable<string> argumentList, int timeoutMs = 5000)
    {
        return RunCommandDetailedAsync(fileName, null, argumentList, timeoutMs);
    }

    public static CommandResult RunCommandDetailed(string fileName, IEnumerable<string> argumentList, int timeoutMs = 5000)
    {
        return RunCommandDetailedAsync(fileName, argumentList, timeoutMs).GetAwaiter().GetResult();
    }

    private static async Task<CommandResult> RunCommandDetailedAsync(string fileName, string? arguments, IEnumerable<string>? argumentList, int timeoutMs)
    {
        Logger.Log($"Executing command: {fileName} {FormatArguments(arguments, argumentList)}", "CMD_START");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Garante que caracteres especiais (acentos) sejam lidos corretamente
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (argumentList != null)
            {
                foreach (string argument in argumentList)
                {
                    psi.ArgumentList.Add(argument);
                }
            }
            else
            {
                psi.Arguments = arguments ?? string.Empty;
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                Logger.Log($"Failed to start process: {fileName}", "CMD_ERROR");
                return new CommandResult(false, false, null, string.Empty, string.Empty);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.Log($"Command timed out ({timeoutMs}ms): {fileName} {arguments}", "CMD_TIMEOUT");
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception kEx)
                {
                    Logger.Log($"Failed to kill timed out process: {kEx.Message}", "CMD_ERROR");
                }

                string timeoutStdOut = outputTask.IsCompletedSuccessfully ? await outputTask : string.Empty;
                string timeoutStdErr = errorTask.IsCompletedSuccessfully ? await errorTask : string.Empty;
                return new CommandResult(true, true, null, timeoutStdOut, timeoutStdErr);
            }

            string output = await outputTask;
            string error = await errorTask;

            Logger.Log($"Command finished. ExitCode: {process.ExitCode}. OutputLen: {output.Length}. ErrorLen: {error.Length}", "CMD_END");
            if (!string.IsNullOrWhiteSpace(error))
            {
                Logger.Log($"Command stderr: {error}", "CMD_STDERR");
            }
            return new CommandResult(true, false, process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception running command {fileName}: {ex.Message}", "CMD_EXCEPTION");
            return new CommandResult(false, false, null, string.Empty, ex.Message);
        }
    }

    private static string FormatArguments(string? arguments, IEnumerable<string>? argumentList)
    {
        return argumentList == null ? arguments ?? string.Empty : string.Join(" ", argumentList);
    }

    public static void RunCommandNoWait(string fileName, string arguments)
    {
        Logger.Log($"Executing (NoWait): {fileName} {arguments}", "CMD_ASYNC");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(psi)?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception in RunCommandNoWait: {ex.Message}", "CMD_ASYNC_ERROR");
        }
    }
}
