# Capture

A Windows 10/11 desktop application that records system audio and microphone input with automatic Whisper transcription.

## Features

- Capture system audio (loopback) and microphone simultaneously
- Real-time mixing of audio streams
- Offline transcription using Whisper AI
- Multiple language support
- Integration with Jaboo API for transcription management

## Prerequisites

- Windows 10/11
- .NET 8.0 SDK or later
- 7-Zip or similar tool to extract compressed files

## Build and Run Instructions

### 1. Build the Project

Navigate to the project root directory and build using .NET CLI:

```bash
cd src/CaptureApp
dotnet build
```

Or build the entire solution:

```bash
dotnet build Capture.sln
```

### 2. Install Whisper Model

Download and set up the Whisper transcription engine:

1. Download Faster-Whisper-XXL from the following URL:
   ```
   https://github.com/Purfview/whisper-standalone-win/releases/download/Faster-Whisper-XXL/Faster-Whisper-XXL_r245.4_windows.7z
   ```

2. Extract the downloaded `.7z` file using 7-Zip or a similar tool

3. Create a `whisper` folder in the application's output directory:
   ```
   src/CaptureApp/bin/Debug/net8.0-windows/whisper/
   ```

4. Copy all extracted files into the `whisper` folder

### 3. Configure Jaboo API

1. Go to [https://jaboo.lovable.app/](https://jaboo.lovable.app/)

2. Create an account or sign in

3. Navigate to the API settings and generate a new API key

4. Save the API key - you'll need it in the next step

### 4. Run the Application

Run the application:

```bash
dotnet run
```

Once the application starts:

1. Click on the **Settings** button/menu
2. Enter your Jaboo API key in the appropriate field
3. Save the settings

You're now ready to start recording and transcribing audio!

## Usage

1. Select your preferred language from the dropdown
2. Click **Record** to start capturing audio
3. Click **Stop** when finished
4. The application will automatically transcribe the recording using Whisper
5. View transcriptions in the application or sync them to Jaboo

## Technical Overview

- Uses NAudio for audio capture via WASAPI in shared mode
- Records both system audio (loopback) and microphone input
- Mixes streams in real-time
- Saves recordings as WAV files
- Processes transcriptions using Faster-Whisper
- Syncs transcriptions with Jaboo API for cloud storage

### Architecture Details

The application uses Windows Core Audio (WASAPI) in shared mode via NAudio for concurrent capture of:

- **System Audio (Loopback)**: Captures the default render device output
- **Microphone Input**: Captures the default recording device

Both streams are mixed in real-time and saved as WAV files for offline transcription with Whisper AI.
