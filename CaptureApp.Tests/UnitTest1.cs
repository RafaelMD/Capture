namespace CaptureApp.Tests;

public class AudioRecorderTests
{
    [Fact]
    public void AudioRecorder_CanBeInstantiated()
    {
        // Arrange & Act
        using var recorder = new AudioRecorder();

        // Assert
        Assert.NotNull(recorder);
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void AudioRecorder_IsRecording_InitiallyFalse()
    {
        // Arrange
        using var recorder = new AudioRecorder();

        // Act & Assert
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void AudioRecorder_Dispose_DoesNotThrow()
    {
        // Arrange
        var recorder = new AudioRecorder();

        // Act & Assert
        var exception = Record.Exception(() => recorder.Dispose());
        Assert.Null(exception);
    }
}
