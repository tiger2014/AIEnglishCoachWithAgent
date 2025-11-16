using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <summary>
/// A wrapper stream that ignores calls to Dispose.
/// Useful for passing a stream to a writer that would otherwise close it.
/// </summary>
internal class IgnoreDisposeStream : Stream
{
    public Stream SourceStream { get; }
    public IgnoreDisposeStream(Stream sourceStream) => SourceStream = sourceStream;
    public override bool CanRead => SourceStream.CanRead;
    public override bool CanSeek => SourceStream.CanSeek;
    public override bool CanWrite => SourceStream.CanWrite;
    public override long Length => SourceStream.Length;
    public override long Position { get => SourceStream.Position; set => SourceStream.Position = value; }
    public override void Flush() => SourceStream.Flush();
    public override int Read(byte[] buffer, int offset, int count) => SourceStream.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => SourceStream.Seek(offset, origin);
    public override void SetLength(long value) => SourceStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => SourceStream.Write(buffer, offset, count);
    protected override void Dispose(bool disposing) { /* By design, this does nothing */ }
}

namespace AIEnglishCoachWithAgent.Agent
{
    /// <summary>
    /// An advanced, reusable audio recording service.
    /// - Optionally provides a ChannelReader for real-time consumption.
    /// - Provides the full recording as a file or a memory stream.
    /// - Handles race conditions gracefully on stop.
    /// </summary>
    public class NAudioAudioRecorderService : IDisposable
    {
        // --- Private Fields ---
        private WaveInEvent? _waveIn;
        private MemoryStream? _recordedAudioStream;
        private WaveFileWriter? _waveWriter;
        private Channel<Stream>? _realtimeChannel;
        private readonly bool _enableRealtimeChannel;
        private volatile bool _isStopping = false;
        private TaskCompletionSource? _recordingStoppedTcs;

        // --- Public Properties ---
        public bool IsRecording { get; private set; } = false;
        public WaveFormat RecordingFormat { get; } = new WaveFormat(16000, 16, 1);

        /// <summary>
        /// Initializes a new instance of the AudioRecorderService.
        /// </summary>
        /// <param name="enableRealtimeChannel">If true, a channel for real-time audio segments will be created upon starting.</param>
        public NAudioAudioRecorderService(bool enableRealtimeChannel = false)
        {
            _enableRealtimeChannel = enableRealtimeChannel;
        }

        /// <summary>
        /// Starts the audio recording.
        /// </summary>
        /// <returns>A ChannelReader for real-time consumption if enabled, otherwise null.</returns>
        public ChannelReader<Stream>? StartRecording()
        {
            if (IsRecording)
            {
                throw new InvalidOperationException("Recording is already in progress.");
            }

            _isStopping = false;
            _recordingStoppedTcs = new TaskCompletionSource();
            _recordedAudioStream = new MemoryStream();
            _waveIn = new WaveInEvent { WaveFormat = this.RecordingFormat };
            // The WaveFileWriter should be created here and disposed in OnRecordingStopped.
            _waveWriter = new WaveFileWriter(new IgnoreDisposeStream(_recordedAudioStream), _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            if (_enableRealtimeChannel)
            {
                var channelOptions = new BoundedChannelOptions(10)
                {
                    FullMode = BoundedChannelFullMode.Wait
                };
                _realtimeChannel = Channel.CreateBounded<Stream>(channelOptions);
            }

            _waveIn.StartRecording();
            IsRecording = true;

            return _realtimeChannel?.Reader;
        }

        /// <summary>
        /// Initiates the process of stopping the recording.
        /// </summary>
        /// <returns>A task that completes when the recording has fully stopped and all data is processed.</returns>
        public Task StopRecording()
        {
            if (!IsRecording || _isStopping) return Task.CompletedTask;

            _isStopping = true;
            _waveIn?.StopRecording();
            return _recordingStoppedTcs?.Task ?? Task.CompletedTask;
        }

        public Stream GetFullAudioStream()
        {
            if (IsRecording || _isStopping)
            {
                throw new InvalidOperationException("Cannot get the full audio stream while recording is active or stopping.");
            }
            if (_recordedAudioStream == null || _recordedAudioStream.Length == 0)
            {
                return new MemoryStream();
            }

            var fullStream = new MemoryStream();
            _recordedAudioStream.Position = 0;
            _recordedAudioStream.CopyTo(fullStream);
            fullStream.Position = 0;
            return fullStream;
        }

        public async Task SaveToFileAsync(string filePath)
        {
            using var fullStream = GetFullAudioStream();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await fullStream.CopyToAsync(fileStream);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_waveWriter == null || e.BytesRecorded == 0) return;

            _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            _waveWriter.Flush();

            // Only write to channel if it's enabled and exists
            if (_enableRealtimeChannel && _realtimeChannel != null)
            {
                var segmentStream = CreateWavStreamFromRaw(e.Buffer, e.BytesRecorded);
                _realtimeChannel.Writer.TryWrite(segmentStream);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            IsRecording = false;
            _isStopping = false;

            _realtimeChannel?.Writer.Complete();

            // Signal that recording has completely stopped.
            _recordingStoppedTcs?.TrySetResult();

            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
                _waveIn = null;
            }

            _waveWriter?.Dispose();
            _waveWriter = null;

            // Now that the writer is disposed, we can safely use the underlying stream.
            // The stream will be disposed when the service itself is disposed.

            if (e.Exception != null)
            {
                Console.WriteLine($"Recording stopped with an error: {e.Exception.Message}");
            }
        }

        private Stream CreateWavStreamFromRaw(byte[] rawData, int count)
        {
            var stream = new MemoryStream();
            // Wrap the stream to prevent the writer from closing it upon disposal.
            using (var writer = new WaveFileWriter(new IgnoreDisposeStream(stream), this.RecordingFormat))
            {
                writer.Write(rawData, 0, count);
            }
            stream.Position = 0;
            return stream;
        }

        public void Dispose()
        {
            _ = StopRecording(); // Fire and forget in Dispose
            _waveIn?.Dispose();
            _waveWriter?.Dispose();
            _recordedAudioStream?.Dispose();
            _realtimeChannel?.Writer.TryComplete();
        }
    }
}