using NAudio.Wave;
using System.IO;

public class MicrophoneRecorder
{
    private WaveInEvent _waveIn;
    private MemoryStream _memoryStream;
    private WaveFileWriter _writer;

    public bool IsRecording { get; private set; }

    public void StartRecording()
    {
        _memoryStream = new MemoryStream();

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1) // Whisper 推荐采样率
        };

        _writer = new WaveFileWriter(_memoryStream, _waveIn.WaveFormat);

        _waveIn.DataAvailable += (s, e) =>
        {
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
        };

        _waveIn.StartRecording();
        IsRecording = true;
    }

    public Stream StopRecording()
    {
        if (!IsRecording) return Stream.Null;

        _waveIn.StopRecording();
        _writer.Flush();
        _memoryStream.Position = 0; // 重置流位置以便后续读取
        IsRecording = false;

        return _memoryStream;
    }
}
