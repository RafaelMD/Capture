# Setup Guide for Whisper.cpp

This guide will help you install and configure Whisper.cpp for use with the Capture application.

## Quick Start

### Linux/macOS

```bash
# 1. Install build dependencies (if not already installed)
# On Ubuntu/Debian:
sudo apt-get update
sudo apt-get install build-essential git

# On macOS with Homebrew:
brew install cmake

# 2. Clone and build Whisper.cpp
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp
make

# 3. Download a model (Base model recommended for starting)
bash ./models/download-ggml-model.sh base

# 4. Test the installation
./main -f samples/jfk.wav -m models/ggml-base.bin

# 5. Set environment variables for Capture
export WHISPER_EXECUTABLE="$(pwd)/main"
export WHISPER_MODELS_DIR="$(pwd)/models"

# 6. Run Capture
cd ../Capture/Capture
dotnet run
```

### Windows

#### Option 1: Download Pre-built Binaries

1. Visit https://github.com/ggerganov/whisper.cpp/releases
2. Download the latest release for Windows
3. Extract to a folder (e.g., `C:\whisper.cpp`)

#### Option 2: Build from Source

Requirements:
- Visual Studio 2019 or later with C++ support
- CMake
- Git

```powershell
# 1. Clone the repository
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp

# 2. Create build directory
mkdir build
cd build

# 3. Generate Visual Studio solution
cmake ..

# 4. Build (or open whisper.sln in Visual Studio)
cmake --build . --config Release

# 5. The executable will be in build/bin/Release/main.exe
```

#### Download Models

```powershell
# Download a model using PowerShell
cd whisper.cpp\models
Invoke-WebRequest -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin" -OutFile "ggml-base.bin"
```

#### Set Environment Variables

```powershell
# Temporary (current session only)
$env:WHISPER_EXECUTABLE="C:\whisper.cpp\build\bin\Release\main.exe"
$env:WHISPER_MODELS_DIR="C:\whisper.cpp\models"

# Permanent (system-wide)
[System.Environment]::SetEnvironmentVariable("WHISPER_EXECUTABLE", "C:\whisper.cpp\build\bin\Release\main.exe", "User")
[System.Environment]::SetEnvironmentVariable("WHISPER_MODELS_DIR", "C:\whisper.cpp\models", "User")
```

## Downloading Models

### Available Models

| Model  | Size    | Download Link |
|--------|---------|---------------|
| Tiny   | 75 MB   | https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin |
| Base   | 142 MB  | https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin |
| Small  | 466 MB  | https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin |
| Medium | 1.5 GB  | https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin |
| Large  | 2.9 GB  | https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin |

### Download Using Script (Linux/macOS)

```bash
cd whisper.cpp
bash ./models/download-ggml-model.sh base    # Downloads base model
bash ./models/download-ggml-model.sh small   # Downloads small model
# etc.
```

### Manual Download

1. Visit https://huggingface.co/ggerganov/whisper.cpp/tree/main
2. Click on the model file you want to download
3. Click the "Download" button
4. Save to your models directory

## GPU Acceleration (Optional)

For significantly faster transcription, you can build Whisper.cpp with GPU support.

### NVIDIA CUDA (Linux/Windows)

Requirements:
- NVIDIA GPU with CUDA support
- CUDA Toolkit installed

```bash
# Clone repository
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp

# Build with CUDA support
make clean
make WHISPER_CUDA=1

# The resulting executable will use GPU acceleration
```

### Apple Metal (macOS)

Metal support is enabled by default on macOS with Apple Silicon.

```bash
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp
make

# Metal acceleration is automatically used on Apple Silicon Macs
```

### OpenCL (Cross-platform)

```bash
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp
make WHISPER_CLBLAST=1
```

## Verifying Installation

### Test Whisper.cpp

```bash
# Download a test audio file
wget https://github.com/ggerganov/whisper.cpp/raw/master/samples/jfk.wav

# Run transcription
./main -f jfk.wav -m models/ggml-base.bin -l en

# You should see the transcription of JFK's speech
```

### Test with Capture

1. Run the Capture application:
   ```bash
   cd Capture/Capture
   dotnet run
   ```

2. Select option 4 to configure Whisper
3. Enter the paths to your Whisper executable and models directory
4. The application will verify the installation

## Troubleshooting

### "Whisper executable not found"

- Verify the path to the executable is correct
- On Linux/macOS, ensure the file has execute permissions: `chmod +x main`
- Check that the file actually exists at the specified path

### "Model file not found"

- Verify the model files are in the correct directory
- Check that model filenames match exactly (case-sensitive on Linux/macOS)
- Re-download the model if it's corrupted

### "Permission denied" (Linux/macOS)

```bash
# Make the executable file executable
chmod +x /path/to/whisper.cpp/main
```

### Slow Performance

- Try using a smaller model (Tiny or Base)
- Consider building with GPU acceleration
- Close other applications to free up system resources
- Check CPU usage - Whisper is computationally intensive

### "libomp.so not found" (Linux)

```bash
# Install OpenMP library
sudo apt-get install libomp-dev
```

### Build Errors on Windows

- Ensure Visual Studio has C++ development tools installed
- Update CMake to the latest version
- Try building with Visual Studio GUI instead of command line

## Performance Optimization

### CPU Optimization

For better performance on CPU:

```bash
# Build with optimizations
make clean
make WHISPER_OPENBLAS=1  # Use OpenBLAS for better performance
```

### Memory Optimization

If you have limited RAM:
- Use smaller models (Tiny or Base)
- Close other applications
- Process shorter audio segments

### Disk Space Management

- Models can be shared across multiple applications
- You don't need to download all models - start with Base
- Old recordings can be archived or deleted

## Additional Resources

- Whisper.cpp GitHub: https://github.com/ggerganov/whisper.cpp
- Whisper.cpp Wiki: https://github.com/ggerganov/whisper.cpp/wiki
- OpenAI Whisper: https://github.com/openai/whisper
- Model Cards: https://huggingface.co/ggerganov/whisper.cpp

## Getting Help

If you encounter issues:
1. Check the Whisper.cpp GitHub issues
2. Verify your installation using the test steps above
3. Check system requirements (CPU, RAM, disk space)
4. Review error messages carefully
