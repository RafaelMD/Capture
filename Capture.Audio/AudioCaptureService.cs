using NAudio.Wave;
using System;
using System.IO;
using System.Threading;

namespace Capture.Audio
{
    /// <summary>
    /// Service for capturing and recording both system audio (loopback) and microphone input
    /// using WASAPI in shared mode. Mixes both streams and saves to a WAV file.
    /// </summary>
    public class AudioCaptureService : IAudioCaptureService
    {
        private WasapiLoopbackCapture? _loopbackCapture;
        private WaveInEvent? _microphoneCapture;
        private WaveFileWriter? _waveWriter;
        private AudioMixer? _mixer;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private bool _isRecording;

        // Buffers for mixing audio
        private byte[] _loopbackBuffer = Array.Empty<byte>();
        private byte[] _microphoneBuffer = Array.Empty<byte>();
        private int _loopbackBytesRecorded;
        private int _microphoneBytesRecorded;

        // Target format: 16-bit PCM, 44100 Hz, Stereo (standard CD quality)
        private readonly WaveFormat _targetFormat = new WaveFormat(44100, 16, 2);

        /// <summary>
        /// Gets a value indicating whether recording is currently active.
        /// </summary>
        public bool IsRecording
        {
            get
            {
                lock (_lockObject)
                {
                    return _isRecording;
                }
            }
        }

        /// <summary>
        /// Event raised when an error occurs during recording.
        /// </summary>
        public event EventHandler<string>? RecordingError;

