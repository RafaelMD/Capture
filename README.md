# Capture - Audio Recording & Transcription with Whisper

A .NET console application for audio recording and offline speech-to-text transcription using OpenAI's Whisper model.

## Features

- **Audio Recording**: Record audio in Whisper-compatible format (16kHz mono PCM WAV)
- **Offline Transcription**: Transcribe audio files using Whisper.cpp (no internet required)
- **Multi-language Support**: Transcribe in 50+ languages including English, Spanish, French, German, Italian, Portuguese, Dutch, Russian, Chinese, Japanese, and Korean
- **Multiple Model Sizes**: Choose between Tiny, Base, Small, Medium, and Large models for different accuracy/speed trade-offs
- **Audio Preprocessing**: Automatic conversion to Whisper-compatible format if needed
- **Recording Management**: View and select from previously recorded audio files

## Requirements

### Software Dependencies

1. **.NET 9.0 SDK** or later
2. **Whisper.cpp** - C++ port of OpenAI Whisper for efficient CPU inference
   - Download from: https://github.com/ggerganov/whisper.cpp
   - Build instructions available in their repository
3. **Whisper Model Files** - Pre-trained models
   - Download from: https://huggingface.co/ggerganov/whisper.cpp

### Audio Hardware

- Microphone or audio input device
- Sufficient disk space for recordings and models

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/RafaelMD/Capture.git
cd Capture/Capture
```

### 2. Build the Application

```bash
dotnet restore
dotnet build
```

### 3. Install Whisper.cpp

#### On Linux/macOS:

```bash
# Clone and build whisper.cpp
git clone https://github.com/ggerganov/whisper.cpp
cd whisper.cpp
make
```

#### On Windows:

- Download pre-built binaries from the releases page, or
- Build from source using CMake and Visual Studio

### 4. Download Whisper Models

Download at least one model file from https://huggingface.co/ggerganov/whisper.cpp

Available models:
- **ggml-tiny.bin** (~75 MB) - Fastest, basic accuracy
- **ggml-base.bin** (~142 MB) - Good balance for CPU (recommended for starting)
- **ggml-small.bin** (~466 MB) - Better accuracy
- **ggml-medium.bin** (~1.5 GB) - High accuracy
- **ggml-large-v3.bin** (~2.9 GB) - Best accuracy, slowest

Place the downloaded model files in a dedicated directory (e.g., `~/whisper-models/`).

## Usage

### Running the Application

```bash
dotnet run --project Capture
```

Or run the built executable:

```bash
./Capture/bin/Debug/net9.0/Capture
```

### First-Time Setup

1. When you first run the application, select option **4** to configure Whisper
2. Enter the path to the Whisper.cpp executable (e.g., `./whisper.cpp/main` or `C:\whisper.cpp\main.exe`)
3. Enter the path to your models directory (e.g., `~/whisper-models/`)

Alternatively, set environment variables:
```bash
export WHISPER_EXECUTABLE="/path/to/whisper.cpp/main"
export WHISPER_MODELS_DIR="/path/to/models"
```

### Recording Audio

1. Select **option 1** to start recording
2. Speak into your microphone
3. Select **option 2** to stop recording when finished
4. The recording is saved in `~/Capture/Recordings/` with a timestamp

### Transcribing Audio

1. After recording, select **option 3** to transcribe
2. Wait for the transcription to complete (progress will be displayed)
3. The transcribed text will be displayed and saved as a `.txt` file next to the recording

### Setting Language

1. Select **option 5** to set the language
2. Enter the language code (e.g., `en` for English, `es` for Spanish)
3. The language will be used for subsequent transcriptions

### Changing Model Size

1. Select **option 6** to choose a different model
2. Select from Tiny, Base, Small, Medium, or Large
3. Ensure the corresponding model file is downloaded

### Viewing Recordings

1. Select **option 7** to view all recordings
2. Select a recording number to set it as the current file for transcription

## Menu Options

```
1. Start Recording       - Begin recording audio
2. Stop Recording        - Stop the current recording
3. Transcribe Last Recording - Transcribe the most recent recording
4. Configure Whisper     - Set up Whisper executable and models paths
5. Set Language          - Choose transcription language
6. Set Model Size        - Select Whisper model (accuracy vs speed)
7. View Recordings       - List and select from previous recordings
8. Exit                  - Quit the application
```

## Technical Details

### Audio Format

The application records audio in the following format, optimized for Whisper:
- **Sample Rate**: 16,000 Hz (16 kHz)
- **Channels**: Mono (1 channel)
- **Bit Depth**: 16-bit PCM
- **Format**: WAV

If you provide audio in a different format, the application will automatically convert it using NAudio.

### Transcription Process

1. **Audio Validation**: Checks if the audio file is in Whisper-compatible format
2. **Preprocessing** (if needed): Converts audio to 16kHz mono PCM
3. **Model Loading**: Loads the selected Whisper model
4. **Chunking**: Whisper internally processes audio in 30-second segments
5. **Transcription**: Processes the entire audio file
6. **Output**: Returns the transcribed text and saves it to a file

### Model Performance

Performance characteristics on a typical CPU:

| Model  | Size    | Speed (vs Real-time) | Accuracy | Memory Usage |
|--------|---------|----------------------|----------|--------------|
| Tiny   | 75 MB   | ~10x faster          | Good     | ~1 GB        |
| Base   | 142 MB  | ~5x faster           | Better   | ~1 GB        |
| Small  | 466 MB  | ~2x faster           | High     | ~2 GB        |
| Medium | 1.5 GB  | ~1x (real-time)      | Very High| ~5 GB        |
| Large  | 2.9 GB  | ~0.5x (slower)       | Best     | ~10 GB       |

*Note: Actual performance depends on your CPU. GPU acceleration can be enabled by building Whisper.cpp with CUDA support.*

### Language Support

Whisper supports transcription in over 50 languages. Common language codes:

- `en` - English
- `es` - Spanish
- `fr` - French
- `de` - German
- `it` - Italian
- `pt` - Portuguese
- `nl` - Dutch
- `ru` - Russian
- `zh` - Chinese
- `ja` - Japanese
- `ko` - Korean
- `ar` - Arabic
- `hi` - Hindi
- `pl` - Polish
- `tr` - Turkish

For a complete list, see: https://github.com/openai/whisper#available-models-and-languages

### Accuracy Considerations

Whisper's accuracy is influenced by:
- **Audio Quality**: Clear audio with minimal background noise works best
- **Model Size**: Larger models are more accurate but slower
- **Language Hint**: Providing the correct language code improves accuracy
- **Recording Conditions**: Studio-quality recordings work better than phone recordings
- **Speaker Clarity**: Clear speech with good pronunciation is transcribed more accurately

## Troubleshooting

### "Whisper is not configured"

- Run option 4 to configure Whisper paths
- Verify the Whisper executable exists and is executable
- Check that model files are in the specified directory

### "Model not found"

- Ensure you've downloaded the model file for the selected size
- Verify the models directory path is correct
- Check that model filenames match the expected format (e.g., `ggml-base.bin`)

### Audio Recording Issues

- Check that your microphone is connected and recognized by the system
- Verify microphone permissions on your operating system
- Try adjusting microphone volume in system settings

### Slow Transcription

- Use a smaller model (Tiny or Base) for faster processing
- Consider building Whisper.cpp with GPU support (CUDA) if you have an NVIDIA GPU
- Close other resource-intensive applications

### Poor Transcription Quality

- Use a larger model (Small, Medium, or Large)
- Ensure audio is clear with minimal background noise
- Verify the correct language is selected
- Record in a quiet environment

## Architecture

### Project Structure

```
Capture/
├── Capture.csproj              # Project configuration
├── Program.cs                  # Main application entry point
├── Models/
│   └── TranscriptionOptions.cs # Configuration models
└── Services/
    ├── AudioRecorder.cs        # Audio recording with NAudio
    ├── AudioPreprocessor.cs    # Audio format conversion
    └── WhisperTranscriber.cs   # Whisper integration
