using Capture.Models;
using Capture.Services;

namespace Capture;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("   Capture - Audio Recording & Transcription");
        Console.WriteLine("   with Whisper (Offline ASR)");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        var app = new CaptureApplication();
        await app.RunAsync();
    }
}

class CaptureApplication
{
    private readonly AudioRecorder _audioRecorder;
    private readonly AudioPreprocessor _audioPreprocessor;
    private WhisperTranscriber? _whisperTranscriber;
    private string _recordingsDirectory;
    private string _currentRecordingPath = string.Empty;
    private TranscriptionOptions _transcriptionOptions;

    public CaptureApplication()
    {
        _audioRecorder = new AudioRecorder();
        _audioPreprocessor = new AudioPreprocessor();
        _recordingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Capture", "Recordings");
        Directory.CreateDirectory(_recordingsDirectory);

        _transcriptionOptions = new TranscriptionOptions
        {
            Language = "en",
            ModelSize = WhisperModelSize.Base
        };

        LoadWhisperConfiguration();
    }

    private void LoadWhisperConfiguration()
    {
        // Try to load from environment variables or default paths
        var whisperExe = Environment.GetEnvironmentVariable("WHISPER_EXECUTABLE");
        var modelsDir = Environment.GetEnvironmentVariable("WHISPER_MODELS_DIR");

        if (!string.IsNullOrEmpty(whisperExe) && !string.IsNullOrEmpty(modelsDir))
        {
            _whisperTranscriber = new WhisperTranscriber(whisperExe, modelsDir);
        }
    }

