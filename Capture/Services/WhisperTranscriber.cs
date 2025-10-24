using System.Diagnostics;
using Capture.Models;

namespace Capture.Services;

/// <summary>
/// Handles Whisper transcription using whisper.cpp
/// </summary>
public class WhisperTranscriber
{
    private readonly string _whisperExecutablePath;
    private readonly string _modelsDirectory;

    /// <summary>
    /// Event raised to report transcription progress
    /// </summary>
    public event EventHandler<string>? ProgressUpdated;

    /// <summary>
    /// Event raised when transcription is complete
    /// </summary>
    public event EventHandler<string>? TranscriptionCompleted;

    /// <summary>
    /// Initialize the Whisper transcriber
    /// </summary>
    /// <param name="whisperExecutablePath">Path to whisper.cpp executable (main.exe on Windows)</param>
    /// <param name="modelsDirectory">Directory containing Whisper model files</param>
    public WhisperTranscriber(string whisperExecutablePath, string modelsDirectory)
    {
        _whisperExecutablePath = whisperExecutablePath;
        _modelsDirectory = modelsDirectory;
    }

    /// <summary>
    /// Transcribe an audio file using Whisper
    /// </summary>
    /// <param name="options">Transcription options</param>
    /// <returns>Transcribed text</returns>
    public async Task<string> TranscribeAsync(TranscriptionOptions options)
    {
        if (!File.Exists(options.AudioFilePath))
        {
            throw new FileNotFoundException("Audio file not found", options.AudioFilePath);
        }

        // Get model file path
        var modelPath = GetModelPath(options.ModelSize);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"Whisper model not found. Please download the {options.ModelSize} model and place it in {_modelsDirectory}",
                modelPath);
        }

        // Build whisper.cpp command line arguments
        var arguments = BuildWhisperArguments(options, modelPath);

        ProgressUpdated?.Invoke(this, "Starting transcription...");

        // Run whisper.cpp
        var result = await RunWhisperProcessAsync(arguments);

        TranscriptionCompleted?.Invoke(this, result);
        return result;
    }

    /// <summary>
    /// Get the model file path for a given model size
    /// </summary>
    private string GetModelPath(WhisperModelSize modelSize)
    {
        var modelName = modelSize switch
        {
            WhisperModelSize.Tiny => "ggml-tiny.bin",
            WhisperModelSize.Base => "ggml-base.bin",
            WhisperModelSize.Small => "ggml-small.bin",
            WhisperModelSize.Medium => "ggml-medium.bin",
            WhisperModelSize.Large => "ggml-large-v3.bin",
            _ => "ggml-base.bin"
        };

        return Path.Combine(_modelsDirectory, modelName);
    }

    /// <summary>
    /// Build command line arguments for whisper.cpp
    /// </summary>
    private string BuildWhisperArguments(TranscriptionOptions options, string modelPath)
    {
        var args = new List<string>
        {
            $"-m \"{modelPath}\"",
            $"-f \"{options.AudioFilePath}\"",
            $"-l {options.Language}",
            "--output-txt", // Output as text file
            "--no-timestamps" // Don't include timestamps in output
        };

        if (options.TranslateToEnglish)
        {
            args.Add("--translate");
        }

        return string.Join(" ", args);
    }

    /// <summary>
    /// Run the whisper.cpp process and capture output
    /// </summary>
    private async Task<string> RunWhisperProcessAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _whisperExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                ProgressUpdated?.Invoke(this, e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
                ProgressUpdated?.Invoke(this, e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Whisper transcription failed: {errorBuilder}");
        }

        // Read the output text file (whisper.cpp creates a .txt file next to the audio file)
        var audioFileName = Path.GetFileNameWithoutExtension(Path.GetFileName(arguments.Split('"')[3]));
        var outputTextFile = Path.Combine(
            Path.GetDirectoryName(arguments.Split('"')[3]) ?? "",
            $"{audioFileName}.txt");

        if (File.Exists(outputTextFile))
        {
            var transcription = await File.ReadAllTextAsync(outputTextFile);
            return transcription.Trim();
        }

        return outputBuilder.ToString();
    }

    /// <summary>
    /// Check if Whisper is properly installed and configured
    /// </summary>
    public bool IsWhisperAvailable()
    {
        return File.Exists(_whisperExecutablePath) && Directory.Exists(_modelsDirectory);
    }

    /// <summary>
    /// Get list of available models
    /// </summary>
    public List<WhisperModelSize> GetAvailableModels()
    {
        var availableModels = new List<WhisperModelSize>();

        foreach (WhisperModelSize modelSize in Enum.GetValues(typeof(WhisperModelSize)))
        {
            var modelPath = GetModelPath(modelSize);
            if (File.Exists(modelPath))
            {
                availableModels.Add(modelSize);
            }
        }

        return availableModels;
    }
}
