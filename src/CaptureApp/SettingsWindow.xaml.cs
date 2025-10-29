using System;
using System.IO;
using System.Linq;
using System.Windows;
using CaptureApp.Models;
using Microsoft.Extensions.Configuration;

namespace CaptureApp;

public partial class SettingsWindow : Window
{
    private readonly IConfiguration _configuration;
    private readonly string _recordingsDirectory;
    private readonly Action? _onSettingsSaved;

    public SettingsWindow(IConfiguration configuration, string? lastRecordingPath = null, Action? onSettingsSaved = null)
    {
        InitializeComponent();
        _configuration = configuration;
        _onSettingsSaved = onSettingsSaved;
        
        _recordingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
            "CaptureRecordings");

        // Initialize language dropdown
        LanguageComboBox.ItemsSource = WhisperLanguageCatalog.Languages;

        LoadSettings();
        
        // Display last recording if provided
        if (!string.IsNullOrEmpty(lastRecordingPath))
        {
            LastRecordingTextBlock.Text = lastRecordingPath;
        }
    }

    private void LoadSettings()
    {
        // Load current settings
        var executablePath = _configuration["Whisper:ExecutablePath"] ?? "whisper/faster-whisper-xxl.exe";
        var model = _configuration["Whisper:Model"] ?? "medium";
        var outputFolder = _configuration["Whisper:OutputFolder"] ?? Path.Combine(_recordingsDirectory, "Transcripts");
        var languageCode = _configuration["Whisper:Language"] ?? "pt";

        ExecutablePathTextBox.Text = executablePath;
        ModelTextBox.Text = model;
        OutputFolderTextBox.Text = outputFolder;
        ExtraArgsTextBox.Text = _configuration["Whisper:ExtraArguments"] ?? "";
        
        // Set language selection
        var selectedLanguage = WhisperLanguageCatalog.Languages.FirstOrDefault(l => l.Code == languageCode)
                               ?? WhisperLanguageCatalog.Languages.First();
        LanguageComboBox.SelectedItem = selectedLanguage;
        
        // Load Jaboo Integration settings
        ApiUrlTextBox.Text = _configuration["Jaboo:ApiUrl"] ?? "https://izkvewewmtlamozgbqwr.supabase.co/functions/v1/receive-transcription";
        ApiKeyTextBox.Text = _configuration["Jaboo:ApiKey"] ?? "";
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the appsettings.json path
            var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            
            // Read current JSON
            var json = File.ReadAllText(appSettingsPath);
            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            
            if (settings == null)
            {
                settings = new AppSettings { Whisper = new WhisperSettings() };
            }
            
            // Update settings
            settings.Whisper ??= new WhisperSettings();
            settings.Whisper.ExecutablePath = ExecutablePathTextBox.Text;
            settings.Whisper.Model = ModelTextBox.Text;
            settings.Whisper.OutputFolder = OutputFolderTextBox.Text;
            settings.Whisper.ExtraArguments = string.IsNullOrWhiteSpace(ExtraArgsTextBox.Text) 
                ? null 
                : ExtraArgsTextBox.Text;
            
            // Save language selection
            var selectedLanguage = (LanguageOption?)LanguageComboBox.SelectedItem;
            settings.Whisper.Language = selectedLanguage?.Code ?? "pt";
            
            // Update Jaboo Integration settings
            settings.Jaboo ??= new JabooSettings();
            settings.Jaboo.ApiUrl = ApiUrlTextBox.Text;
            settings.Jaboo.ApiKey = string.IsNullOrWhiteSpace(ApiKeyTextBox.Text)
                ? null
                : ApiKeyTextBox.Text;
            
            // Write back to file
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            var updatedJson = System.Text.Json.JsonSerializer.Serialize(settings, options);
            File.WriteAllText(appSettingsPath, updatedJson);
            
            MessageBox.Show("Settings saved successfully!", 
                          "Settings Saved", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Information);
            
            // Notify MainWindow to reload settings
            _onSettingsSaved?.Invoke();
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", 
                          "Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class AppSettings
{
    public WhisperSettings? Whisper { get; set; }
    public JabooSettings? Jaboo { get; set; }
}

public class WhisperSettings
{
    public string? ExecutablePath { get; set; }
    public string? Model { get; set; }
    public string? OutputFolder { get; set; }
    public string? ExtraArguments { get; set; }
    public string? Language { get; set; }
}

public class JabooSettings
{
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
}
