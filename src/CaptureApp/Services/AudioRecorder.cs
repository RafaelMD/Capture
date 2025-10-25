using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CaptureApp.Services;

public sealed class AudioRecorder : IDisposable
{
    // Use standard 16-bit PCM format for better compatibility
    private readonly WaveFormat _targetFormat = new WaveFormat(16000, 16, 1);
    private WasapiLoopbackCapture? _loopbackCapture;
    private WasapiCapture? _microphoneCapture;
    private WaveFileWriter? _loopbackWriter;
    private WaveFileWriter? _microphoneWriter;
    private CancellationTokenSource? _recordingCts;
    private readonly object _loopbackLock = new();
    private readonly object _microphoneLock = new();
    private string? _loopbackFilePath;
    private string? _microphoneFilePath;

    public event EventHandler<string>? Status;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<Exception>? RecordingFailed;

    public bool IsRecording => _recordingCts is not null;
    public string? CurrentFilePath { get; private set; }

    public static string GetAvailableDevicesInfo()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var info = new System.Text.StringBuilder();
            
            info.AppendLine("=== Playback Devices (Render) ===");
            var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            if (renderDevices.Count == 0)
            {
                info.AppendLine("  No active playback devices found!");
            }
            else
            {
                foreach (var device in renderDevices)
                {
                    info.AppendLine($"  • {device.FriendlyName} {(device.State == DeviceState.Active ? "[Active]" : "")}");
                }
            }
            
            info.AppendLine("\n=== Recording Devices (Capture) ===");
            var captureDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            if (captureDevices.Count == 0)
            {
                info.AppendLine("  No active recording devices found!");
            }
            else
            {
                foreach (var device in captureDevices)
                {
                    info.AppendLine($"  • {device.FriendlyName} {(device.State == DeviceState.Active ? "[Active]" : "")}");
                }
            }
            
