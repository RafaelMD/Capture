using System;

namespace Capture.Audio
{
    /// <summary>
    /// Interface for audio capture service that supports capturing system audio and microphone input.
    /// </summary>
    public interface IAudioCaptureService : IDisposable
    {
        /// <summary>
        /// Starts recording audio from both system output (loopback) and microphone input.
        /// </summary>
        /// <param name="outputFilePath">Path to the output WAV file.</param>
        void StartRecording(string outputFilePath);

        /// <summary>
        /// Stops the current recording session.
        /// </summary>
        void StopRecording();

        /// <summary>
        /// Gets a value indicating whether recording is currently active.
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Event raised when an error occurs during recording.
        /// </summary>
        event EventHandler<string>? RecordingError;
    }
}
