using System;

namespace CaptureApp.Models;

public class RecordingHistoryItem
{
    public string Name { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string TranscriptionPreview { get; set; } = string.Empty;
    public string AudioFilePath { get; set; } = string.Empty;
    public string TranscriptFilePath { get; set; } = string.Empty;
    public string MetadataFilePath { get; set; } = string.Empty;
    
    public string DisplayDate => DateTime.ToString("MMM dd, yyyy");
    public string DisplayTime => DateTime.ToString("HH:mm");
    public string DisplayPreview => TranscriptionPreview.Length > 100 
        ? TranscriptionPreview.Substring(0, 100) + "..." 
        : TranscriptionPreview;
}