            return info.ToString();
        }
        catch (Exception ex)
        {
            return $"Error enumerating devices: {ex.Message}";
        }
    }

    public void StartRecording(string outputFilePath)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? Environment.CurrentDirectory);

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            
            // Check if devices exist before attempting to use them
            MMDevice? renderDevice = null;
            MMDevice? captureDevice = null;
            
            try
            {
                renderDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("No default playback device found. Please ensure your speakers/headphones are properly connected and enabled.", ex);
            }
            
            try
            {
                captureDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("No default recording device (microphone) found. Please ensure your microphone is properly connected and enabled in Windows Sound settings.", ex);
            }

            // Create separate file paths
            var directory = Path.GetDirectoryName(outputFilePath) ?? Environment.CurrentDirectory;
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputFilePath);
            _loopbackFilePath = Path.Combine(directory, $"{fileNameWithoutExt}_loopback.wav");
            _microphoneFilePath = Path.Combine(directory, $"{fileNameWithoutExt}_microphone.wav");
            CurrentFilePath = outputFilePath;

            _loopbackCapture = new WasapiLoopbackCapture(renderDevice);
            _microphoneCapture = new WasapiCapture(captureDevice);

            // Create WAV writers with proper format
            var loopbackFormat = new WaveFormat(16000, 16, _loopbackCapture.WaveFormat.Channels);
            var microphoneFormat = new WaveFormat(16000, 16, _microphoneCapture.WaveFormat.Channels);
            
            _loopbackWriter = new WaveFileWriter(_loopbackFilePath, loopbackFormat);
            _microphoneWriter = new WaveFileWriter(_microphoneFilePath, microphoneFormat);

            int loopbackSamples = 0;
            int microphoneSamples = 0;

            // Loopback capture handler - records system audio
            _loopbackCapture.DataAvailable += (_, e) =>
            {
                if (e.BytesRecorded > 0)
                {
                    loopbackSamples += e.BytesRecorded;
                    if (loopbackSamples == e.BytesRecorded || loopbackSamples % 96000 < e.BytesRecorded)
                    {
                        Status?.Invoke(this, $"Loopback: {loopbackSamples:N0} bytes captured");
                    }
                    
                    // Convert and write directly
                    lock (_loopbackLock)
                    {
                        try
                        {
                            var samples = ConvertBytesToSamples(e.Buffer, e.BytesRecorded, _loopbackCapture.WaveFormat);
                            var resampled = ResampleIfNeeded(samples, _loopbackCapture.WaveFormat.SampleRate, 16000);
                            var bytes = ConvertSamplesToBytes(resampled);
                            _loopbackWriter?.Write(bytes, 0, bytes.Length);
                        }
                        catch (Exception ex)
                        {
                            Status?.Invoke(this, $"Loopback write error: {ex.Message}");
                        }
                    }
                }
            };
            
            // Microphone capture handler - records microphone audio
            _microphoneCapture.DataAvailable += (_, e) =>
            {
                if (e.BytesRecorded > 0)
                {
                    microphoneSamples += e.BytesRecorded;
                    if (microphoneSamples == e.BytesRecorded || microphoneSamples % 96000 < e.BytesRecorded)
                    {
                        Status?.Invoke(this, $"Microphone: {microphoneSamples:N0} bytes captured");
                    }
                    
                    // Convert and write directly
                    lock (_microphoneLock)
                    {
                        try
                        {
                            var samples = ConvertBytesToSamples(e.Buffer, e.BytesRecorded, _microphoneCapture.WaveFormat);
                            var resampled = ResampleIfNeeded(samples, _microphoneCapture.WaveFormat.SampleRate, 16000);
                            var bytes = ConvertSamplesToBytes(resampled);
                            _microphoneWriter?.Write(bytes, 0, bytes.Length);
                        }
                        catch (Exception ex)
                        {
                            Status?.Invoke(this, $"Microphone write error: {ex.Message}");
                        }
                    }
                }
            };

            _loopbackCapture.RecordingStopped += OnCaptureStopped;
            _microphoneCapture.RecordingStopped += OnCaptureStopped;

            _recordingCts = new CancellationTokenSource();

            _loopbackCapture.StartRecording();
            _microphoneCapture.StartRecording();

            Status?.Invoke(this, $"Recording started. Loopback: {_loopbackFilePath}, Microphone: {_microphoneFilePath}");
        }
        catch
        {
            Cleanup();
            throw;
        }
    }

    public async Task StopRecordingAsync()
    {
        if (!IsRecording)
        {
            Status?.Invoke(this, "No recording in progress.");
            return;
        }

        Status?.Invoke(this, "Cancelling recording task...");
        _recordingCts?.Cancel();

        Status?.Invoke(this, "Stopping loopback capture...");
        try
        {
            _loopbackCapture?.StopRecording();
        }
        catch (Exception ex)
        {
            RecordingFailed?.Invoke(this, ex);
        }

        Status?.Invoke(this, "Stopping microphone capture...");
        try
        {
            _microphoneCapture?.StopRecording();
        }
        catch (Exception ex)
        {
            RecordingFailed?.Invoke(this, ex);
        }

        await Task.Delay(200).ConfigureAwait(false); // Give time for last writes

        Status?.Invoke(this, "Closing audio files...");
        
        lock (_loopbackLock)
        {
            _loopbackWriter?.Dispose();
            _loopbackWriter = null;
        }
        
        lock (_microphoneLock)
        {
            _microphoneWriter?.Dispose();
            _microphoneWriter = null;
        }

        Status?.Invoke(this, "Merging audio files...");
        
        // Merge the two files
        try
        {
            await MergeAudioFilesAsync(_loopbackFilePath!, _microphoneFilePath!, CurrentFilePath!);
            Status?.Invoke(this, $"Merged audio saved to: {CurrentFilePath}");
        }
        catch (Exception ex)
        {
            Status?.Invoke(this, $"Error merging audio: {ex.Message}");
        }

        Cleanup();

        if (CurrentFilePath is not null)
        {
            RecordingStopped?.Invoke(this, CurrentFilePath);
        }

        Status?.Invoke(this, "Recording stopped.");
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            RecordingFailed?.Invoke(this, e.Exception);
        }
    }

    private float[] ConvertBytesToSamples(byte[] buffer, int length, WaveFormat format)
    {
        var sampleCount = length / (format.BitsPerSample / 8);
        var samples = new float[sampleCount];
        
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            Buffer.BlockCopy(buffer, 0, samples, 0, length);
        }
        else if (format.BitsPerSample == 16)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = BitConverter.ToInt16(buffer, i * 2) / 32768f;
            }
        }
        
        return samples;
    }

    private float[] ResampleIfNeeded(float[] samples, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate)
            return samples;
            
        var ratio = (double)targetRate / sourceRate;
        var newLength = (int)(samples.Length * ratio);
        var resampled = new float[newLength];
        
        for (int i = 0; i < newLength; i++)
        {
            var sourceIndex = i / ratio;
            var index = (int)sourceIndex;
            if (index < samples.Length)
            {
                resampled[i] = samples[index];
            }
        }
        
        return resampled;
    }

    private byte[] ConvertSamplesToBytes(float[] samples)
    {
        var bytes = new byte[samples.Length * 2]; // 16-bit = 2 bytes per sample
        
        for (int i = 0; i < samples.Length; i++)
        {
            var sample = Math.Max(-1f, Math.Min(1f, samples[i])); // Clamp
            var int16Value = (short)(sample * 32767f);
            BitConverter.GetBytes(int16Value).CopyTo(bytes, i * 2);
        }
        
        return bytes;
    }

    private async Task MergeAudioFilesAsync(string loopbackPath, string microphonePath, string outputPath)
    {
        await Task.Run(() =>
        {
            using var loopbackReader = new AudioFileReader(loopbackPath);
            using var microphoneReader = new AudioFileReader(microphonePath);
            
            // Mix both sources
            var mixer = new MixingSampleProvider(new[] { loopbackReader, microphoneReader })
            {
                ReadFully = true
            };
            
            // Convert to mono 16kHz 16-bit
            var resampled = new WdlResamplingSampleProvider(mixer, 16000);
            var mono = resampled.WaveFormat.Channels > 1 
                ? new StereoToMonoSampleProvider(resampled) 
                : (ISampleProvider)resampled;
            var wave16 = new SampleToWaveProvider16(mono);
            
            // Write to output file
            WaveFileWriter.CreateWaveFile(outputPath, wave16);
            
            Status?.Invoke(this, $"Merged {new FileInfo(outputPath).Length:N0} bytes");
        });
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            StopRecordingAsync().GetAwaiter().GetResult();
        }

        Cleanup();
    }

    private void Cleanup()
    {
        _loopbackCapture?.Dispose();
        _loopbackCapture = null;
        _microphoneCapture?.Dispose();
        _microphoneCapture = null;

        _recordingCts?.Dispose();
        _recordingCts = null;

        lock (_loopbackLock)
        {
            _loopbackWriter?.Dispose();
            _loopbackWriter = null;
        }
        
        lock (_microphoneLock)
        {
            _microphoneWriter?.Dispose();
            _microphoneWriter = null;
        }
    }
}
