using System;
using System.Collections.Specialized;
using System.Windows;
using CaptureApp.Models;

namespace CaptureApp;

public partial class ProcessingLogWindow : Window
{
    private readonly ProcessingItem _processingItem;

    public ProcessingLogWindow(ProcessingItem processingItem)
    {
        InitializeComponent();
        _processingItem = processingItem ?? throw new ArgumentNullException(nameof(processingItem));
        
        LoadProcessingData();
        
        // Subscribe to log updates
        _processingItem.LogMessages.CollectionChanged += OnLogMessagesChanged;
    }

    private void LoadProcessingData()
    {
        TitleTextBlock.Text = _processingItem.Name;
        DateTextBlock.Text = _processingItem.DateTime.ToString("MMMM dd, yyyy 'at' HH:mm");
        StatusTextBlock.Text = _processingItem.Status;
        
        // Bind log messages
        LogItemsControl.ItemsSource = _processingItem.LogMessages;
        
        // Scroll to bottom
        ScrollToBottom();
    }

    private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // When new log messages are added, scroll to bottom
        Dispatcher.Invoke(() =>
        {
            StatusTextBlock.Text = _processingItem.Status;
            ScrollToBottom();
        });
    }

    private void ScrollToBottom()
    {
        LogScrollViewer.ScrollToBottom();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        // Unsubscribe from events
        _processingItem.LogMessages.CollectionChanged -= OnLogMessagesChanged;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Make sure to unsubscribe
        _processingItem.LogMessages.CollectionChanged -= OnLogMessagesChanged;
    }
}
