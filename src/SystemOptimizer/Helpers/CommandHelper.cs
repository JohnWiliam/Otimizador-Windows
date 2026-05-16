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
        return FormatOutput(RunCommandDetailed(fileName, arguments, timeoutMs));
    }

    public static string RunCommand(string fileName, IReadOnlyList<string> arguments, int timeoutMs = 5000)
    {
        return FormatOutput(RunCommandDetailed(fileName, arguments, timeoutMs));
    }

    public static CommandResult RunCommandDetailed(string fileName, string arguments, int timeoutMs = 5000)
    {
        var psi = CreateStartInfo(fileName);
        psi.Arguments = arguments;
        return RunProcess(psi, timeoutMs, $"{fileName} {arguments}");
    }

    public static CommandResult RunCommandDetailed(string fileName, IReadOnlyList<string> arguments, int timeoutMs = 5000)
    {
        var psi = CreateStartInfo(fileName);
        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return RunProcess(psi, timeoutMs, $"{fileName} {string.Join(' ', arguments)}");
    }

    private static string FormatOutput(CommandResult result)
    {
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

    private static ProcessStartInfo CreateStartInfo(string fileName)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private static CommandResult RunProcess(ProcessStartInfo psi, int timeoutMs, string displayCommand)
    {
        Logger.Log($"Executing command: {displayCommand}", "CMD_START");
        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                Logger.Log($"Failed to start process: {psi.FileName}", "CMD_ERROR");
                return new CommandResult(false, false, null, string.Empty, string.Empty);
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                Task<string> errorTask = process.StandardError.ReadToEndAsync(cts.Token);
                process.WaitForExitAsync(cts.Token).GetAwaiter().GetResult();

                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();

                Logger.Log($"Command finished. ExitCode: {process.ExitCode}. OutputLen: {output.Length}. ErrorLen: {error.Length}", "CMD_END");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Logger.Log($"Command stderr: {error}", "CMD_STDERR");
                }
                return new CommandResult(true, false, process.ExitCode, output, error);
            }
            catch (OperationCanceledException)
            {
                Logger.Log($"Command timed out ({timeoutMs}ms): {displayCommand}", "CMD_TIMEOUT");
                TryKillProcess(process);
                return new CommandResult(true, true, null, string.Empty, string.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception running command {psi.FileName}: {ex.Message}", "CMD_EXCEPTION");
            return new CommandResult(false, false, null, string.Empty, ex.Message);
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to kill timed out process: {ex.Message}", "CMD_ERROR");
        }
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
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception in RunCommandNoWait: {ex.Message}", "CMD_ASYNC_ERROR");
        }
    }
}
