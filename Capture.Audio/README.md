# Capture.Audio

A .NET library for capturing and recording both system audio (loopback) and microphone input simultaneously using WASAPI in shared mode.

## Features

- **Dual Audio Capture**: Simultaneously captures system audio (what-you-hear) and microphone input
- **WASAPI Shared Mode**: Uses Windows Core Audio APIs in shared mode, allowing multiple applications to access audio devices
- **Real-time Audio Mixing**: Mixes both audio streams in real-time into a single output
- **WAV File Recording**: Saves the mixed audio to a standard WAV file for easy processing
- **Thread-safe**: Handles concurrent audio streams with proper synchronization

## Architecture

### Components

1. **IAudioCaptureService**: Main interface for audio capture operations
2. **AudioCaptureService**: Implementation that handles WASAPI loopback and microphone capture
3. **AudioMixer**: Utility class for mixing two audio streams into one

### Audio Pipeline

```
┌─────────────────────┐
│  System Audio       │
│  (Loopback Capture) │
│  WASAPI             │
└──────────┬──────────┘
           │
           │  PCM 16-bit, 44100 Hz, Stereo
           │
           ▼
       ┌───────────┐       ┌─────────────┐
       │  Audio    │──────▶│ WAV File    │
       │  Mixer    │       │ Writer      │
       └───────────┘       └─────────────┘
           ▲
           │
           │  PCM 16-bit, 44100 Hz, Stereo
           │
┌──────────┴──────────┐
│  Microphone Input   │
│  WaveInEvent        │
└─────────────────────┘
```

## Technical Details

### Audio Format

- **Sample Rate**: 44100 Hz (CD quality)
- **Bit Depth**: 16-bit PCM
- **Channels**: 2 (Stereo)

### WASAPI Shared Mode

The library uses WASAPI in shared mode for both captures:
- **System Audio**: `WasapiLoopbackCapture` captures all audio being played by the system
- **Microphone**: `WaveInEvent` captures input from the default microphone device

Shared mode ensures that the application doesn't take exclusive control of audio devices, allowing other applications to use them simultaneously.

### Audio Mixing

The `AudioMixer` class performs real-time mixing by:
1. Reading 16-bit PCM samples from both streams
2. Adding the samples together
3. Clipping the result to prevent overflow
4. Writing the mixed output to the WAV file

### Synchronization

Both audio streams are captured in parallel and buffered separately. The service mixes and writes audio only when data is available from both sources, ensuring temporal alignment of the streams.

## Usage Example

```csharp
using Capture.Audio;

// Create the audio capture service
using (var audioService = new AudioCaptureService())
{
    // Start recording to a WAV file
    audioService.StartRecording("output.wav");
    
    // Record for some time...
    await Task.Delay(TimeSpan.FromSeconds(30));
    
    // Stop recording
    audioService.StopRecording();
}
```

## Error Handling

The service provides error notifications through the `RecordingError` event:

```csharp
audioService.RecordingError += (sender, error) =>
{
    Console.WriteLine($"Recording error: {error}");
};
```

## Requirements

- .NET 9.0 or later
- NAudio 2.2.1 or later
- Windows operating system (WASAPI is Windows-specific)

## Dependencies

- NAudio
- NAudio.Wasapi

## Notes

- The library performs automatic format conversion if the capture device formats don't match the target format
- Audio buffers are managed to prevent memory leaks and ensure smooth recording
- All resources are properly disposed when recording stops or the service is disposed
