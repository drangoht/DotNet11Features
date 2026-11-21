using System.Diagnostics;
using System.Text;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 06 · Run a command and capture its output in one line: Process.RunAndCaptureTextAsync
// Documentation: docs/en/06-process.md · docs/fr/06-process.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E06_Process
{
    // ── Before: the correct version takes about twenty lines ────────────────────────────────────
    // The classic trap: reading StandardOutput.ReadToEnd() THEN StandardError.ReadToEnd() can block
    // forever if the program fills the error buffer (~4 KB) while we wait for standard output.
    // Both streams must be read at the same time.
    static async Task<(int Code, string Output, string Errors)> RunBeforeAsync(string program, params string[] arguments)
    {
        var info = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in arguments) info.ArgumentList.Add(a);

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Cannot start {program}");
        Task<string> output = process.StandardOutput.ReadToEndAsync();   // both reads at the same time
        Task<string> errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await errors);
    }

    public static async Task Run()
    {
        Title("06 · Process.RunAndCaptureTextAsync (.NET 11)");

        Section("Before: ProcessStartInfo + redirections + reading both streams in parallel");
        var (code, output, _) = await RunBeforeAsync("dotnet", "--version");
        Line($"code {code} · dotnet --version → {output.Trim()}");

        Section("After: one line");
        ProcessTextOutput result = await Process.RunAndCaptureTextAsync("dotnet", ["--version"]);
        Line($"code {result.ExitStatus.ExitCode} · dotnet --version → {result.StandardOutput.Trim()} (pid {result.ProcessId})");
        Check(result.ExitStatus.ExitCode == 0 && result.StandardOutput.Trim() == output.Trim(), "same result as the version from before");

        Section("Standard error is captured separately, along with the exit code");
        ProcessTextOutput failure = await Process.RunAndCaptureTextAsync("dotnet", ["command-that-does-not-exist"]);
        string firstLine = (failure.StandardError + failure.StandardOutput).Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        Line($"code {failure.ExitStatus.ExitCode} · {firstLine[..Math.Min(90, firstLine.Length)]}");
        Check(failure.ExitStatus.ExitCode != 0, "a failure does not throw: read ExitStatus.ExitCode");

        Section("Cancellation: the process is stopped");
        string[] wait = OperatingSystem.IsWindows()
            ? ["powershell", "-NoProfile", "-Command", "Start-Sleep -Seconds 30"]
            : ["sleep", "30"];
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ProcessTextOutput cancelled = await Process.RunAndCaptureTextAsync(wait[0], wait[1..], timeout.Token);
            Line($"finished in {stopwatch.ElapsedMilliseconds} ms · Canceled = {cancelled.ExitStatus.Canceled}");
            Check(cancelled.ExitStatus.Canceled, "the 30 s process is stopped after 0.5 s");
        }
        catch (OperationCanceledException)
        {
            Line($"OperationCanceledException after {stopwatch.ElapsedMilliseconds} ms");
            Check(stopwatch.ElapsedMilliseconds < 10_000, "the 30 s process is stopped without waiting for it to end");
        }

        Section("Synchronous version with a timeout: Process.Run");
        ProcessExitStatus status = Process.Run("dotnet", ["--info"], silent: true, timeout: TimeSpan.FromSeconds(30));
        Line($"dotnet --info (output discarded, silent: true) → code {status.ExitCode}");
        Check(status.ExitCode == 0, "Process.Run returns the exit status directly");
    }
}
