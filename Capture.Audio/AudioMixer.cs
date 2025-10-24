using NAudio.Wave;
using System;

namespace Capture.Audio
{
    /// <summary>
    /// Provides functionality to mix two audio streams into a single output stream.
    /// </summary>
    public class AudioMixer : IDisposable
    {
        private readonly WaveFormat _targetFormat;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the AudioMixer class.
        /// </summary>
        /// <param name="targetFormat">The target wave format for the mixed output.</param>
        public AudioMixer(WaveFormat targetFormat)
        {
            _targetFormat = targetFormat ?? throw new ArgumentNullException(nameof(targetFormat));
        }

        /// <summary>
        /// Mixes two audio buffers into a single output buffer.
        /// Both input buffers must be in the same format as the target format.
        /// </summary>
        /// <param name="buffer1">First audio buffer.</param>
        /// <param name="buffer2">Second audio buffer.</param>
        /// <param name="outputBuffer">Output buffer to store the mixed audio.</param>
        /// <param name="length">Length of data to mix in bytes.</param>
        public void MixBuffers(byte[] buffer1, byte[] buffer2, byte[] outputBuffer, int length)
        {
            if (buffer1 == null) throw new ArgumentNullException(nameof(buffer1));
            if (buffer2 == null) throw new ArgumentNullException(nameof(buffer2));
            if (outputBuffer == null) throw new ArgumentNullException(nameof(outputBuffer));

            // For 16-bit PCM audio, we need to mix samples as 16-bit integers
            if (_targetFormat.BitsPerSample == 16)
            {
                Mix16BitBuffers(buffer1, buffer2, outputBuffer, length);
            }
            else
            {
                throw new NotSupportedException($"Mixing {_targetFormat.BitsPerSample}-bit audio is not supported.");
            }
        }

        private void Mix16BitBuffers(byte[] buffer1, byte[] buffer2, byte[] outputBuffer, int length)
        {
            int sampleCount = length / 2; // 16-bit = 2 bytes per sample
            
            for (int i = 0; i < sampleCount; i++)
            {
                int byteOffset = i * 2;
                
                // Read 16-bit samples from both buffers
                short sample1 = BitConverter.ToInt16(buffer1, byteOffset);
                short sample2 = BitConverter.ToInt16(buffer2, byteOffset);
                
                // Mix the samples (simple addition with clipping)
                int mixedSample = sample1 + sample2;
                
                // Clip to prevent overflow
                if (mixedSample > short.MaxValue)
                    mixedSample = short.MaxValue;
                else if (mixedSample < short.MinValue)
                    mixedSample = short.MinValue;
                
                // Write back to output buffer
                byte[] mixedBytes = BitConverter.GetBytes((short)mixedSample);
                outputBuffer[byteOffset] = mixedBytes[0];
                outputBuffer[byteOffset + 1] = mixedBytes[1];
            }
        }

        /// <summary>
        /// Disposes the AudioMixer and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by the AudioMixer.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // No managed resources to dispose in this implementation
                }
                _disposed = true;
            }
        }
    }
}
