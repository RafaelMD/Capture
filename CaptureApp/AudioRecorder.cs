using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.IO;

namespace CaptureApp;

/// <summary>
/// Handles background audio recording from microphone and speakers (loopback) using NAudio.
/// Uses shared mode WASAPI to avoid locking devices.
/// </summary>
public class AudioRecorder : IDisposable
{
    private WasapiCapture? _microphoneCapture;
    private WasapiLoopbackCapture? _speakerCapture;
    private WaveFileWriter? _waveWriter;
    private readonly object _lockObject = new();
    private bool _isRecording;
    private string? _outputFilePath;
    private WaveFormat? _recordingFormat;

    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? RecordingStarted;
    public event EventHandler? RecordingStopped;

    public bool IsRecording
    {
        get { lock (_lockObject) { return _isRecording; } }
        private set { lock (_lockObject) { _isRecording = value; } }
    }

    /// <summary>
    /// Starts recording audio from both microphone and speakers to a WAV file.
    /// </summary>
    /// <param name="outputPath">Path where the WAV file will be saved</param>
    public void StartRecording(string outputPath)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        try
        {
            _outputFilePath = outputPath;

            // Use default microphone device
            var microphoneDevice = GetDefaultCaptureDevice();
            if (microphoneDevice == null)
            {
                throw new InvalidOperationException("No microphone device found.");
            }

            // Initialize microphone capture in shared mode
            _microphoneCapture = new WasapiCapture(microphoneDevice, true, 100);
            _recordingFormat = _microphoneCapture.WaveFormat;

            // Initialize wave file writer
            _waveWriter = new WaveFileWriter(outputPath, _recordingFormat);

            // Wire up event handlers for microphone
            _microphoneCapture.DataAvailable += OnMicrophoneDataAvailable;
            _microphoneCapture.RecordingStopped += OnRecordingStopped;

            // Initialize speaker loopback capture (this captures what's playing on speakers)
            _speakerCapture = new WasapiLoopbackCapture();
            _speakerCapture.DataAvailable += OnSpeakerDataAvailable;
            _speakerCapture.RecordingStopped += OnRecordingStopped;

            // Start both captures
            _microphoneCapture.StartRecording();
            _speakerCapture.StartRecording();

            IsRecording = true;
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CleanupResources();
            ErrorOccurred?.Invoke(this, $"Failed to start recording: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Stops the recording and properly flushes the WAV file.
    /// </summary>
    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        try
        {
            // Stop captures
            _microphoneCapture?.StopRecording();
            _speakerCapture?.StopRecording();

            IsRecording = false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error stopping recording: {ex.Message}");
        }
    }

    private void OnMicrophoneDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_waveWriter != null && e.BytesRecorded > 0)
        {
            lock (_lockObject)
            {
                try
                {
                    // Write microphone data to file
                    _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, $"Error writing microphone data: {ex.Message}");
                }
            }
        }
    }

    private void OnSpeakerDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_waveWriter != null && e.BytesRecorded > 0)
        {
            lock (_lockObject)
            {
                try
                {
                    // Write speaker data to file
                    // Note: In a real implementation, you might want to mix these streams
                    // or write them to separate files
                    _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, $"Error writing speaker data: {ex.Message}");
                }
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            ErrorOccurred?.Invoke(this, $"Recording error: {e.Exception.Message}");
        }

        CleanupResources();
        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    private MMDevice? GetDefaultCaptureDevice()
    {
        var enumerator = new MMDeviceEnumerator();
        try
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }
        catch
        {
            return null;
        }
    }

    private void CleanupResources()
    {
        lock (_lockObject)
        {
            if (_microphoneCapture != null)
            {
                _microphoneCapture.DataAvailable -= OnMicrophoneDataAvailable;
                _microphoneCapture.RecordingStopped -= OnRecordingStopped;
                _microphoneCapture.Dispose();
                _microphoneCapture = null;
            }

            if (_speakerCapture != null)
            {
                _speakerCapture.DataAvailable -= OnSpeakerDataAvailable;
                _speakerCapture.RecordingStopped -= OnRecordingStopped;
                _speakerCapture.Dispose();
                _speakerCapture = null;
            }

            if (_waveWriter != null)
            {
                // Flush ensures WAV headers are written properly
                _waveWriter.Flush();
                _waveWriter.Dispose();
                _waveWriter = null;
            }
        }
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            StopRecording();
        }
        CleanupResources();
        GC.SuppressFinalize(this);
    }
}
