using System;
using System.IO;

namespace AIEnglishCoachWithAgent.Agent
{
    /// <summary>
    /// 将音频数据同时路由到两个目标：完整缓冲（Whisper）和实时流（Vosk）
    /// </summary>
    public class DualStreamRouter : IDisposable
    {
        private readonly MemoryStream _completeBuffer;
        private readonly Stream _liveStream;
        private bool _disposed;

        public DualStreamRouter(MemoryStream completeBuffer, Stream liveStream)
        {
            _completeBuffer = completeBuffer ?? throw new ArgumentNullException(nameof(completeBuffer));
            _liveStream = liveStream ?? throw new ArgumentNullException(nameof(liveStream));
        }

        /// <summary>
        /// 同时写入两个流
        /// </summary>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DualStreamRouter));

            _completeBuffer.Write(buffer, offset, count);
            _liveStream.Write(buffer, offset, count);
        }

        public void Dispose()
        {
            if (_disposed) return;

            // 注意：不要 Dispose _completeBuffer，因为外部还要用
            // 只关闭 _liveStream 的写入端
            if (_liveStream is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _disposed = true;
        }
    }
}