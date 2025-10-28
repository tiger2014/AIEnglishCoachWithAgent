using AIEnglishCoachWithAgent.Agent;
using Microsoft.Agents.AI;
using System.IO;
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
            this.FormClosing += Form1_FormClosing;
            this.Load += new System.EventHandler(Form1_Load);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            this.btnStart.Enabled = false;
            this.btnStop.Enabled = false;
            this.txtResult.Text = "Initializing, please wait...\r\n";
            this.txtResult.Font = new Font("Microsoft YaHei", 14);

            _recorder = new MicrophoneRecorder();
            _whisper = await SpeechRecognizer.CreateAsync("ggml-small.en.bin");
            _speaker = new SpeechSynthesizer();
            //string instructions = "Your name is Stone. You are a spoken English practice partner. Please use only very simple vocabulary and short sentences. Your goal is to help the user have daily conversations in an easy-to-understand way. Please keep the conversation simple and direct. My name is David";

            string instructions = "Your name is Stone. You are an English conversation practice partner. Your responses must use vocabulary and sentence structures appropriate or below for the CET-4 (College English Test Band 4) level. Your goal is to conduct engaging daily conversations. IMPORTANT: For TTS compatibility, your output must only contain standard words and common punctuation marks like periods, commas, question marks, and exclamation points. Do not use any special characters, emojis, parentheses, quotation marks, or symbols. My nmae is David";
            _ollamaAgent = new OllamaAgent("gemma3:4b", instructions);
            _thread = _ollamaAgent.CreateNewChatThread();

            this.txtResult.Text = "Ready. Please start recording.\r\n";
            this.btnStart.Enabled = true;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStop.Enabled = true;
            _recorder.StartRecording();
            //txtResult.Text = "录音";
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            var stream = _recorder.StopRecording();
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            txtResult.AppendText("Processing, please wait...\r\n");

            try
            {
                string text = await _whisper.TranscribeAsync(stream);

                txtResult.AppendText($"David: {text}\r\n");

                var response = await _ollamaAgent._agent.RunAsync(text, _thread);

                txtResult.AppendText($"Stone: {response.Text}\r\n\r\n");

                await _speaker.SpeakAsync(response.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtResult.AppendText($"ERROR: {ex.Message}\r\n\r\n");
            }
            finally
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _whisper?.Dispose();
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
            this.btnStop.Enabled = false;
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
