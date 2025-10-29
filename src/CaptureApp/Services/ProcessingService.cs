using System.Collections.ObjectModel;
using CaptureApp.Models;

namespace CaptureApp.Services;

public class ProcessingService
{
    public ObservableCollection<ProcessingItem> ProcessingQueue { get; } = new();

    public ProcessingItem AddToQueue(string name, string audioFilePath)
    {
        var item = new ProcessingItem
        {
            Name = name,
            DateTime = System.DateTime.Now,
            AudioFilePath = audioFilePath
        };
        
        ProcessingQueue.Add(item);
        return item;
    }

    public void RemoveFromQueue(ProcessingItem item)
    {
        ProcessingQueue.Remove(item);
    }
}
