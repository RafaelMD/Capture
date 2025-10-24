# Capture

## Designing a Windows Desktop Audio Recorder with Whisper Transcription

### Overview
Our goal is to build a Windows 10/11 desktop application (written in C#) that can record everything the user hears and says.
The recorder must:

* Capture the default playback stream ("what you hear") and the default microphone simultaneously.
* Operate in shared mode so it never blocks other applications from using the audio devices.
* Continue recording when the UI is minimized or hidden.
* Persist the captured audio to disk for later offline transcription with OpenAI Whisper.
* Allow the user to pick the spoken language before recording to guide the transcription model.

### Audio Capture Architecture
We use Windows Core Audio (WASAPI) in shared mode—either directly or via a wrapper such as [NAudio](https://github.com/naudio/NAudio)—because it supports both microphone capture and loopback capture of the system playback mix.
Shared mode keeps Windows' software mixer in charge, allowing multiple apps to access the microphone and speakers concurrently without device contention.

#### System Audio (Loopback) Capture
* Open the default render device in loopback mode (`WasapiLoopbackCapture` in NAudio).
* Receive PCM audio buffers through the capture callback (e.g., `DataAvailable`).
* Configure the capture to use a standard format (16-bit PCM at 44.1 kHz or 48 kHz).

#### Microphone Capture
* Open the default capture endpoint with `WasapiCapture` or `WaveInEvent` in shared mode.
* Use the same sample rate and bit depth as the loopback stream to simplify mixing.
* Handle device-sharing scenarios gracefully—the microphone remains available to other apps.

### Mixing the Streams
We need a single mixed stream to simplify transcription and playback.

1. Convert both sources to a common format (e.g., 16-bit PCM, 16 kHz mono) if necessary.
2. Feed each source into a mixer such as NAudio's `MixingSampleProvider`.
3. Sum the samples with headroom to avoid clipping; optionally apply simple attenuation if needed.
4. Output a unified mono (preferred for Whisper) or stereo stream.

### Recording Pipeline
* Initialize a `WaveFileWriter` (or equivalent) with the target format.
* As mixed buffers arrive, append them to the WAV file.
* Continue writing until the user stops recording, then dispose the writer to finalize the header.
* Ensure sufficient disk space; long sessions produce large uncompressed files.

### Synchronization
Starting the microphone and loopback captures back-to-back keeps the streams aligned.
Using a single mixer that pulls from both inputs maintains time synchronization.
Identical sample rates and buffer sizes reduce drift; any minor misalignment is typically inconsequential for transcription.

### Background Operation & UI
* Provide a minimal UI with:
  * Language selection drop-down.
  * Record/Stop toggle button.
  * Optional Transcribe button to trigger Whisper post-processing.
* Once recording begins, keep capture/mixing/writing on background threads to avoid UI freezes.
* Allow the window to be minimized or hidden while continuing to record; optionally show a tray icon or status indicator to remind the user that recording is active.
* On Stop, halt both captures, dispose resources, and re-enable the transcription controls.

### Whisper Transcription (Offline)
* Whisper requires mono, 16 kHz PCM input. If the recorded WAV differs, resample and downmix before transcription (NAudio or FFmpeg can handle this).
* Run Whisper locally—via a bundled Python script or a native port such as [whisper.cpp](https://github.com/ggerganov/whisper.cpp).
* Load the user-selected model size (tiny/base/small/medium/large) depending on speed vs. accuracy requirements.
* Pass the user’s language choice to Whisper (e.g., `--language <code>`) to improve accuracy and avoid unintended translation.
* Execute transcription on a background thread, displaying progress feedback in the UI.
* Present the resulting text to the user and optionally save it alongside the audio file.

### Implementation Outline
1. **Audio initialization** – enumerate devices (optional), set up WASAPI/NAudio capture objects, configure formats, and initialize the mixer plus file writer.
2. **UI workflow** – disable language changes during recording, start both captures on Record, and ensure Stop cleans up resources and finalizes the WAV.
3. **Storage** – save recordings in WAV format; consider post-session compression as an optional feature.
4. **Transcription** – when requested, preprocess audio for Whisper, run the model locally, and display/save the transcript.
5. **Quality of life (optional)** – playback controls, timestamped segments from Whisper, system tray integration, or selectable output formats.

### References
* Windows WASAPI shared mode overview – [Audiophile Style](https://audiophilestyle.com/forums/topic/64625-windows-11-wasapi/)
* NAudio capture & WAV writing example – [Stack Overflow](https://stackoverflow.com/questions/17982468/naudio-record-sound-from-microphone-then-save)
* Whisper model details – [OpenAI Whisper announcement](https://openai.com/index/whisper/)
