using Microsoft.VisualBasic;
using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using System.Text;
using Whisper.net; // 按你实际 NuGet 包名调整
using Whisper.net.Ggml;

namespace AIEnglishCoachWithAgent.Agent
{
    public class SpeechRecognizer : IDisposable
    {
        private WhisperProcessor _processor;

        private SpeechRecognizer() { }
        public static async Task<SpeechRecognizer> CreateAsync(string modelName = "ggml-small.en.bin")
        {
            var recognizer = new SpeechRecognizer();
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir);
            var modelPath = Path.Combine(modelDir, modelName);

            // model link
            // https://huggingface.co/sandrohanea/whisper.net/tree/main/classic
            if (!File.Exists(modelPath))
            {
                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.SmallEn);
                using var fileWriter = File.OpenWrite(modelPath);
                await modelStream.CopyToAsync(fileWriter);
            }
            var factory = WhisperFactory.FromPath(modelPath);
            recognizer._processor = factory.CreateBuilder()
                .WithLanguage("en")
                .Build();
            return recognizer;
        }

        public async Task<string> TranscribeAsync(Stream audioStream)
        {
            var sb = new StringBuilder();

            await foreach (var segment in _processor.ProcessAsync(audioStream, CancellationToken.None))
            {
                Debug.WriteLine($"[{segment.Start}->{segment.End}] {segment.Text}");
                sb.Append(segment.Text);
            }

            return sb.ToString().Trim();
        }

        public void Dispose()
        {
            _processor?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
