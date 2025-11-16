using AIEnglishCoachWithAgent.Agent;
using NAudio.Wave;
using System.IO;

public class MicrophoneRecorder
{
    private WaveInEvent _waveIn;
    private MemoryStream _memoryStream;
    private WaveFileWriter _writer;
    //新增：Vosk 实时流
    private PipedStream _pipedStream;
    public Stream LiveStream => _pipedStream?.Reader; // 暴露实时流供 Vosk 使用

    public bool IsRecording { get; private set; }

    public void StartRecording()
    {
        _memoryStream = new MemoryStream();

        _pipedStream = new PipedStream();

        _waveIn = new WaveInEvent
        {
            // Explicitly set Sample Rate, Bit Depth (16), and Channels (1)
            WaveFormat = new WaveFormat(16000, 16, 1)
        };

        _writer = new WaveFileWriter(_memoryStream, _waveIn.WaveFormat);

        _waveIn.DataAvailable += OnDataAvailable;

        _waveIn.StartRecording();
        IsRecording = true;
    }

    public Stream StopRecording()
    {
        if (!IsRecording) return Stream.Null;

        IsRecording = false;

        // 1. Stop recording. This will flush any remaining buffers through the DataAvailable event.
        _waveIn?.StopRecording();

        // 2. Unsubscribe from the event now that we are sure no more events will be raised.
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
        }

        // 3. Dispose the WaveIn device
        _waveIn?.Dispose();
        _waveIn = null;

        // 4. Close the live stream writer for Vosk
        try
        {
            _pipedStream?.Writer?.Dispose();
        }
        catch { /* Ignore errors if already closed */ }
        _pipedStream = null;

        // 5. Dispose the WaveFileWriter, which finalizes the WAV header in the memory stream
        _writer?.Dispose();
        _writer = null;

        // 6. Create a new stream from the buffer to return.
        // _memoryStream is closed by WaveFileWriter, so we must use its internal buffer.
        var streamToReturn = new MemoryStream(_memoryStream.ToArray());
        streamToReturn.Position = 0;

        return streamToReturn;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // This handler might be called one last time after StopRecording begins.
        // We check IsRecording. If it's false, it means StopRecording has been called,
        // but we still want to process this final buffer.
        // The null check on _writer is a final safety net.
        if (_writer == null) return; 

        _writer.Write(e.Buffer, 0, e.BytesRecorded);

        // It's possible the piped stream writer has been disposed if the consumer (Vosk) finished early.
        try
        {
            _pipedStream?.Writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
        catch (ObjectDisposedException)
        {
            // This is expected if the reader has been closed, so we can ignore it.
        }
    }
}
