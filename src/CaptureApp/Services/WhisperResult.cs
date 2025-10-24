namespace CaptureApp.Services;

public sealed record WhisperResult(string TranscriptPath, int ExitCode, string Output);
