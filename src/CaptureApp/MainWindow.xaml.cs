using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CaptureApp.Models;
using CaptureApp.Services;
using NAudio.Wave;
using Microsoft.Extensions.Configuration;

namespace CaptureApp;

public partial class MainWindow : Window
{
    private readonly AudioRecorder _recorder = new();
    private readonly string _recordingsDirectory;
    private CancellationTokenSource? _transcriptionCts;
    private string? _lastRecordingPath;
    private readonly IConfiguration _configuration;
    private readonly string _whisperExecutablePath;
    private readonly string _whisperModel;
    private readonly RecordingHistoryService _historyService;

    public MainWindow()
    {
        InitializeComponent();

        // Load configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Read Whisper settings from configuration
        _whisperExecutablePath = _configuration["Whisper:ExecutablePath"] ?? "whisper/faster-whisper-xxl.exe";
        _whisperModel = _configuration["Whisper:Model"] ?? "medium";

        // Resolve relative path to absolute
        if (!Path.IsPathRooted(_whisperExecutablePath))
        {
            _whisperExecutablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _whisperExecutablePath);
        }

        LanguageComboBox.ItemsSource = WhisperLanguageCatalog.Languages;
        LanguageComboBox.SelectedItem = WhisperLanguageCatalog.Languages.FirstOrDefault(l => l.Code == "pt")
                                         ?? WhisperLanguageCatalog.Languages.First();

        _recordingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CaptureRecordings");
        Directory.CreateDirectory(_recordingsDirectory);

        // Initialize history service
        _historyService = new RecordingHistoryService(_recordingsDirectory);
        LoadHistory();

        // Log Whisper configuration
        if (File.Exists(_whisperExecutablePath))
        {
            // Whisper executable found
        }
        else
        {
            UpdateStatus("WARNING: Whisper executable not found");
        }

