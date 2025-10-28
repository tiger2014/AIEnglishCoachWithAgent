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

        Stream streamToReturn;

        try
        {
            _waveIn?.StopRecording();
        }
        finally
        {
            _writer?.Dispose(); // 这会更新WAV文件头并关闭 _memoryStream
            _waveIn?.Dispose();

            // 因为 _memoryStream 已被关闭，我们需要创建一个新的流来返回数据
            streamToReturn = new MemoryStream(_memoryStream.ToArray());
            streamToReturn.Position = 0;

            _writer = null;
            _waveIn = null;
            IsRecording = false;
        }

        return streamToReturn;
    }
}
