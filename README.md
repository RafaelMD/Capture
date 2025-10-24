# Capture

# Designing a Windows Desktop Audio Recorder with Whisper Transcription
## Overview
We aim to build a C# desktop application (for Windows 10/11) that can capture all audio on the PC – including the user’s microphone and system playback (audio from other applications) – and record it for later transcription. The app will run in the background once recording starts, and must not interfere with other applications’ use of the microphone (i.e. it should capture audio in a shared/non-exclusive mode). For transcription, we will use OpenAI’s Whisper running locally (offline). The user can choose the language of speech before recording starts, ensuring Whisper knows which language to transcribe. Below, we outline the design of this software, covering audio capture, background operation, and the Whisper integration for transcription.

## Audio Capture Design (Microphone + System Audio)
Choosing an Audio API: On modern Windows, the recommended way to capture audio is via the WASAPI (Windows Core Audio) APIs or a higher-level library like NAudio that wraps these APIs. WASAPI supports loopback capture for recording system output audio, and standard capture for microphones. We will use WASAPI in shared mode for both input and output streams, so that our app doesn’t take exclusive control of the devices. (In shared mode, Windows mixes audio streams and allows multiple apps to access the audio devices simultaneously audiophilestyle.com .) This ensures our recorder does not block other applications from using the microphone or speakers while recording. Capturing System Audio (What-You-Hear): We will open the default playback device in loopback mode. This gives us a stream of all audio being played by the system (music, calls, videos, etc.) as if it were an input source. In NAudio, for example, this is done with WasapiLoopbackCapture on the default audio render device. The loopback capture provides raw PCM audio data via an event callback (e.g. DataAvailable events). 

Capturing Microphone Input: Simultaneously, we open the user’s microphone (default input device) for capture. This could be done with NAudio’s WaveInEvent or WasapiCapture targeting the input device. We use shared mode here as well, so if another app (e.g. a videoconferencing app) is using the mic, our capture runs in parallel without issue. We configure the mic capture format – typically 16-bit PCM audio at a standard sample rate (e.g. 44100 Hz or 48000 Hz). We will likely choose the same sample rate and format for both mic and system captures to simplify merging them. 

Mixing Audio Streams: We have two audio sources (mic and system). For a single combined recording, we need to mix these two streams into one. A simple approach is to mix down to two-channel audio: e.g. one channel for system sound and one for mic (though that complicates transcription), or simply mix both into one mono/stereo track. The design choice here: for transcription, a single mixed audio track (mono) is preferable. We can convert both streams to a common format (e.g. 16-bit, 16 kHz mono) and sum them in real-time. Using a library, we can employ a mixing provider (NAudio provides MixingSampleProvider) to add the two input streams and produce one mixed output. This mixed audio stream can then be written to an output file. Alternatively, we could write the two sources to separate tracks (e.g. two audio files) for potential separate processing, but merging them for transcription later would add complexity. Real-time mixing into one file is straightforward and ensures the transcript covers both sides of the audio. 

Recording to File: The captured audio data (mixed stream) will be written to a WAV file (for highest quality and easy processing). As audio buffers come in from the capture APIs, we append them to the wave file on disk. We must ensure the file writer is initialized with the correct format (matching our capture format). For example, if using NAudio: initialize a WaveFileWriter with the chosen WaveFormat. As data arrives in the DataAvailable event, write it into the file. (Simply playing the audio to speakers is not sufficient – we need to buffer and save the audio data for later use stackoverflow.com .) We continue recording until the user stops or pauses the recording. Timing and Sync: Because we capture two streams, we need to ensure they stay time-synchronized in the mixed output. If using a single thread and mixing provider for both, this is handled. If capturing on separate threads, we’ll merge based on timestamps or buffer sizes. In practice, using one loopback capture and one mic capture in parallel is feasible; we start them at (almost) the same time. Any small drift might not be noticeable for transcription purposes, but using the same sample rate and buffering strategy reduces sync issues.

## Background Recording and UI Considerations

