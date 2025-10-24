using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureApp.Services;

public sealed class WhisperTranscriber
{
    private readonly WhisperOptions _options;

    public WhisperTranscriber(WhisperOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<WhisperResult> TranscribeAsync(
        string audioFile,
        string languageCode,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(audioFile))
        {
            throw new ArgumentException("Audio file path is required.", nameof(audioFile));
        }

        if (!File.Exists(audioFile))
        {
            throw new FileNotFoundException("Audio file not found.", audioFile);
        }

        if (string.IsNullOrWhiteSpace(_options.ExecutablePath))
        {
            throw new InvalidOperationException("Whisper executable path is not configured.");
        }

        if (!File.Exists(_options.ExecutablePath))
        {
            throw new FileNotFoundException("Whisper executable not found.", _options.ExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(_options.ModelPath))
        {
            throw new InvalidOperationException("Whisper model path is not configured.");
        }

        if (!File.Exists(_options.ModelPath))
        {
            throw new FileNotFoundException("Whisper model not found.", _options.ModelPath);
        }

        var outputDirectory = string.IsNullOrWhiteSpace(_options.OutputDirectory)
            ? Path.GetDirectoryName(audioFile) ?? Environment.CurrentDirectory
            : _options.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);

        var outputPrefix = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(audioFile) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        var arguments = BuildArguments(audioFile, languageCode, outputPrefix);

        var psi = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var outputBuilder = new StringBuilder();
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var completionSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                progress?.Report(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                progress?.Report(e.Data);
            }
        };

        process.Exited += (_, _) => completionSource.TrySetResult(process.ExitCode);

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start Whisper process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using (cancellationToken.Register(() =>
               {
                   try
                   {
                       if (!process.HasExited)
                       {
                           process.Kill(true);
                       }
                   }
                   catch
                   {
                       // ignored
                   }
                   completionSource.TrySetCanceled(cancellationToken);
               }))
        {
            var exitCode = await completionSource.Task.ConfigureAwait(false);
            var transcriptPath = outputPrefix + ".txt";
            return new WhisperResult(transcriptPath, exitCode, outputBuilder.ToString());
        }
    }

    private string BuildArguments(string audioFile, string languageCode, string outputPrefix)
    {
        var builder = new StringBuilder();
        builder.Append(" -m \"");
        builder.Append(_options.ModelPath);
        builder.Append("\"");
        builder.Append(" -l ");
        builder.Append(languageCode);
        builder.Append(" -f \"");
        builder.Append(audioFile);
        builder.Append("\"");
        builder.Append(" -otxt");
        builder.Append(" -of \"");
        builder.Append(outputPrefix);
        builder.Append("\"");

        if (!string.IsNullOrWhiteSpace(_options.AdditionalArguments))
        {
            builder.Append(' ');
            builder.Append(_options.AdditionalArguments);
        }

        return builder.ToString();
    }
}