        _recorder.Status += (_, message) => Dispatcher.Invoke(() => {
            // Status messages are now handled by explicit status updates
        });
        _recorder.RecordingStopped += (_, path) => Dispatcher.Invoke(async () =>
        {
            _lastRecordingPath = path;
            UpdateStatus("Recording stopped");
            
            // Reset button to Start state
            RecordButton.IsEnabled = true;
            RecordButtonIcon.Text = "▶";
            RecordButtonIcon.Foreground = System.Windows.Media.Brushes.Green;
            RecordButtonText.Text = "Start";
            
            // Automatically start transcription
            if (File.Exists(path))
            {
                UpdateStatus("Waiting for transcription");
                await Task.Delay(500); // Small delay for better UX
                await StartTranscriptionAsync();
            }
        });
        _recorder.RecordingFailed += (_, ex) => Dispatcher.Invoke(() =>
        {
            UpdateStatus($"Recording failed: {ex.Message}");
            
            // Reset button to Start state
            RecordButton.IsEnabled = true;
            RecordButtonIcon.Text = "▶";
            RecordButtonIcon.Foreground = System.Windows.Media.Brushes.Green;
            RecordButtonText.Text = "Start";
        });
    }

    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void SaveRecordingMetadata(string transcriptPath)
    {
        if (_lastRecordingPath is null)
            return;

        // Read transcription content
        var transcription = string.Empty;
        if (File.Exists(transcriptPath))
        {
            transcription = File.ReadAllText(transcriptPath);
        }

        // Parse tags from hashtags
        var tagsText = string.Empty;
        Dispatcher.Invoke(() => tagsText = TagsTextBox.Text);
        var tags = ParseHashtags(tagsText);

        // Get meeting name
        var meetingName = string.Empty;
        Dispatcher.Invoke(() => meetingName = MeetingNameTextBox.Text);

        // Create metadata object
        var metadata = new RecordingMetadata
        {
            Name = string.IsNullOrWhiteSpace(meetingName) ? "Untitled Meeting" : meetingName,
            Transcription = transcription,
            DateTime = DateTime.Now,
            Tags = tags
        };

        // Save to JSON file (same name as transcript but .json extension)
        var jsonPath = Path.ChangeExtension(transcriptPath, ".json");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var jsonContent = JsonSerializer.Serialize(metadata, jsonOptions);
        File.WriteAllText(jsonPath, jsonContent);

        // Sync with Jaboo
        _ = Task.Run(async () =>
        {
            try
            {
                var apiUrl = _configuration["Jaboo:ApiUrl"];
                var apiKey = _configuration["Jaboo:ApiKey"];

                if (!string.IsNullOrWhiteSpace(apiUrl) && !string.IsNullOrWhiteSpace(apiKey))
                {
                    Dispatcher.Invoke(() => UpdateStatus("Syncing with Jaboo..."));
                    
                    using var jabooSync = new JabooSyncService();
                    var success = await jabooSync.SyncAsync(apiUrl, apiKey, metadata);
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (success)
                        {
                            UpdateStatus("Sync completed successfully");
                        }
                        else
                        {
                            UpdateStatus("Sync failed - check API settings");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Sync error: {ex.Message}"));
            }
        });
    }

    private void LoadHistory()
    {
        try
        {
            var history = _historyService.LoadHistory();
            HistoryListBox.ItemsSource = history;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to load history: {ex.Message}");
        }
    }

    private void OnHistorySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (HistoryListBox.SelectedItem is RecordingHistoryItem item)
        {
            // Open transcription view window
            try
            {
                var transcriptionWindow = new TranscriptionViewWindow(item)
                {
                    Owner = this
                };
                transcriptionWindow.ShowDialog();
                
                // Clear selection after closing the window
                HistoryListBox.SelectedItem = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open transcription: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnNewRecordingClicked(object sender, RoutedEventArgs e)
    {
        // Clear any selection in history
        HistoryListBox.SelectedItem = null;
        
        // Focus on the meeting name textbox for user convenience
        MeetingNameTextBox.Focus();
        
        // Optionally scroll to top of the page
        UpdateStatus("Ready to start new recording");
    }

    private static List<string> ParseHashtags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Match hashtags: # followed by word characters
        var matches = Regex.Matches(text, @"#(\w+)");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_configuration, _lastRecordingPath)
        {
            Owner = this
        };
        settingsWindow.ShowDialog();
    }

    private async void OnRecordClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            // If currently recording, stop it
            if (_recorder.IsRecording)
            {
                UpdateStatus("Stopping recording...");
                RecordButton.IsEnabled = false;
                await _recorder.StopRecordingAsync();
                return;
            }

            // Start recording
            var language = (LanguageOption?)LanguageComboBox.SelectedItem ?? WhisperLanguageCatalog.Languages.First();
            Directory.CreateDirectory(_recordingsDirectory);
            var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            var fullPath = Path.Combine(_recordingsDirectory, fileName);

            _recorder.StartRecording(fullPath);
            _lastRecordingPath = fullPath;

            // Change button to Stop state
            RecordButtonIcon.Text = "⏹";
            RecordButtonIcon.Foreground = System.Windows.Media.Brushes.Red;
            RecordButtonText.Text = "Stop";
            
            UpdateStatus($"Recording...");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to start recording: {ex.Message}");
            
            // Reset button to Start state
            RecordButton.IsEnabled = true;
            RecordButtonIcon.Text = "▶";
            RecordButtonIcon.Foreground = System.Windows.Media.Brushes.Green;
            RecordButtonText.Text = "Start";
        }
    }

    private async Task StartTranscriptionAsync()
    {
        if (_lastRecordingPath is null || !File.Exists(_lastRecordingPath))
        {
            UpdateStatus("No recording available to transcribe.");
            return;
        }

        // Read settings from configuration
        var outputDirectory = _configuration["Whisper:OutputFolder"];
        var extraArgs = _configuration["Whisper:ExtraArguments"];
        var language = (LanguageOption?)LanguageComboBox.SelectedItem ?? WhisperLanguageCatalog.Languages.First();

        if (!File.Exists(_whisperExecutablePath))
        {
            UpdateStatus($"Whisper executable not found: {_whisperExecutablePath}");
            return;
        }

        try
        {
            RecordButton.IsEnabled = false;
            UpdateStatus("Transcribing...");

            _transcriptionCts = new CancellationTokenSource();
            var options = new WhisperOptions
            {
                ExecutablePath = _whisperExecutablePath,
                ModelPath = _whisperModel,
                OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                    ? Path.Combine(_recordingsDirectory, "Transcripts")
                    : outputDirectory,
                AdditionalArguments = string.IsNullOrWhiteSpace(extraArgs) ? null : extraArgs
            };

            var transcriber = new WhisperTranscriber(options);
            var progress = new Progress<string>(line => { /* Progress updates removed */ });
            var result = await transcriber.TranscribeAsync(_lastRecordingPath, language.Code, progress, _transcriptionCts.Token)
                                          .ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (result.ExitCode == 0)
                {
                    if (File.Exists(result.TranscriptPath))
                    {
                        UpdateStatus($"Transcription completed");
                        
                        // Save metadata JSON
                        try
                        {
                            SaveRecordingMetadata(result.TranscriptPath);
                            
                            // Refresh history after successful transcription
                            LoadHistory();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to save metadata: {ex.Message}");
                        }
                    }
                    else
                    {
                        UpdateStatus($"Transcription completed but file not found: {result.TranscriptPath}");
                    }
                }
                else
                {
                    UpdateStatus($"Transcription failed (exit code {result.ExitCode})");
                }

                RecordButton.IsEnabled = true;
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Invoke(() => UpdateStatus("Transcription cancelled"));
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatus($"Transcription failed: {ex.Message}");
                RecordButton.IsEnabled = true;
            });
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
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