```

### Key Components

- **AudioRecorder**: Uses NAudio library for cross-platform audio capture
- **AudioPreprocessor**: Converts audio to Whisper's required format using NAudio
- **WhisperTranscriber**: Interfaces with Whisper.cpp via process execution
- **TranscriptionOptions**: Encapsulates language, model, and transcription settings

## Development

### Building from Source

```bash
git clone https://github.com/RafaelMD/Capture.git
cd Capture/Capture
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Code Structure

The application follows a service-oriented architecture:
- Services handle specific concerns (recording, preprocessing, transcription)
- Models define data structures
- Main application coordinates services through a console UI

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is open source. Please check the repository for license details.

## Acknowledgments

- **OpenAI Whisper**: State-of-the-art speech recognition model
- **Whisper.cpp**: Efficient C++ implementation by Georgi Gerganov
- **NAudio**: Audio library for .NET by Mark Heath

## Resources

- Whisper.cpp: https://github.com/ggerganov/whisper.cpp
- Whisper Models: https://huggingface.co/ggerganov/whisper.cpp
- OpenAI Whisper Paper: https://cdn.openai.com/papers/whisper.pdf
- NAudio Documentation: https://github.com/naudio/NAudio

## Support

For issues, questions, or contributions, please visit the GitHub repository.