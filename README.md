# Capture

A Windows desktop application for recording audio from both microphone and speakers (system audio) with background operation support.

## Features

### User Interface
- Simple, intuitive UI with a Record/Stop button
- Language selection dropdown for future transcription support
- Status indicator showing current recording state
- System tray icon for minimized background recording
- Recording indicator with balloon notifications

### Background Recording
- Audio capture runs on background threads without freezing the UI
- Continue recording even when the application window is minimized
- System tray icon provides visual feedback when recording is active
- Non-blocking operation allows other applications to use audio devices

### Audio Capture
- Records from both microphone and speakers simultaneously
- Uses NAudio library with WASAPI in shared mode
- Non-exclusive access means other apps can use audio devices concurrently
- Output format: Uncompressed WAV files (optimal for transcription)
- Proper WAV file header flushing prevents corruption

### Safety Features
- Warning dialog when attempting to close while recording
- Automatic cleanup and disposal of audio resources
- Thread-safe operations with proper locking
- Error handling with user-friendly notifications

## Technical Details

### Audio Capture Implementation
- **NAudio Library**: Handles audio capture via WASAPI
- **Shared Mode**: Allows concurrent access to audio devices
- **Background Threads**: Audio capture and file writing run asynchronously
- **Thread Safety**: Events are marshaled to UI thread properly

### File Output
- Recordings are saved to: `Documents/CaptureRecordings/`
- Filename format: `Recording_YYYYMMDD_HHmmss.wav`
- WAV format is used for best transcription compatibility (e.g., with Whisper)

## System Requirements

- Windows 10 or later
- .NET 9.0 or later
- Microphone and/or speakers for audio capture

## Building the Application

```bash
# Clone the repository
git clone https://github.com/RafaelMD/Capture.git
cd Capture

# Build the solution
dotnet build

# Run the application
dotnet run --project CaptureApp
```

## Usage

1. **Select Language**: Choose the language for future transcription from the dropdown
2. **Start Recording**: Click "Start Recording" to begin capturing audio
3. **Background Operation**: Minimize the window if needed - recording continues
4. **Stop Recording**: Click "Stop Recording" when done
5. **Access Files**: Recordings are saved to your Documents folder

## Architecture

### AudioRecorder Class
- Manages WasapiCapture for microphone input
- Manages WasapiLoopbackCapture for speaker output
- Handles background thread events safely
- Implements IDisposable for proper resource cleanup

### MainForm Class
- Windows Forms UI with Record/Stop toggle
- Language selection for transcription preparation
- System tray integration for minimized operation
- Thread-safe event handling from background audio threads

## Performance Considerations

- Audio capture is not CPU intensive
- Mixing two audio streams doubles data throughput
- WAV files can grow large for long recordings (~10 MB per minute)
- Ensure adequate disk space is available

## Future Enhancements

- Transcription integration (e.g., Whisper API)
- Optional compressed format output (MP3/WMA)
- Audio level visualization
- Configurable output directory
- Recording pause/resume functionality

## License

This project is open source and available under standard license terms.