using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace AIEnglishCoachWithAgent.Agent
{
    /// <summary>
    /// Creates a pair of connected streams. Data written to the Writer stream can be read from the Reader stream.
    /// This is useful for scenarios where one component produces data and another consumes it, like real-time audio processing.
    /// </summary>
    public class PipedStream
    {
        private readonly Stream _reader;
        private readonly Stream _writer;

        public PipedStream()
        {
            var pipe = new Pipe();
            _reader = new PipeReader(pipe);
            _writer = new PipeWriter(pipe);
        }

        public Stream Reader => _reader;
        public Stream Writer => _writer;

        private class Pipe
        {
            public readonly BlockingCollection<byte[]> Buffer = new BlockingCollection<byte[]>();
        }

        private class PipeReader : Stream
        {
            private readonly Pipe _pipe;
            private MemoryStream _currentChunk;

            public PipeReader(Pipe pipe) { _pipe = pipe; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                while (_currentChunk == null || _currentChunk.Position >= _currentChunk.Length)
                {
                    if (!_pipe.Buffer.TryTake(out var chunk, Timeout.Infinite)) return 0; // No more data
                    _currentChunk = new MemoryStream(chunk);
                }

                return _currentChunk.Read(buffer, offset, count);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private class PipeWriter : Stream
        {
            private readonly Pipe _pipe;
            public PipeWriter(Pipe pipe) { _pipe = pipe; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                var chunk = new byte[count];
                Array.Copy(buffer, offset, chunk, 0, count);
                _pipe.Buffer.Add(chunk);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { _pipe.Buffer.CompleteAdding(); }
                base.Dispose(disposing);
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
