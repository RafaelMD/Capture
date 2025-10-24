namespace Capture.Models;

/// <summary>
/// Options for Whisper transcription
/// </summary>
public class TranscriptionOptions
{
    /// <summary>
    /// Language code for transcription (e.g., "en", "es", "fr")
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Whisper model size to use
    /// </summary>
    public WhisperModelSize ModelSize { get; set; } = WhisperModelSize.Base;

    /// <summary>
    /// Path to the audio file to transcribe
    /// </summary>
    public string AudioFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to translate to English (instead of transcribing in original language)
    /// </summary>
    public bool TranslateToEnglish { get; set; } = false;
}

/// <summary>
/// Whisper model sizes with trade-offs between accuracy and speed
/// </summary>
public enum WhisperModelSize
{
    /// <summary>
    /// Tiny model (~75 MB) - Fastest, least accurate
    /// </summary>
    Tiny,

    /// <summary>
    /// Base model (~142 MB) - Good balance for CPU
    /// </summary>
    Base,

    /// <summary>
    /// Small model (~466 MB) - Better accuracy
    /// </summary>
    Small,

    /// <summary>
    /// Medium model (~1.5 GB) - High accuracy
    /// </summary>
    Medium,

    /// <summary>
    /// Large model (~2.9 GB) - Best accuracy, slowest
    /// </summary>
    Large
}
