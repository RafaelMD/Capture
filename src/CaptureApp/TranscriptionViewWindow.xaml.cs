using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using CaptureApp.Models;

namespace CaptureApp;

public partial class TranscriptionViewWindow : Window
{
    private readonly RecordingHistoryItem _recordingItem;

    public TranscriptionViewWindow(RecordingHistoryItem recordingItem)
    {
        InitializeComponent();
        _recordingItem = recordingItem ?? throw new ArgumentNullException(nameof(recordingItem));
        LoadTranscriptionData();
    }

    private void LoadTranscriptionData()
    {
        try
        {
            // Set title and date
            TitleTextBlock.Text = _recordingItem.Name;
            DateTextBlock.Text = _recordingItem.DateTime.ToString("MMMM dd, yyyy 'at' HH:mm");

            // Load metadata if available
            if (!string.IsNullOrWhiteSpace(_recordingItem.MetadataFilePath) && 
                File.Exists(_recordingItem.MetadataFilePath))
            {
                var jsonContent = File.ReadAllText(_recordingItem.MetadataFilePath);
                var metadata = JsonSerializer.Deserialize<RecordingMetadata>(jsonContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (metadata != null)
                {
                    MeetingNameTextBlock.Text = metadata.Name;

                    // Display tags
                    if (metadata.Tags != null && metadata.Tags.Any())
                    {
                        TagsTextBlock.Text = string.Join(", ", metadata.Tags.Select(t => $"#{t}"));
                        TagsTextBlock.Foreground = System.Windows.Media.Brushes.Black;
                    }
                }
            }
            else
            {
                MeetingNameTextBlock.Text = _recordingItem.Name;
            }

            // Load transcription
            if (!string.IsNullOrWhiteSpace(_recordingItem.TranscriptionPreview))
            {
                TranscriptionTextBox.Text = _recordingItem.TranscriptionPreview;
            }
            else
            {
                TranscriptionTextBox.Text = "No transcription available for this recording.";
            }
        }
        catch (Exception ex)
        {
            TranscriptionTextBox.Text = $"Error loading transcription: {ex.Message}";
        }
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(TranscriptionTextBox.Text))
            {
                Clipboard.SetText(TranscriptionTextBox.Text);
                MessageBox.Show("Transcription copied to clipboard!", "Copy Successful", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy: {ex.Message}", "Copy Failed", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenFileClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            // Try to open the transcript file if available
            if (!string.IsNullOrWhiteSpace(_recordingItem.TranscriptFilePath) && 
                File.Exists(_recordingItem.TranscriptFilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _recordingItem.TranscriptFilePath,
                    UseShellExecute = true
                });
            }
            else if (!string.IsNullOrWhiteSpace(_recordingItem.MetadataFilePath) && 
                     File.Exists(_recordingItem.MetadataFilePath))
            {
                // Fall back to opening the metadata file location
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_recordingItem.MetadataFilePath}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("No file available to open.", "File Not Found", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open file: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
