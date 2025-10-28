using Microsoft.VisualBasic;
using NAudio.Wave;
using System.Diagnostics;
using System.Text;
using Whisper.net; // 按你实际 NuGet 包名调整
using Whisper.net.Ggml;

namespace AIEnglishCoachWithAgent.Agent
{
    public class SpeechRecognizer
    {
        private static WhisperProcessor _processor;
        private string _modelPath;

        private SpeechRecognizer() { }
        public static async Task<SpeechRecognizer> CreateAsync(string modelName = "ggml-small.en.bin")
        {
            modelName = @"C:\Develope\workspace\AIEnglishCoachWithAgent\models\"+modelName;
            // model link
            // https://huggingface.co/sandrohanea/whisper.net/tree/main/classic
            if (!File.Exists(modelName))
            {
                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.SmallEn);
                using var fileWriter = File.OpenWrite(modelName);
                await modelStream.CopyToAsync(fileWriter);
            }
            var factory = WhisperFactory.FromPath(modelName);
            _processor = factory.CreateBuilder()
                .WithLanguage("en")
                .Build();
            return new SpeechRecognizer();
        }

        public async Task<string> TranscribeAsync(Stream audioStream)
        {
            var sb = new StringBuilder();

            await foreach (var segment in _processor.ProcessAsync(audioStream))
            {
                Debug.WriteLine($"[{segment.Start}->{segment.End}] {segment.Text}");
                sb.Append(segment.Text);
            }

            return sb.ToString().Trim();
        }
    }
}
