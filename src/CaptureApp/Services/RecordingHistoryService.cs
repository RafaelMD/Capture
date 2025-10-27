using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CaptureApp.Models;

namespace CaptureApp.Services;

public class RecordingHistoryService
{
    private readonly string _recordingsDirectory;
    private readonly string _transcriptsDirectory;

    public RecordingHistoryService(string recordingsDirectory)
    {
        _recordingsDirectory = recordingsDirectory;
        _transcriptsDirectory = Path.Combine(recordingsDirectory, "Transcripts");
    }

    public List<RecordingHistoryItem> LoadHistory()
    {
        var historyItems = new List<RecordingHistoryItem>();

        if (!Directory.Exists(_transcriptsDirectory))
        {
            return historyItems;
        }

        // Find all JSON metadata files (only completed recordings have JSON files)
        var jsonFiles = Directory.GetFiles(_transcriptsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                                  .OrderByDescending(f => File.GetCreationTime(f));

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(jsonFile);
                var historyItem = new RecordingHistoryItem
                {
                    MetadataFilePath = jsonFile,
                    DateTime = File.GetCreationTime(jsonFile)
                };

                // Load metadata from JSON
                try
                {
                    var jsonContent = File.ReadAllText(jsonFile);
                    var metadata = JsonSerializer.Deserialize<RecordingMetadata>(jsonContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (metadata != null)
                    {
                        historyItem.Name = metadata.Name;
                        historyItem.DateTime = metadata.DateTime;
                        
                        if (!string.IsNullOrWhiteSpace(metadata.Transcription))
                        {
                            historyItem.TranscriptionPreview = metadata.Transcription;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                    continue; // Skip invalid JSON files
                }

                // Look for corresponding transcript SRT file
                var transcriptFile = Path.Combine(_transcriptsDirectory, fileName + ".srt");
                if (File.Exists(transcriptFile))
                {
                    historyItem.TranscriptFilePath = transcriptFile;
                }

                // Look for corresponding audio file
                var audioFile = Path.Combine(_recordingsDirectory, fileName + ".wav");
                if (File.Exists(audioFile))
                {
                    historyItem.AudioFilePath = audioFile;
                }

                // Set default name if not loaded from metadata
                if (string.IsNullOrWhiteSpace(historyItem.Name))
                {
                    historyItem.Name = fileName.Replace("capture_", "Recording ");
                }

                historyItems.Add(historyItem);
            }
            catch (Exception ex)
            {
                // Log and skip problematic files
                System.Diagnostics.Debug.WriteLine($"Error loading history item: {ex.Message}");
            }
        }

        return historyItems;
    }

    private static string CleanSrtContent(string srtContent)
    {
        if (string.IsNullOrWhiteSpace(srtContent))
            return string.Empty;

        // Parse SRT format and extract only the text
        var lines = srtContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var textLines = new List<string>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Skip sequence numbers and timecodes
            if (string.IsNullOrEmpty(line) || 
                int.TryParse(line, out _) || 
                line.Contains("-->"))
            {
                continue;
            }
            
            textLines.Add(line);
        }

        return string.Join(" ", textLines);
    }
}
