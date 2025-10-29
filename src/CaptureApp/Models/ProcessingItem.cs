using System;
using System.Collections.ObjectModel;

namespace CaptureApp.Models;

public class ProcessingItem
{
    public string Name { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string AudioFilePath { get; set; } = string.Empty;
    public ObservableCollection<string> LogMessages { get; set; } = new();
    public string Status { get; set; } = "Processing...";
    
    public string DisplayDate => DateTime.ToString("MMM dd, yyyy");
    public string DisplayTime => DateTime.ToString("HH:mm");
    
    public void AddLogMessage(string message)
    {
        LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
