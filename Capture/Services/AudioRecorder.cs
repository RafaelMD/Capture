using NAudio.Wave;

namespace Capture.Services;

/// <summary>
/// Handles audio recording using NAudio
/// </summary>
public class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _outputFilePath;
    private bool _isRecording;

    /// <summary>
    /// Event raised when recording starts
    /// </summary>
    public event EventHandler? RecordingStarted;

    /// <summary>
    /// Event raised when recording stops
    /// </summary>
    public event EventHandler? RecordingStopped;

    /// <summary>
    /// Gets whether recording is currently active
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Gets the path to the current recording file
    /// </summary>
    public string? OutputFilePath => _outputFilePath;

    /// <summary>
    /// Start recording audio to a file
    /// </summary>
    /// <param name="outputPath">Path where the recording will be saved</param>
    /// <param name="sampleRate">Sample rate (default: 16000 Hz for Whisper compatibility)</param>
    /// <param name="channels">Number of channels (default: 1 for mono)</param>
    public void StartRecording(string outputPath, int sampleRate = 16000, int channels = 1)
    {
        if (_isRecording)
        {
            throw new InvalidOperationException("Recording is already in progress");
        }

        _outputFilePath = outputPath;

        // Configure audio capture
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(sampleRate, 16, channels),
            BufferMilliseconds = 50
        };

        // Create wave file writer
        _writer = new WaveFileWriter(outputPath, _waveIn.WaveFormat);

        // Handle data available event
        _waveIn.DataAvailable += (sender, e) =>
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        };

        // Start recording
        _waveIn.StartRecording();
        _isRecording = true;
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stop the current recording
    /// </summary>
    public void StopRecording()
    {
        if (!_isRecording)
        {
            return;
        }

        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _writer?.Dispose();

        _isRecording = false;
        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        StopRecording();
        GC.SuppressFinalize(this);
    }
}
