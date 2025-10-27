using System;
using System.Collections.Generic;

namespace CaptureApp.Models;

public class RecordingMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Transcription { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public List<string> Tags { get; set; } = new();
}
