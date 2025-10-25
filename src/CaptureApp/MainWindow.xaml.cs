using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CaptureApp.Models;
using CaptureApp.Services;
using NAudio.Wave;

namespace CaptureApp;

public partial class MainWindow : Window
{
    private readonly AudioRecorder _recorder = new();
    private readonly string _recordingsDirectory;
    private CancellationTokenSource? _transcriptionCts;
    private string? _lastRecordingPath;
    private IWavePlayer? _waveOutDevice;
    private AudioFileReader? _audioFileReader;

    public MainWindow()
    {
        InitializeComponent();

        LanguageComboBox.ItemsSource = WhisperLanguageCatalog.Languages;
        LanguageComboBox.SelectedItem = WhisperLanguageCatalog.Languages.FirstOrDefault(l => l.Code == "en")
                                         ?? WhisperLanguageCatalog.Languages.First();

        _recordingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CaptureRecordings");
        Directory.CreateDirectory(_recordingsDirectory);

        var defaultTranscripts = Path.Combine(_recordingsDirectory, "Transcripts");
        WhisperOutputTextBox.Text = defaultTranscripts;

        // Log available audio devices on startup
        AppendLog("Checking available audio devices...");
        AppendLog(AudioRecorder.GetAvailableDevicesInfo());

        _recorder.Status += (_, message) => Dispatcher.Invoke(() => AppendLog(message));
        _recorder.RecordingStopped += (_, path) => Dispatcher.Invoke(() =>
        {
            _lastRecordingPath = path;
            LastRecordingTextBlock.Text = path;
            UpdateStatus("Recording stopped.");
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PlayButton.IsEnabled = File.Exists(path);
            TranscribeButton.IsEnabled = File.Exists(path);
        });
        _recorder.RecordingFailed += (_, ex) => Dispatcher.Invoke(() =>
        {
            UpdateStatus($"Recording failed: {ex.Message}");
            AppendLog(ex.ToString());
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        });
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }

    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
        AppendLog(message);
    }

    private void OnRecordClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_recorder.IsRecording)
            {
                return;
            }

            var language = (LanguageOption?)LanguageComboBox.SelectedItem ?? WhisperLanguageCatalog.Languages.First();
            Directory.CreateDirectory(_recordingsDirectory);
            var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            var fullPath = Path.Combine(_recordingsDirectory, fileName);

            _recorder.StartRecording(fullPath);
            _lastRecordingPath = fullPath;
            LastRecordingTextBlock.Text = fullPath;

            RecordButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            TranscribeButton.IsEnabled = false;
            UpdateStatus($"Recording… ({language.DisplayName})");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to start recording: {ex.Message}");
            AppendLog(ex.ToString());
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    private async void OnStopClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatus("Stopping recording...");
            AppendLog("Stop button clicked.");
            StopButton.IsEnabled = false;
            await _recorder.StopRecordingAsync();
            AppendLog("Stop recording completed.");
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatus($"Failed to stop recording: {ex.Message}");
                AppendLog(ex.ToString());
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            });
        }
    }

    private void OnPlayClicked(object sender, RoutedEventArgs e)
    {
        // Toggle playback
        if (_waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            StopPlayback();
            UpdateStatus("Playback stopped.");
            return;
        }

        if (_lastRecordingPath is null || !File.Exists(_lastRecordingPath))
        {
            UpdateStatus("No recording available to play.");
            return;
        }

        try
        {
            // Stop any existing playback
            StopPlayback();

            _audioFileReader = new AudioFileReader(_lastRecordingPath);
            _waveOutDevice = new WaveOutEvent();
            _waveOutDevice.Init(_audioFileReader);
            _waveOutDevice.PlaybackStopped += (s, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("Playback finished.");
                    PlayButton.Content = "Play";
                    StopPlayback();
                });
            };

            _waveOutDevice.Play();
            PlayButton.Content = "Stop";
            UpdateStatus($"Playing: {Path.GetFileName(_lastRecordingPath)}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to play recording: {ex.Message}");
            AppendLog(ex.ToString());
            StopPlayback();
        }
    }

    private void StopPlayback()
    {
        _waveOutDevice?.Stop();
        _waveOutDevice?.Dispose();
        _waveOutDevice = null;
        _audioFileReader?.Dispose();
        _audioFileReader = null;
        PlayButton.Content = "Play";
    }

    private async void OnTranscribeClicked(object sender, RoutedEventArgs e)
    {
        if (_lastRecordingPath is null || !File.Exists(_lastRecordingPath))
        {
            UpdateStatus("No recording available to transcribe.");
            return;
        }

        var executable = WhisperExecutableTextBox.Text;
        var model = WhisperModelTextBox.Text;
        var outputDirectory = WhisperOutputTextBox.Text;
        var extraArgs = WhisperArgsTextBox.Text;
        var language = (LanguageOption?)LanguageComboBox.SelectedItem ?? WhisperLanguageCatalog.Languages.First();

        if (string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(model))
        {
            UpdateStatus("Provide both Whisper executable and model paths.");
            return;
        }

        try
        {
            TranscribeButton.IsEnabled = false;
            RecordButton.IsEnabled = false;
            UpdateStatus("Starting transcription…");

            _transcriptionCts = new CancellationTokenSource();
            var options = new WhisperOptions
            {
                ExecutablePath = executable,
                ModelPath = model,
                OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                    ? Path.Combine(_recordingsDirectory, "Transcripts")
                    : outputDirectory,
                AdditionalArguments = string.IsNullOrWhiteSpace(extraArgs) ? null : extraArgs
            };

            var transcriber = new WhisperTranscriber(options);
            var progress = new Progress<string>(line => Dispatcher.Invoke(() => AppendLog(line)));
            var result = await transcriber.TranscribeAsync(_lastRecordingPath, language.Code, progress, _transcriptionCts.Token)
                                          .ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (result.ExitCode == 0 && File.Exists(result.TranscriptPath))
                {
                    UpdateStatus($"Transcription complete: {result.TranscriptPath}");
                }
                else
                {
                    UpdateStatus($"Transcription finished with exit code {result.ExitCode}. See logs for details.");
                }

                RecordButton.IsEnabled = true;
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Invoke(() => UpdateStatus("Transcription cancelled."));
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatus($"Transcription failed: {ex.Message}");
                AppendLog(ex.ToString());
                RecordButton.IsEnabled = true;
            });
        }
        finally
        {
            Dispatcher.Invoke(() => TranscribeButton.IsEnabled = true);
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopPlayback();
        
        if (_recorder.IsRecording)
        {
            // Force synchronous stop without canceling window close
            try
            {
                _recorder.StopRecordingAsync().Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                // Log but don't prevent closing
                System.Diagnostics.Debug.WriteLine($"Error stopping recording on close: {ex.Message}");
            }
        }
        
        if (_transcriptionCts is not null)
        {
            _transcriptionCts.Cancel();
        }
        
        _recorder.Dispose();
    }
}
