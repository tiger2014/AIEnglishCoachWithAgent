using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace AIEnglishCoachWithAgent.Agent
{
    public class WhisperTranscriptionService : IDisposable
    {
        private WhisperProcessor? _processor;
        private readonly string _modelPath;

        public bool IsInitialized { get; private set; } = false;

        public WhisperTranscriptionService(string modelName = "ggml-small.en.bin")
        {
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            _modelPath = Path.Combine(modelDir, modelName);
        }

        public async Task InitializeAsync()
        {
            if (IsInitialized) return;

            if (!File.Exists(_modelPath))
            {
                var modelDir = Path.GetDirectoryName(_modelPath);
                if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir!);

                // Assuming GgmlType.SmallEn matches "ggml-small.en.bin"
                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.SmallEn);
                using var fileWriter = File.OpenWrite(_modelPath);
                await modelStream.CopyToAsync(fileWriter);
            }

            var factory = WhisperFactory.FromPath(_modelPath);
            _processor = factory.CreateBuilder()
                .WithLanguage("auto")       // 自动检测语言；en
                .WithPrintTimestamps(false) // 不打印时间戳
                .Build();

            IsInitialized = true;
        }

        public async Task<string> TranscribeAsync(Stream audioStream)
        {
            if (!IsInitialized || _processor == null)
                throw new InvalidOperationException("Service is not initialized. Call InitializeAsync() first.");

            var sb = new StringBuilder();
            await foreach (var segment in _processor.ProcessAsync(audioStream))
            {
                sb.Append(segment.Text);
            }
            return sb.ToString().Trim();
        }

        public void Dispose()
        {
            _processor?.Dispose();
        }
    }
}