using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Capture.Services;

/// <summary>
/// Preprocesses audio files to meet Whisper requirements (16kHz mono PCM)
/// </summary>
public class AudioPreprocessor
{
    /// <summary>
    /// Convert audio file to Whisper-compatible format (16kHz mono PCM WAV)
    /// </summary>
    /// <param name="inputPath">Path to the input audio file</param>
    /// <param name="outputPath">Path where the converted file will be saved</param>
    /// <returns>True if conversion was successful</returns>
    public bool ConvertToWhisperFormat(string inputPath, string outputPath)
    {
        try
        {
            using var reader = new AudioFileReader(inputPath);
            
            // Target format: 16kHz, mono, 16-bit PCM
            var targetFormat = new WaveFormat(16000, 16, 1);
            
            // Convert to mono if needed
            ISampleProvider sampleProvider = reader;
            if (reader.WaveFormat.Channels > 1)
            {
                sampleProvider = reader.ToMono();
            }
            
            // Resample to 16kHz if needed
            if (reader.WaveFormat.SampleRate != 16000)
            {
                var resampler = new WdlResamplingSampleProvider(sampleProvider, 16000);
                sampleProvider = resampler;
            }
            
            // Write to output file
            WaveFileWriter.CreateWaveFile16(outputPath, sampleProvider);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error converting audio: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if an audio file is already in Whisper-compatible format
    /// </summary>
    /// <param name="filePath">Path to the audio file</param>
    /// <returns>True if the file is already in the correct format</returns>
    public bool IsWhisperCompatible(string filePath)
    {
        try
        {
            using var reader = new AudioFileReader(filePath);
            return reader.WaveFormat.SampleRate == 16000 
                && reader.WaveFormat.Channels == 1
                && reader.WaveFormat.BitsPerSample == 16;
        }
        catch
        {
            return false;
        }
    }
}
