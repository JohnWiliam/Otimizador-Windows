using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        return ToLegacyOutput(result);
    }

    public static string RunCommand(string fileName, IEnumerable<string> arguments, int timeoutMs = 5000)
    {
        var result = RunCommandDetailed(fileName, arguments, timeoutMs);
        return ToLegacyOutput(result);
    }

    public static CommandResult RunCommandDetailed(string fileName, string arguments, int timeoutMs = 5000)
    {
        var psi = CreateProcessStartInfo(fileName);
        psi.Arguments = arguments;
        return RunProcess(psi, timeoutMs, arguments);
    }

    public static CommandResult RunCommandDetailed(string fileName, IEnumerable<string> arguments, int timeoutMs = 5000)
    {
        var argumentList = arguments.ToArray();
        var psi = CreateProcessStartInfo(fileName);
        foreach (var argument in argumentList)
        {
            psi.ArgumentList.Add(argument);
        }

        return RunProcess(psi, timeoutMs, string.Join(" ", argumentList.Select(RedactForLog)));
    }

    public static async Task<CommandResult> RunCommandDetailedAsync(string fileName, string arguments, int timeoutMs = 5000)
    {
        var psi = CreateProcessStartInfo(fileName);
        psi.Arguments = arguments;
        return await RunProcessAsync(psi, timeoutMs, arguments);
    }

    public static async Task<CommandResult> RunCommandDetailedAsync(string fileName, IEnumerable<string> arguments, int timeoutMs = 5000)
    {
        var argumentList = arguments.ToArray();
        var psi = CreateProcessStartInfo(fileName);
        foreach (var argument in argumentList)
        {
            psi.ArgumentList.Add(argument);
        }

        return await RunProcessAsync(psi, timeoutMs, string.Join(" ", argumentList.Select(RedactForLog)));
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

    private static ProcessStartInfo CreateProcessStartInfo(string fileName) => new()
    {
        FileName = fileName,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    private static CommandResult RunProcess(ProcessStartInfo psi, int timeoutMs, string argumentsForLog)
    {
        try
        {
            return RunProcessAsync(psi, timeoutMs, argumentsForLog).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception running command {psi.FileName}: {ex.Message}", "CMD_EXCEPTION");
            return new CommandResult(false, false, null, string.Empty, ex.Message);
        }
    }

    private static async Task<CommandResult> RunProcessAsync(ProcessStartInfo psi, int timeoutMs, string argumentsForLog)
    {
        Logger.Log($"Executing command: {psi.FileName} {argumentsForLog}", "CMD_START");
        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                Logger.Log($"Failed to start process: {psi.FileName}", "CMD_ERROR");
                return new CommandResult(false, false, null, string.Empty, string.Empty);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();
            var timeoutTask = Task.Delay(timeoutMs);

            if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
            {
                Logger.Log($"Command timed out ({timeoutMs}ms): {psi.FileName} {argumentsForLog}", "CMD_TIMEOUT");
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception kEx)
                {
                    Logger.Log($"Failed to kill timed out process: {kEx.Message}", "CMD_ERROR");
                }

                string timeoutStdOut = outputTask.IsCompletedSuccessfully ? outputTask.Result : string.Empty;
                string timeoutStdErr = errorTask.IsCompletedSuccessfully ? errorTask.Result : string.Empty;
                return new CommandResult(true, true, null, timeoutStdOut, timeoutStdErr);
            }

            await Task.WhenAll(outputTask, errorTask);
            string output = outputTask.Result;
            string error = errorTask.Result;

            Logger.Log($"Command finished. ExitCode: {process.ExitCode}. OutputLen: {output.Length}. ErrorLen: {error.Length}", "CMD_END");
            if (!string.IsNullOrWhiteSpace(error))
            {
                Logger.Log($"Command stderr: {error}", "CMD_STDERR");
            }

            return new CommandResult(true, false, process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception running command {psi.FileName}: {ex.Message}", "CMD_EXCEPTION");
            return new CommandResult(false, false, null, string.Empty, ex.Message);
        }
    }

    private static string ToLegacyOutput(CommandResult result)
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

    private static string RedactForLog(string argument)
    {
        if (argument.Length <= 128)
        {
            return argument;
        }

        return argument[..128] + "...";
    }
}