User Interface: The app will have a simple UI with a Record button (and possibly a Stop button). The user can select the language from a dropdown (for transcription) before recording. Once Record is clicked, the app begins capturing audio as described. At this point, we may hide or minimize the window – the recording should continue in the background until stopped. We can show an indicator (e.g. a system tray icon or a small “recording…” label) to remind the user that recording is active. The Record button would toggle to a Stop button, or we provide a separate Stop. Background Operation: To avoid freezing the UI, the audio capture and file writing run on background threads. Libraries like NAudio handle this by raising events on capture threads which we handle asynchronously. We must ensure to properly stop and dispose the capture objects and file writer when done (e.g. on Stop button). This flushes the WAV file headers so it’s not corrupted. After stopping, the user can choose to start again (new file) or proceed to transcription. 

Non-Blocking Audio Devices: Because we use shared mode WASAPI, other apps can continue to use the microphone and speakers during recording. For instance, if the user is in a video call, our app quietly captures the audio without affecting the call. We do not open the devices in exclusive mode. This design satisfies the requirement that the mic isn’t “locked” by our recorder. (Windows audio mixing in shared mode allows concurrent capture/playback audiophilestyle.com.) Performance Considerations: Capturing and writing audio is not very CPU intensive, but mixing two streams doubles the data throughput. Still, on a modern system this should be fine (a few MB per minute). The WAV file will grow potentially large for long recordings, so ensure the storage location has space. We might allow an option to record to a compressed format (like WMA/MP3) to save space, but that complicates later transcription (Whisper works best with uncompressed audio). It’s simplest to record WAV then optionally compress after transcription if needed.
Transcription with Whisper (Offline ASR)

Using Whisper Locally: Once a recording is done (or even during, if we wanted real-time transcription, though the prompt says “transcribe later”), the user can initiate transcription. We use OpenAI Whisper, which is an open-source automatic speech recognition model openai.com. Running Whisper locally means we need the model files and an inference engine on the user’s machine. There are a few ways to do this in a C# project: for example, call a Python script that uses the whisper library, or use Whisper.cpp (a C++ port of Whisper) with P/Invoke or a .NET wrapper. Since we prefer everything local and self-contained, using Whisper.cpp (which is optimized for CPU inference) could be ideal. We would load the model (the user may choose a size like tiny/base/large depending on accuracy vs speed) and then pass the recorded audio file to it for transcription. Language Selection: Before recording, the user selects the language of the speech (e.g. English, Spanish, etc.). We will use this information when running Whisper. Whisper can auto-detect language, but providing a hint improves accuracy and ensures it transcribes in the original language (not translating unless we want it to). For example, we run Whisper with a --language parameter or corresponding API call. (Whisper is multilingual and was trained to handle many languages openai.com , so users can record in their chosen language and get a transcript in the same language.) If needed, we could also offer an option to translate to English using Whisper’s translation mode, but that’s beyond the base requirements. Transcription Process: After the user hits Stop (or a “Transcribe” button), the app will:
If not already in PCM mono 16 kHz format, pre-process the audio file (Whisper expects 16 kHz mono audio). This might involve resampling and mixing down to mono. If we recorded in stereo or higher sample rate, we use an audio library (NAudio or FFmpeg) to convert the WAV for Whisper’s input.

Load the Whisper model (this could be somewhat memory heavy; large models may require a good CPU/GPU).
Run the transcription. This can take some time depending on audio length and model size (possibly real-time or slower-than-real-time on CPU). Since this is a background operation, we can show a progress bar or spinner.
Once done, display the transcribed text to the user, and optionally save it to a text file or allow copy.
Because Whisper processes audio in chunks (30-second segments) openai.com , for long recordings the tool will internally split the audio. We should ensure the entire audio is fed to Whisper – usually providing the whole file is fine as the Whisper code will chunk it internally. Accuracy and Model Selection: We should document that Whisper’s accuracy is very good for a variety of languages and even noisy environments
openai.com . However, larger Whisper models are more accurate but slower. Depending on user needs, we could allow choosing a model (tiny, base, small, medium, large). For local use without a high-end GPU, the small or medium models might be a good trade-off. We also ensure the user knows that transcription will use CPU (or GPU if we integrate CUDA) and might be resource-intensive for a while.
Implementation Outline

