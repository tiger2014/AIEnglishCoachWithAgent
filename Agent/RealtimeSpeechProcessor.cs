using NAudio.Wave;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIEnglishCoachWithAgent.Agent
{
    public class RealtimeSpeechProcessor : IDisposable
    {
        private readonly SpeechRecognizer _speechRecognizer;
        private readonly FileStream _fileStream;
        private readonly string _tempFilePath;
        private readonly IDisposable _subscription;
        private Task<string> _transcriptionTask;
        private readonly WaveFormat _waveFormat;
        private long _dataSize = 0;

        public RealtimeSpeechProcessor(SpeechRecognizer speechRecognizer, IObservable<byte[]> audioStream, WaveFormat waveFormat)
        {
            _speechRecognizer = speechRecognizer;
            _waveFormat = waveFormat;

            // Create a temporary file to store audio. This allows seeking.
            _tempFilePath = Path.GetTempFileName();
            _fileStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

            WriteWavHeader();

            // Subscribe to the audio stream and write data to the FileStream
            _subscription = audioStream.Subscribe(onNext: buffer =>
            {
                if (_fileStream.CanWrite)
                {
                    _fileStream.Write(buffer, 0, buffer.Length);
                    _fileStream.Flush(); // Ensure data is written to disk for the reader
                    _dataSize += buffer.Length;
                }
            }, onCompleted: () =>
            {
                // When recording stops, update the header.
                UpdateWavHeader();
            });
        }

        public async Task StartAsync()
        {
            // Start the transcription task, which reads from the FileStream.
            // The stream is readable even while we are writing to it.
            _transcriptionTask = _speechRecognizer.TranscribeAsync(_fileStream);
            await Task.CompletedTask; // Keep async signature
        }

        public async Task<string> StopAndGetResultAsync()
        {
            _subscription.Dispose(); // Stop listening for new audio data

            // The onCompleted action from the subscription will handle the final header update.
            // We just need to wait for the already-running transcription task to finish.
            return await _transcriptionTask;
        }

        private void WriteWavHeader()
        {
            // Use a BinaryWriter but keep the underlying stream open.
            var writer = new BinaryWriter(_fileStream, Encoding.UTF8, true);

            // RIFF header
            writer.Write(Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(0); // Placeholder for file size (4 bytes)
            writer.Write(Encoding.UTF8.GetBytes("WAVE"));

            // "fmt " sub-chunk
            writer.Write(Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16); // Sub-chunk size for PCM
            writer.Write((short)_waveFormat.Encoding);
            writer.Write((short)_waveFormat.Channels);
            writer.Write(_waveFormat.SampleRate);
            writer.Write(_waveFormat.AverageBytesPerSecond);
            writer.Write((short)_waveFormat.BlockAlign);
            writer.Write((short)_waveFormat.BitsPerSample);

            // "data" sub-chunk
            writer.Write(Encoding.UTF8.GetBytes("data"));
            writer.Write(0); // Placeholder for data size (4 bytes)
        }

        private void UpdateWavHeader()
        {
            if (!_fileStream.CanWrite || !_fileStream.CanSeek) return;

            var writer = new BinaryWriter(_fileStream, Encoding.UTF8, true);

            // File size
            writer.Seek(4, SeekOrigin.Begin);
            writer.Write((int)(_dataSize + 36)); // 36 bytes for header before data

            // Data size
            writer.Seek(40, SeekOrigin.Begin);
            writer.Write((int)_dataSize);
            writer.Flush();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _fileStream?.Close(); // This also disposes the stream

            // Clean up the temporary file
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }
    }
}