        /// <summary>
        /// Starts recording audio from both system output (loopback) and microphone input.
        /// </summary>
        /// <param name="outputFilePath">Path to the output WAV file.</param>
        public void StartRecording(string outputFilePath)
        {
            if (string.IsNullOrWhiteSpace(outputFilePath))
                throw new ArgumentException("Output file path cannot be null or empty.", nameof(outputFilePath));

            lock (_lockObject)
            {
                if (_isRecording)
                    throw new InvalidOperationException("Recording is already in progress.");

                try
                {
                    // Initialize the wave file writer
                    _waveWriter = new WaveFileWriter(outputFilePath, _targetFormat);

                    // Initialize the mixer
                    _mixer = new AudioMixer(_targetFormat);

                    // Setup loopback capture (system audio)
                    _loopbackCapture = new WasapiLoopbackCapture();
                    _loopbackCapture.DataAvailable += OnLoopbackDataAvailable;
                    _loopbackCapture.RecordingStopped += OnRecordingStopped;

                    // Setup microphone capture
                    // Note: Using default input device with WaveInEvent
                    _microphoneCapture = new WaveInEvent();
                    _microphoneCapture.WaveFormat = _targetFormat;
                    _microphoneCapture.DataAvailable += OnMicrophoneDataAvailable;
                    _microphoneCapture.RecordingStopped += OnRecordingStopped;

                    // Initialize buffers
                    int bufferSize = _targetFormat.AverageBytesPerSecond; // 1 second buffer
                    _loopbackBuffer = new byte[bufferSize];
                    _microphoneBuffer = new byte[bufferSize];
                    _loopbackBytesRecorded = 0;
                    _microphoneBytesRecorded = 0;

                    // Start both captures
                    _loopbackCapture.StartRecording();
                    _microphoneCapture.StartRecording();

                    _isRecording = true;
                }
                catch (Exception ex)
                {
                    CleanupResources();
                    RecordingError?.Invoke(this, $"Failed to start recording: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the current recording session.
        /// </summary>
        public void StopRecording()
        {
            lock (_lockObject)
            {
                if (!_isRecording)
                    return;

                try
                {
                    // Stop both captures
                    _loopbackCapture?.StopRecording();
                    _microphoneCapture?.StopRecording();

                    // Give some time for the stopped events to fire
                    Thread.Sleep(100);

                    CleanupResources();
                    _isRecording = false;
                }
                catch (Exception ex)
                {
                    RecordingError?.Invoke(this, $"Error stopping recording: {ex.Message}");
                }
            }
        }

        private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (e.BytesRecorded > 0)
                {
                    // Convert loopback audio to target format if necessary
                    byte[] convertedData = ConvertAudioFormat(e.Buffer, e.BytesRecorded, 
                        _loopbackCapture!.WaveFormat, _targetFormat);

                    lock (_lockObject)
                    {
                        // Store in loopback buffer
                        int bytesToCopy = Math.Min(convertedData.Length, _loopbackBuffer.Length - _loopbackBytesRecorded);
                        Array.Copy(convertedData, 0, _loopbackBuffer, _loopbackBytesRecorded, bytesToCopy);
                        _loopbackBytesRecorded += bytesToCopy;

                        // Try to mix and write if we have data from both sources
                        TryMixAndWrite();
                    }
                }
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, $"Error processing loopback audio: {ex.Message}");
            }
        }

        private void OnMicrophoneDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (e.BytesRecorded > 0)
                {
                    // Convert microphone audio to target format if necessary
                    byte[] convertedData = ConvertAudioFormat(e.Buffer, e.BytesRecorded,
                        _microphoneCapture!.WaveFormat, _targetFormat);

                    lock (_lockObject)
                    {
                        // Store in microphone buffer
                        int bytesToCopy = Math.Min(convertedData.Length, _microphoneBuffer.Length - _microphoneBytesRecorded);
                        Array.Copy(convertedData, 0, _microphoneBuffer, _microphoneBytesRecorded, bytesToCopy);
                        _microphoneBytesRecorded += bytesToCopy;

                        // Try to mix and write if we have data from both sources
                        TryMixAndWrite();
                    }
                }
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, $"Error processing microphone audio: {ex.Message}");
            }
        }

        private void TryMixAndWrite()
        {
            // Only mix and write if we have data from both sources
            if (_loopbackBytesRecorded > 0 && _microphoneBytesRecorded > 0 && _waveWriter != null && _mixer != null)
            {
                // Determine how much data we can mix (minimum of both buffers)
                int bytesToMix = Math.Min(_loopbackBytesRecorded, _microphoneBytesRecorded);

                // Ensure we mix complete samples (multiple of bytes per sample)
                int bytesPerSample = _targetFormat.BitsPerSample / 8 * _targetFormat.Channels;
                bytesToMix = (bytesToMix / bytesPerSample) * bytesPerSample;

                if (bytesToMix > 0)
                {
                    // Create output buffer
                    byte[] mixedBuffer = new byte[bytesToMix];

                    // Mix the audio
                    _mixer.MixBuffers(_loopbackBuffer, _microphoneBuffer, mixedBuffer, bytesToMix);

                    // Write to file
                    _waveWriter.Write(mixedBuffer, 0, bytesToMix);

                    // Shift remaining data in buffers
                    ShiftBuffer(_loopbackBuffer, ref _loopbackBytesRecorded, bytesToMix);
                    ShiftBuffer(_microphoneBuffer, ref _microphoneBytesRecorded, bytesToMix);
                }
            }
        }

        private void ShiftBuffer(byte[] buffer, ref int bytesRecorded, int bytesToRemove)
        {
            int remainingBytes = bytesRecorded - bytesToRemove;
            if (remainingBytes > 0)
            {
                Array.Copy(buffer, bytesToRemove, buffer, 0, remainingBytes);
            }
            bytesRecorded = remainingBytes;
        }

        private byte[] ConvertAudioFormat(byte[] inputBuffer, int bytesRecorded, WaveFormat sourceFormat, WaveFormat targetFormat)
        {
            // If formats match, no conversion needed
            if (sourceFormat.Equals(targetFormat))
            {
                byte[] result = new byte[bytesRecorded];
                Array.Copy(inputBuffer, result, bytesRecorded);
                return result;
            }

            // Use NAudio's resampler for format conversion
            using (var memoryStream = new MemoryStream(inputBuffer, 0, bytesRecorded))
            using (var rawSourceStream = new RawSourceWaveStream(memoryStream, sourceFormat))
            {
                // Convert to target format
                using (var resampler = new MediaFoundationResampler(rawSourceStream, targetFormat))
                {
                    byte[] outputBuffer = new byte[bytesRecorded * targetFormat.AverageBytesPerSecond / sourceFormat.AverageBytesPerSecond + 1024];
                    int bytesRead = resampler.Read(outputBuffer, 0, outputBuffer.Length);
                    
                    byte[] result = new byte[bytesRead];
                    Array.Copy(outputBuffer, result, bytesRead);
                    return result;
                }
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                RecordingError?.Invoke(this, $"Recording stopped with error: {e.Exception.Message}");
            }
        }

        private void CleanupResources()
        {
            if (_loopbackCapture != null)
            {
                _loopbackCapture.DataAvailable -= OnLoopbackDataAvailable;
                _loopbackCapture.RecordingStopped -= OnRecordingStopped;
                _loopbackCapture.Dispose();
                _loopbackCapture = null;
            }

            if (_microphoneCapture != null)
            {
                _microphoneCapture.DataAvailable -= OnMicrophoneDataAvailable;
                _microphoneCapture.RecordingStopped -= OnRecordingStopped;
                _microphoneCapture.Dispose();
                _microphoneCapture = null;
            }

            if (_waveWriter != null)
            {
                _waveWriter.Dispose();
                _waveWriter = null;
            }

            if (_mixer != null)
            {
                _mixer.Dispose();
                _mixer = null;
            }
        }

        /// <summary>
        /// Disposes the AudioCaptureService and releases all resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by the AudioCaptureService.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopRecording();
                    CleanupResources();
                }
                _disposed = true;
            }
        }
    }
}
