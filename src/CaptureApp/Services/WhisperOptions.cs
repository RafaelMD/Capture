namespace CaptureApp.Services;

public sealed class WhisperOptions
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string ModelPath { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public string? AdditionalArguments { get; set; }
}
