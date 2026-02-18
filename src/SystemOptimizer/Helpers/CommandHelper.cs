using System;
using System.Diagnostics;
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

        if (!result.Started || result.TimedOut)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(result.StdErr) && string.IsNullOrWhiteSpace(result.StdOut) && result.ExitCode != 0)
        {
            Logger.Log($"Command returned error: {result.StdErr}", "CMD_STDERR");
            return $"[ERRO CMD] {result.StdErr}";
        }

        return result.StdOut;
    }

    public static CommandResult RunCommandDetailed(string fileName, string arguments, int timeoutMs = 5000)
    {
        Logger.Log($"Executing command: {fileName} {arguments}", "CMD_START");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Garante que caracteres especiais (acentos) sejam lidos corretamente
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Logger.Log($"Failed to start process: {fileName}", "CMD_ERROR");
                return new CommandResult(false, false, null, string.Empty, string.Empty);
            }

            // Leitura assíncrona para evitar Deadlocks
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Aguarda a saída do processo com timeout
            if (!process.WaitForExit(timeoutMs))
            {
                Logger.Log($"Command timed out ({timeoutMs}ms): {fileName} {arguments}", "CMD_TIMEOUT");
                try
                {
                    process.Kill();
                }
                catch (Exception kEx)
                {
                    Logger.Log($"Failed to kill timed out process: {kEx.Message}", "CMD_ERROR");
                }
                string timeoutStdOut = outputTask.IsCompletedSuccessfully ? outputTask.Result : string.Empty;
                string timeoutStdErr = errorTask.IsCompletedSuccessfully ? errorTask.Result : string.Empty;
                return new CommandResult(true, true, null, timeoutStdOut, timeoutStdErr);
            }

            // Se o processo terminou, aguardamos as tarefas de leitura terminarem
            Task.WaitAll(outputTask, errorTask);

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
            Logger.Log($"Exception running command {fileName}: {ex.Message}", "CMD_EXCEPTION");
            return new CommandResult(false, false, null, string.Empty, ex.Message);
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