    public async Task RunAsync()
    {
        bool exit = false;

        while (!exit)
        {
            Console.WriteLine();
            Console.WriteLine("Main Menu:");
            Console.WriteLine("1. Start Recording");
            Console.WriteLine("2. Stop Recording");
            Console.WriteLine("3. Transcribe Last Recording");
            Console.WriteLine("4. Configure Whisper");
            Console.WriteLine("5. Set Language");
            Console.WriteLine("6. Set Model Size");
            Console.WriteLine("7. View Recordings");
            Console.WriteLine("8. Exit");
            Console.Write("Select an option: ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    StartRecording();
                    break;
                case "2":
                    StopRecording();
                    break;
                case "3":
                    await TranscribeRecordingAsync();
                    break;
                case "4":
                    ConfigureWhisper();
                    break;
                case "5":
                    SetLanguage();
                    break;
                case "6":
                    SetModelSize();
                    break;
                case "7":
                    ViewRecordings();
                    break;
                case "8":
                    exit = true;
                    break;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }

        _audioRecorder.Dispose();
        Console.WriteLine("Goodbye!");
    }

    private void StartRecording()
    {
        if (_audioRecorder.IsRecording)
        {
            Console.WriteLine("Recording is already in progress.");
            return;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentRecordingPath = Path.Combine(_recordingsDirectory, $"recording_{timestamp}.wav");

            Console.WriteLine("Starting recording... Press Enter to stop.");
            
            // Record at 16kHz mono for Whisper compatibility
            _audioRecorder.StartRecording(_currentRecordingPath, sampleRate: 16000, channels: 1);
            Console.WriteLine($"Recording to: {Path.GetFileName(_currentRecordingPath)}");
            Console.WriteLine("Recording in progress...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting recording: {ex.Message}");
        }
    }

    private void StopRecording()
    {
        if (!_audioRecorder.IsRecording)
        {
            Console.WriteLine("No recording in progress.");
            return;
        }

        _audioRecorder.StopRecording();
        Console.WriteLine("Recording stopped.");
        Console.WriteLine($"File saved: {_currentRecordingPath}");
    }

    private async Task TranscribeRecordingAsync()
    {
        if (string.IsNullOrEmpty(_currentRecordingPath) || !File.Exists(_currentRecordingPath))
        {
            Console.WriteLine("No recording available. Please record audio first or select a file.");
            return;
        }

        if (_whisperTranscriber == null || !_whisperTranscriber.IsWhisperAvailable())
        {
            Console.WriteLine("Whisper is not configured. Please configure Whisper first (option 4).");
            return;
        }

        try
        {
            Console.WriteLine();
            Console.WriteLine("=== Starting Transcription ===");
            Console.WriteLine($"File: {Path.GetFileName(_currentRecordingPath)}");
            Console.WriteLine($"Language: {_transcriptionOptions.Language}");
            Console.WriteLine($"Model: {_transcriptionOptions.ModelSize}");
            Console.WriteLine();

            // Preprocess audio if needed
            var audioPath = _currentRecordingPath;
            if (!_audioPreprocessor.IsWhisperCompatible(audioPath))
            {
                Console.WriteLine("Preprocessing audio to 16kHz mono format...");
                var processedPath = Path.Combine(
                    Path.GetDirectoryName(audioPath) ?? "",
                    Path.GetFileNameWithoutExtension(audioPath) + "_processed.wav");

                if (_audioPreprocessor.ConvertToWhisperFormat(audioPath, processedPath))
                {
                    audioPath = processedPath;
                    Console.WriteLine("Audio preprocessing complete.");
                }
                else
                {
                    Console.WriteLine("Warning: Could not preprocess audio. Attempting with original file.");
                }
            }

            // Set up progress handler
            _whisperTranscriber.ProgressUpdated += (s, msg) =>
            {
                Console.WriteLine($"[Whisper] {msg}");
            };

            // Prepare transcription options
            _transcriptionOptions.AudioFilePath = audioPath;

            // Transcribe
            Console.WriteLine("Transcribing with Whisper... This may take some time.");
            var transcription = await _whisperTranscriber.TranscribeAsync(_transcriptionOptions);

            Console.WriteLine();
            Console.WriteLine("=== Transcription Complete ===");
            Console.WriteLine();
            Console.WriteLine(transcription);
            Console.WriteLine();

            // Save to file
            var transcriptPath = Path.Combine(
                Path.GetDirectoryName(_currentRecordingPath) ?? "",
                Path.GetFileNameWithoutExtension(_currentRecordingPath) + "_transcript.txt");
            
            File.WriteAllText(transcriptPath, transcription);
            Console.WriteLine($"Transcription saved to: {transcriptPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transcription failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void ConfigureWhisper()
    {
        Console.WriteLine();
        Console.WriteLine("=== Configure Whisper ===");
        Console.WriteLine();
        Console.WriteLine("To use Whisper for transcription, you need:");
        Console.WriteLine("1. Whisper.cpp executable (download from https://github.com/ggerganov/whisper.cpp)");
        Console.WriteLine("2. Whisper model files (download from https://huggingface.co/ggerganov/whisper.cpp)");
        Console.WriteLine();

        Console.Write("Enter path to Whisper executable (e.g., ./whisper.cpp/main): ");
        var whisperExe = Console.ReadLine() ?? string.Empty;

        Console.Write("Enter path to models directory: ");
        var modelsDir = Console.ReadLine() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(whisperExe) || string.IsNullOrWhiteSpace(modelsDir))
        {
            Console.WriteLine("Invalid paths provided.");
            return;
        }

        _whisperTranscriber = new WhisperTranscriber(whisperExe, modelsDir);

        if (_whisperTranscriber.IsWhisperAvailable())
        {
            Console.WriteLine("Whisper configured successfully!");
            
            var availableModels = _whisperTranscriber.GetAvailableModels();
            if (availableModels.Any())
            {
                Console.WriteLine($"Available models: {string.Join(", ", availableModels)}");
            }
            else
            {
                Console.WriteLine("Warning: No model files found in the models directory.");
            }
        }
        else
        {
            Console.WriteLine("Could not verify Whisper installation. Please check the paths.");
        }
    }

    private void SetLanguage()
    {
        Console.WriteLine();
        Console.WriteLine("=== Select Language ===");
        Console.WriteLine();
        Console.WriteLine("Common language codes:");
        Console.WriteLine("  en - English");
        Console.WriteLine("  es - Spanish");
        Console.WriteLine("  fr - French");
        Console.WriteLine("  de - German");
        Console.WriteLine("  it - Italian");
        Console.WriteLine("  pt - Portuguese");
        Console.WriteLine("  nl - Dutch");
        Console.WriteLine("  ru - Russian");
        Console.WriteLine("  zh - Chinese");
        Console.WriteLine("  ja - Japanese");
        Console.WriteLine("  ko - Korean");
        Console.WriteLine();
        Console.Write($"Enter language code (current: {_transcriptionOptions.Language}): ");
        
        var lang = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(lang))
        {
            _transcriptionOptions.Language = lang.Trim().ToLower();
            Console.WriteLine($"Language set to: {_transcriptionOptions.Language}");
        }
    }

    private void SetModelSize()
    {
        Console.WriteLine();
        Console.WriteLine("=== Select Model Size ===");
        Console.WriteLine();
        Console.WriteLine("Available models:");
        Console.WriteLine("  1. Tiny (~75 MB) - Fastest, least accurate");
        Console.WriteLine("  2. Base (~142 MB) - Good balance for CPU");
        Console.WriteLine("  3. Small (~466 MB) - Better accuracy");
        Console.WriteLine("  4. Medium (~1.5 GB) - High accuracy");
        Console.WriteLine("  5. Large (~2.9 GB) - Best accuracy, slowest");
        Console.WriteLine();
        Console.Write($"Select model (current: {_transcriptionOptions.ModelSize}): ");
        
        var choice = Console.ReadLine();
        var modelSize = choice switch
        {
            "1" => WhisperModelSize.Tiny,
            "2" => WhisperModelSize.Base,
            "3" => WhisperModelSize.Small,
            "4" => WhisperModelSize.Medium,
            "5" => WhisperModelSize.Large,
            _ => _transcriptionOptions.ModelSize
        };

        _transcriptionOptions.ModelSize = modelSize;
        Console.WriteLine($"Model size set to: {_transcriptionOptions.ModelSize}");
    }

    private void ViewRecordings()
    {
        Console.WriteLine();
        Console.WriteLine("=== Recordings ===");
        Console.WriteLine();

        var recordings = Directory.GetFiles(_recordingsDirectory, "*.wav")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToList();

        if (!recordings.Any())
        {
            Console.WriteLine("No recordings found.");
            return;
        }

        for (int i = 0; i < recordings.Count; i++)
        {
            var file = recordings[i];
            var fileInfo = new FileInfo(file);
            Console.WriteLine($"{i + 1}. {Path.GetFileName(file)} ({fileInfo.Length / 1024} KB) - {fileInfo.CreationTime}");
        }

        Console.WriteLine();
        Console.Write("Select a recording to transcribe (or press Enter to cancel): ");
        var choice = Console.ReadLine();

        if (int.TryParse(choice, out int index) && index > 0 && index <= recordings.Count)
        {
            _currentRecordingPath = recordings[index - 1];
            Console.WriteLine($"Selected: {Path.GetFileName(_currentRecordingPath)}");
        }
    }
}