To summarize the design, here’s a high-level breakdown of components and steps in our C# project:
Audio Capture Setup: On app startup, initialize audio capture devices. Use Windows Core Audio APIs (via NAudio for ease). Enumerate devices if we want to let user choose, or just pick the system default input/output. Prepare a WaveFileWriter for output. If mixing two sources, prepare a mixer that outputs to the writer. Ensure format consistency (e.g. 16-bit PCM, 48kHz stereo or 16kHz mono). No exclusive mode.
UI Controls: The main form has:

A Language dropdown (with a list of languages Whisper supports).
A Record button (which toggles to Stop while recording).
Optionally, a Transcribe button (if we allow manual trigger after stopping; or we can auto-transcribe on stop).
Recording Start: When user hits Record:
Disable language selection UI (to lock choice), initialize the capture streams.
Start the microphone capture and loopback capture essentially at the same time.
Each capture’s DataAvailable handler provides audio buffers. We either mix them on the fly or store them if needed. If using separate handlers, we can mix by writing both into the WaveFile (simple approach: add sample amplitudes, taking care to avoid overflow if both are loud – or use a proper mixer class).
Continue until stop is triggered.
Background Operation: Allow the app window to be hidden or user to switch apps. The recording continues until Stop. Possibly implement a system tray icon with a context menu to stop, for convenience.
Stop Recording: When Stop is clicked:
Stop both captures (WASAPI captures have a Stop function). This will typically fire a RecordingStopped event which we handle to know capture ended.
Dispose the capture objects.
Close the WaveFileWriter (this finalizes the WAV file header).
Re-enable the Transcribe button (now that we have a completed recording).
Transcription Step: When Transcribe is initiated:
Take the recorded WAV file (or its path) and feed it to the Whisper inference process. If using a library, call the function; if using a CLI/exec approach, run the process.
Pass the selected language code to Whisper to guide transcription.
Wait for completion (this could be done on a background thread to keep UI responsive).
Retrieve the output text. Display it in the UI (e.g. in a text box) and/or save to a .txt file alongside the audio file.
Allow the user to copy or edit the text if needed.
User Experience: The user can then see the transcribed text of everything that was said and played during the recording session. If the audio contained, for example, a two-way conversation (mic and speaker), all of it is now in text form.
Additional Features (optional): We could log timestamps or segments (Whisper can provide timestamps for phrases). We might also allow the user to replay the recorded audio within our app (provide a simple audio player for the WAV file) to verify or listen to specific parts. But these are extra features beyond the core ask.
By following this design, we create a desktop app that effectively turns your PC into a conversation recorder: it captures both your microphone and system audio in real time without disrupting other apps, saves the audio locally, and later transcribes it using Whisper’s powerful offline ASR model. The use of local resources (no cloud services) means everything stays on the user’s machine (which could be important for privacy). With Whisper’s open-source model available
openai.com
, the transcription can be done quickly and supports the chosen language of the user’s audio. Overall, the key challenges (simultaneous audio capture, mixing, and offline transcription) are addressed with this design:
We leverage Windows audio capabilities for simultaneous capture (loopback + mic).
We ensure non-blocking audio sharing by using shared mode WASAPI
audiophilestyle.com
.
We properly buffer and save audio data for later processing
stackoverflow.com
.
We integrate a state-of-the-art local transcription engine (Whisper), taking advantage of its multilingual support and open-source availability for offline use
openai.com
.
With careful implementation of each module (audio capture, background recording, and transcription), this C# application will meet the requirements and provide a seamless experience for recording and transcribing any audio heard or spoken on the system.
Sources
Windows WASAPI Shared Mode – allows multiple applications to use audio devices concurrently
audiophilestyle.com
.
NAudio usage example – capturing audio data and buffering it for saving (writing to WAV)
stackoverflow.com
.
OpenAI Whisper – open-sourced ASR model supporting multilingual transcription for offline use
openai.com
.
Citations
Windows 11 / WASAPI - Questions and Answers - Audiophile Style

https://audiophilestyle.com/forums/topic/64625-windows-11-wasapi/

c# - naudio record sound from microphone then save - Stack Overflow

https://stackoverflow.com/questions/17982468/naudio-record-sound-from-microphone-then-save

Introducing Whisper | OpenAI

https://openai.com/index/whisper/

Introducing Whisper | OpenAI

https://openai.com/index/whisper/
