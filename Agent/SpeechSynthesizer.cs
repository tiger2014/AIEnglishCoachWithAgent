using System.Diagnostics;
using Edge_tts_sharp;
using Edge_tts_sharp.Model;

namespace AIEnglishCoachWithAgent.Agent
{
    public class SpeechSynthesizer
    {
        eVoice _voice;

        public SpeechSynthesizer(string voiceName = "en-US-GuyNeural")
        {
            foreach (var item in Edge_tts.GetVoice().Where(s=>s.Locale =="en-US"))
            {
                Debug.WriteLine($"{item.ShortName}, {item.Gender},{item.VoiceTag}, {item.Locale}");
            }

            // 获取对应语音
            _voice = Edge_tts.GetVoice().FirstOrDefault(v => v.ShortName.Contains(voiceName))
                     ?? throw new Exception($"Voice {voiceName} not found.");
        }

        public async Task SpeakAsync(string text)
        {
            // Guard against empty or whitespace input, which can cause errors.
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var option = new PlayOption
            {
                Text = text,
                Rate = 0,      // 语速，范围：-100到100
                Volume = 1.0f,  // 音量，范围：0到1
                SavePath = null // 或者指定保存路径
            };

            // 获取中文语音（例如：晓晓）
            //var voice = Edge_tts.GetVoice().FirstOrDefault(v => v.Name.Contains("Xiaoxiao"));

            //// 获取第一个可用的语音
            //var voice = Edge_tts.GetVoice().First();

            // 播放文本
            Edge_tts.PlayText(option, _voice);
        }
    }
}
