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
    private IConfiguration _configuration = null!;
    private string _whisperExecutablePath = null!;
    private string _whisperModel = null!;
    private readonly RecordingHistoryService _historyService;
    private readonly ProcessingService _processingService = new();
    private ProcessingItem? _currentProcessingItem;

    public MainWindow()
    {
        InitializeComponent();

        // Load configuration
        LoadConfiguration();

        // Resolve relative path to absolute
        if (!Path.IsPathRooted(_whisperExecutablePath))
        {
            _whisperExecutablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _whisperExecutablePath);
        }

        _recordingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CaptureRecordings");
        Directory.CreateDirectory(_recordingsDirectory);

        // Initialize history service
        _historyService = new RecordingHistoryService(_recordingsDirectory);
        LoadHistory();

        // Initialize processing list
        ProcessingListBox.ItemsSource = _processingService.ProcessingQueue;

        // Initialize status bar
        UpdateStatusBar();

        // Log Whisper configuration
        if (File.Exists(_whisperExecutablePath))
        {
            UpdateStatus("Ready");
        }
        else
        {
            UpdateStatus("WARNING: Whisper executable not found");
        }

        _recorder.Status += (_, message) => Dispatcher.Invoke(() => {
            // Status messages are now handled by explicit status updates
        });
        _recorder.RecordingStopped += (_, path) => Dispatcher.Invoke(() =>
        {
            _lastRecordingPath = path;
            
            // Capture values before clearing
            var meetingName = string.IsNullOrWhiteSpace(MeetingNameTextBox.Text) 
                ? $"Recording {DateTime.Now:yyyy-MM-dd HH:mm}" 
                : MeetingNameTextBox.Text;
            var tags = TagsTextBox.Text;
            var notes = NotesTextBox.Text;
            var language = GetConfiguredLanguage();
            
            // Immediately unlock UI for new recording
            RecordButton.IsEnabled = true;
            RecordButtonIcon.Text = "▶";
            RecordButtonIcon.Foreground = System.Windows.Media.Brushes.Green;
            RecordButtonText.Text = "Start";
            UpdateStatus("Ready for new recording");
            
            // Clear the fields for next recording
            MeetingNameTextBox.Clear();
            TagsTextBox.Clear();
            NotesTextBox.Clear();
            
            // Start transcription in background (non-blocking)
            if (File.Exists(path))
            {
                // Fire and forget - transcription happens in background
                _ = StartTranscriptionAsync(meetingName, tags, notes, language);
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

    private void LoadConfiguration()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Read Whisper settings from configuration
        _whisperExecutablePath = _configuration["Whisper:ExecutablePath"] ?? "whisper/faster-whisper-xxl.exe";
        _whisperModel = _configuration["Whisper:Model"] ?? "medium";
        
        // Update status bar if already initialized
        UpdateStatusBar();
    }

    private LanguageOption GetConfiguredLanguage()
    {
        var languageCode = _configuration["Whisper:Language"] ?? "pt";
        return WhisperLanguageCatalog.Languages.FirstOrDefault(l => l.Code == languageCode)
               ?? WhisperLanguageCatalog.Languages.First();
    }

    private void UpdateStatusBar()
    {
        if (LanguageStatusText != null)
        {
            var language = GetConfiguredLanguage();
            LanguageStatusText.Text = language.DisplayName;
        }

        if (MicrophoneStatusText != null)
        {
            // Get default recording device
            try
            {
                var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.Role.Multimedia);
                MicrophoneStatusText.Text = device?.FriendlyName ?? "Default Device";
            }
            catch
            {
                MicrophoneStatusText.Text = "Default Device";
            }
        }

        if (AudioOutputStatusText != null)
        {
            // Get default playback device
            try
            {
                var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
                AudioOutputStatusText.Text = device?.FriendlyName ?? "Default Device";
            }
            catch
            {
                AudioOutputStatusText.Text = "Default Device";
            }
        }
    }

    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void SaveRecordingMetadata(string transcriptPath, string meetingName, string tagsText, string notes, ProcessingItem? processingItem = null)
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
        var tags = ParseHashtags(tagsText);

        // Create metadata object
        var metadata = new RecordingMetadata
        {
            Name = string.IsNullOrWhiteSpace(meetingName) ? "Untitled Meeting" : meetingName,
            Transcription = transcription,
            DateTime = DateTime.Now,
            Tags = tags,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
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
                    Dispatcher.Invoke(() => processingItem?.AddLogMessage("Syncing with Jaboo..."));
                    
                    using var jabooSync = new JabooSyncService();
                    var success = await jabooSync.SyncAsync(apiUrl, apiKey, metadata);
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (success)
                        {
                            processingItem?.AddLogMessage("Sync completed successfully");
                        }
                        else
                        {
                            var errorMsg = !string.IsNullOrWhiteSpace(jabooSync.LastError) 
                                ? $"Sync failed: {jabooSync.LastError}" 
                                : "Sync failed - check API settings";
                            processingItem?.AddLogMessage(errorMsg);
                            
                            // Log request body for debugging
                            if (!string.IsNullOrWhiteSpace(jabooSync.LastRequestBody))
                            {
                                processingItem?.AddLogMessage($"Request body: {jabooSync.LastRequestBody}");
                            }
                        }
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => processingItem?.AddLogMessage("Jaboo API URL or Key not configured"));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => processingItem?.AddLogMessage($"Sync error: {ex.Message}"));
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
            // Log error but don't update main status - it's a background operation
            System.Diagnostics.Debug.WriteLine($"Failed to load history: {ex.Message}");
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

    private void OnProcessingSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProcessingListBox.SelectedItem is ProcessingItem item)
        {
            // Open processing log window
            try
            {
                var logWindow = new ProcessingLogWindow(item)
                {
                    Owner = this
                };
                logWindow.Show(); // Use Show instead of ShowDialog so user can continue working
                
                // Clear selection
                ProcessingListBox.SelectedItem = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open processing log: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnNewRecordingClicked(object sender, RoutedEventArgs e)
    {
        // Clear selection
        HistoryListBox.SelectedItem = null;
        
        // Focus on the meeting name textbox for user convenience
        MeetingNameTextBox.Focus();
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
        var settingsWindow = new SettingsWindow(_configuration, _lastRecordingPath, () =>
        {
            // Reload configuration when settings are saved
            LoadConfiguration();
        })
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

            // Start recording (language is captured when recording stops)
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

    private async Task StartTranscriptionAsync(string meetingName, string tags, string notes, LanguageOption language)
    {
        if (_lastRecordingPath is null || !File.Exists(_lastRecordingPath))
        {
            // Don't update main status - just log to processing item
            return;
        }

        // Read settings from configuration
        var outputDirectory = _configuration["Whisper:OutputFolder"];
        var extraArgs = _configuration["Whisper:ExtraArguments"];

        if (!File.Exists(_whisperExecutablePath))
        {
            // Don't update main status - log error in debug
            System.Diagnostics.Debug.WriteLine($"Whisper executable not found: {_whisperExecutablePath}");
            return;
        }

        // Add to processing queue
        ProcessingItem? processingItem = null;
        Dispatcher.Invoke(() => 
        {
            processingItem = _processingService.AddToQueue(meetingName, _lastRecordingPath);
            _currentProcessingItem = processingItem; // Keep reference for removal
            processingItem.AddLogMessage("Starting transcription...");
            processingItem.AddLogMessage($"Audio file: {Path.GetFileName(_lastRecordingPath)}");
            processingItem.AddLogMessage($"Language: {language.DisplayName}");
            if (!string.IsNullOrWhiteSpace(tags))
            {
                processingItem.AddLogMessage($"Tags: {tags}");
            }
        });

        try
        {
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

            Dispatcher.Invoke(() => 
            {
                processingItem?.AddLogMessage($"Using model: {_whisperModel}");
                processingItem?.AddLogMessage("Transcribing audio...");
            });

            var transcriber = new WhisperTranscriber(options);
            var progress = new Progress<string>(line => 
            {
                Dispatcher.Invoke(() => processingItem?.AddLogMessage(line));
            });
            var result = await transcriber.TranscribeAsync(_lastRecordingPath, language.Code, progress, _transcriptionCts.Token)
                                          .ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (result.ExitCode == 0)
                {
                    if (File.Exists(result.TranscriptPath))
                    {
                        processingItem?.AddLogMessage("Transcription completed successfully");
                        processingItem?.AddLogMessage($"Output file: {Path.GetFileName(result.TranscriptPath)}");
                        
                        // Save metadata JSON
                        try
                        {
                            SaveRecordingMetadata(result.TranscriptPath, meetingName, tags, notes, processingItem);
                            processingItem?.AddLogMessage("Metadata saved");
                            
                            // Update status
                            if (processingItem != null)
                            {
                                processingItem.Status = "Completed";
                            }
                            
                            // Refresh history after successful transcription
                            LoadHistory();
                            
                            // Remove from processing queue after a short delay
                            Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(() =>
                            {
                                if (_currentProcessingItem != null)
                                {
                                    _processingService.RemoveFromQueue(_currentProcessingItem);
                                }
                            }));
                        }
                        catch (Exception ex)
                        {
                            processingItem?.AddLogMessage($"Error saving metadata: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Failed to save metadata: {ex.Message}");
                        }
                    }
                    else
                    {
                        processingItem?.AddLogMessage($"Error: Transcript file not found");
                        // Don't update main status - it might be recording
                    }
                }
                else
                {
                    processingItem?.AddLogMessage($"Transcription failed with exit code {result.ExitCode}");
                    // Don't update main status - it might be recording
                    
                    if (processingItem != null)
                    {
                        processingItem.Status = "Failed";
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Invoke(() => 
            {
                processingItem?.AddLogMessage("Transcription cancelled");
                if (processingItem != null)
                {
                    processingItem.Status = "Cancelled";
                }
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                processingItem?.AddLogMessage($"Error: {ex.Message}");
                if (processingItem != null)
                {
                    processingItem.Status = "Failed";
                }
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
