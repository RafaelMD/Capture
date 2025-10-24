# Capture

Audio capture library for Windows supporting simultaneous recording of system audio and microphone input.

## Overview

This repository contains the Capture.Audio library, which implements a comprehensive audio capture solution using Windows Core Audio APIs (WASAPI). The library enables applications to record both system output audio (loopback) and microphone input simultaneously, mixing them into a single audio file.

## Features

- **Dual Audio Capture**: Simultaneously captures system audio (what-you-hear) and microphone input
- **WASAPI Shared Mode**: Uses Windows Core Audio APIs in shared mode for non-blocking audio access
- **Real-time Audio Mixing**: Mixes both audio streams in real-time into a single output
- **WAV File Recording**: Saves the mixed audio to standard WAV files
- **Thread-safe Operation**: Handles concurrent audio streams with proper synchronization

## Projects

- **Capture.Audio**: Core audio capture library implementing WASAPI-based recording

## Getting Started

See [Capture.Audio/README.md](Capture.Audio/README.md) for detailed documentation on using the audio capture library.

## Requirements

- .NET 9.0 or later
- Windows operating system
- NAudio library

## Architecture

The library uses WASAPI (Windows Audio Session API) to capture audio:
- System audio is captured via loopback capture on the default playback device
- Microphone input is captured from the default input device
- Both streams are mixed in real-time using 16-bit PCM at 44100 Hz
- The mixed output is written to a WAV file for further processing or transcription

## License

See LICENSE file for details.