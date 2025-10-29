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
        private Panel messageContainer;
        private Panel messagePanel;

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
            AddSystemMessage("Initializing, please wait...");

            _recorder = new MicrophoneRecorder();
            _whisper = await SpeechRecognizer.CreateAsync("ggml-small.en.bin");
            _speaker = new SpeechSynthesizer();

            string instructions = "Your name is Stone. You are an English conversation practice partner. Your responses must use vocabulary and sentence structures appropriate or below for the CET-4 (College English Test Band 4) level. Your goal is to conduct engaging daily conversations. IMPORTANT: For TTS compatibility, your output must only contain standard words and common punctuation marks like periods, commas, question marks, and exclamation points. Do not use any special characters, emojis, parentheses, quotation marks, or symbols. My nmae is David";
            _ollamaAgent = new OllamaAgent("gemma3:4b", instructions);
            _thread = _ollamaAgent.CreateNewChatThread();

            AddSystemMessage("Ready. Please start recording.");
            this.btnStart.Enabled = true;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStop.Enabled = true;
            _recorder.StartRecording();
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            var stream = _recorder.StopRecording();
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            AddSystemMessage("Processing, please wait...");

            try
            {
                string text = await _whisper.TranscribeAsync(stream);
                AddMessage("David", text, MessageBubble.MessageType.User);

                var response = await _ollamaAgent._agent.RunAsync(text, _thread);
                AddMessage("Stone", response.Text, MessageBubble.MessageType.AI);

                await _speaker.SpeakAsync(response.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddSystemMessage($"ERROR: {ex.Message}");
            }
            finally
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }
        }

        private void AddMessage(string sender, string message, MessageBubble.MessageType type)
        {
            var bubble = new MessageBubble(sender, message, type);
            messagePanel.Controls.Add(bubble);
            bubble.BringToFront();

            // 滚动到底部
            messageContainer.PerformLayout();
            messageContainer.ScrollControlIntoView(bubble);
        }

        private void AddSystemMessage(string message)
        {
            var bubble = new MessageBubble("System", message, MessageBubble.MessageType.System);
            messagePanel.Controls.Add(bubble);
            bubble.BringToFront();

            // 滚动到底部
            messageContainer.PerformLayout();
            messageContainer.ScrollControlIntoView(bubble);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _whisper?.Dispose();
        }

        private void InitializeComponent()
        {
            btnStart = new Button();
            btnStop = new Button();
            messageContainer = new Panel();
            messagePanel = new Panel();
            messageContainer.SuspendLayout();
            SuspendLayout();
            // 
            // btnStart
            // 
            btnStart.Location = new Point(20, 20);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(100, 40);
            btnStart.TabIndex = 0;
            btnStart.Text = "录音";
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Enabled = false;
            btnStop.Location = new Point(140, 20);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(100, 40);
            btnStop.TabIndex = 1;
            btnStop.Text = "停止";
            btnStop.Click += btnStop_Click;
            // 
            // messageContainer
            // 
            messageContainer.AutoScroll = true;
            messageContainer.BackColor = Color.White;
            messageContainer.BorderStyle = BorderStyle.FixedSingle;
            messageContainer.Controls.Add(messagePanel);
            messageContainer.Location = new Point(20, 80);
            messageContainer.Name = "messageContainer";
            messageContainer.Size = new Size(360, 500);
            messageContainer.TabIndex = 2;
            // 
            // messagePanel
            // 
            messagePanel.AutoSize = true;
            messagePanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            messagePanel.BackColor = Color.White;
            messagePanel.Dock = DockStyle.Top;
            messagePanel.Location = new Point(0, 0);
            messagePanel.Name = "messagePanel";
            messagePanel.Padding = new Padding(0, 10, 0, 10);
            messagePanel.Size = new Size(358, 20);
            messagePanel.TabIndex = 0;
            // 
            // Form1
            // 
            BackColor = Color.FromArgb(240, 240, 240);
            ClientSize = new Size(400, 602);
            Controls.Add(btnStart);
            Controls.Add(btnStop);
            Controls.Add(messageContainer);
            Name = "Form1";
            Text = "AI Coach";
            messageContainer.ResumeLayout(false);
            messageContainer.PerformLayout();
            ResumeLayout(false);
        }
    }
}