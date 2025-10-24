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
    private readonly WaveFormat _targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);
    private WasapiLoopbackCapture? _loopbackCapture;
    private WasapiCapture? _microphoneCapture;
    private BufferedWaveProvider? _loopbackBuffer;
    private BufferedWaveProvider? _microphoneBuffer;
    private WaveFileWriter? _writer;
    private CancellationTokenSource? _recordingCts;
    private Task? _pumpTask;
    private readonly object _writerLock = new();

    public event EventHandler<string>? Status;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<Exception>? RecordingFailed;

    public bool IsRecording => _recordingCts is not null;
    public string? CurrentFilePath { get; private set; }

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
            var renderDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var captureDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);

            _loopbackCapture = new WasapiLoopbackCapture(renderDevice);
            _microphoneCapture = new WasapiCapture(captureDevice);

            _loopbackBuffer = new BufferedWaveProvider(_loopbackCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };
            _microphoneBuffer = new BufferedWaveProvider(_microphoneCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            _loopbackCapture.DataAvailable += (_, e) => _loopbackBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
            _microphoneCapture.DataAvailable += (_, e) => _microphoneBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);

            _loopbackCapture.RecordingStopped += OnCaptureStopped;
            _microphoneCapture.RecordingStopped += OnCaptureStopped;

            var mixer = CreateMixer();
            var writerProvider = new SampleToWaveProvider16(mixer);
            _writer = new WaveFileWriter(outputFilePath, writerProvider.WaveFormat);
            CurrentFilePath = outputFilePath;

            _recordingCts = new CancellationTokenSource();
            _pumpTask = Task.Run(() => PumpAsync(writerProvider, _recordingCts.Token));

            _loopbackCapture.StartRecording();
            _microphoneCapture.StartRecording();

            Status?.Invoke(this, "Recording started.");
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
            return;
        }

        _recordingCts?.Cancel();

        try
        {
            _loopbackCapture?.StopRecording();
        }
        catch (Exception ex)
        {
            RecordingFailed?.Invoke(this, ex);
        }

        try
        {
            _microphoneCapture?.StopRecording();
        }
        catch (Exception ex)
        {
            RecordingFailed?.Invoke(this, ex);
        }

        if (_pumpTask is not null)
        {
            await _pumpTask.ConfigureAwait(false);
        }

        lock (_writerLock)
        {
            _writer?.Dispose();
            _writer = null;
        }

        Cleanup();

        if (CurrentFilePath is not null)
        {
            RecordingStopped?.Invoke(this, CurrentFilePath);
        }

        Status?.Invoke(this, "Recording stopped.");
    }

    private MixingSampleProvider CreateMixer()
    {
        if (_loopbackBuffer is null || _microphoneBuffer is null || _loopbackCapture is null || _microphoneCapture is null)
        {
            throw new InvalidOperationException("Recorder has not been initialised correctly.");
        }

        ISampleProvider loopbackProvider = BuildInputSampleProvider(_loopbackBuffer);
        ISampleProvider microphoneProvider = BuildInputSampleProvider(_microphoneBuffer);

        // reduce gain slightly to avoid clipping when summing
        loopbackProvider = new VolumeSampleProvider(loopbackProvider) { Volume = 0.7f };
        microphoneProvider = new VolumeSampleProvider(microphoneProvider) { Volume = 0.7f };

        var mixer = new MixingSampleProvider(new[] { loopbackProvider, microphoneProvider })
        {
            ReadFully = false
        };

        return mixer;
    }

    private ISampleProvider BuildInputSampleProvider(BufferedWaveProvider buffer)
    {
        ISampleProvider provider = buffer.ToSampleProvider();

        if (provider.WaveFormat.SampleRate != _targetFormat.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, _targetFormat.SampleRate);
        }

        if (provider.WaveFormat.Channels > _targetFormat.Channels)
        {
            provider = new StereoToMonoSampleProvider(provider);
        }
        else if (provider.WaveFormat.Channels < _targetFormat.Channels)
        {
            provider = new MonoToStereoSampleProvider(provider);
        }

        return provider;
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            RecordingFailed?.Invoke(this, e.Exception);
        }
    }

    private void PumpAsync(IWaveProvider provider, CancellationToken cancellationToken)
    {
        var buffer = new byte[provider.WaveFormat.AverageBytesPerSecond / 4];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = provider.Read(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    lock (_writerLock)
                    {
                        _writer?.Write(buffer, 0, read);
                        _writer?.Flush();
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }

            // flush any remaining data
            int remaining;
            while ((remaining = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                lock (_writerLock)
                {
                    _writer?.Write(buffer, 0, remaining);
                }
            }
        }
        catch (Exception ex)
        {
            RecordingFailed?.Invoke(this, ex);
        }
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
        _loopbackBuffer = null;
        _microphoneBuffer = null;

        _recordingCts?.Dispose();
        _recordingCts = null;

        lock (_writerLock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
