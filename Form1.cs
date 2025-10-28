using AIEnglishCoachWithAgent.Agent;
using Microsoft.Agents.AI;
using System.Threading;

namespace AIEnglishCoachWithAgent
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.TextBox txtResult;

        private MicrophoneRecorder _recorder;
        private SpeechRecognizer _whisper;
        private SpeechSynthesizer _speaker;

        private AgentThread? _thread;
        OllamaAgent _ollamaAgent;
        public Form1()
        {
            InitializeComponent();
            _recorder = new MicrophoneRecorder();
            _whisper = SpeechRecognizer.CreateAsync("ggml-small.en.bin").Result;
            _speaker = new SpeechSynthesizer();
            _ollamaAgent = new OllamaAgent("gemma3:4b");
            _thread = _ollamaAgent.CreateNewChatThread();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            _recorder.StartRecording();
            //txtResult.Text = "录音";
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            var stream = _recorder.StopRecording();
            //txtResult.Text = ".....";

            string text = await _whisper.TranscribeAsync(stream);

            txtResult.Text += text + Environment.NewLine;

            var response = await _ollamaAgent._agent.RunAsync(text, _thread);

            txtResult.Text += response.Text + Environment.NewLine;

            await _speaker.SpeakAsync(response.Text);
        }
        private void InitializeComponent()
        {
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.txtResult = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(20, 20);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(100, 40);
            this.btnStart.Text = "录音";
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(140, 20);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(100, 40);
            this.btnStop.Text = "停止";
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // txtResult
            // 
            this.txtResult.Location = new System.Drawing.Point(20, 80);
            this.txtResult.Multiline = true;
            this.txtResult.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtResult.Size = new System.Drawing.Size(360, 200);
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(400, 300);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.txtResult);
            this.Text = "AI Coach";
            this.ResumeLayout(false);
            this.PerformLayout();
        }


    }
}
