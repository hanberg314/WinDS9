namespace WinDS9.Core;

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
