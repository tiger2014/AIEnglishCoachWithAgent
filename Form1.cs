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

        // 平滑滚动相关
        private System.Windows.Forms.Timer scrollTimer;
        private int targetScrollPosition;
        private int scrollStep;

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
            messageContainer.SuspendLayout();

            var bubble = new MessageBubble(sender, message, type);
            messagePanel.Controls.Add(bubble);
            bubble.BringToFront();

            messageContainer.ResumeLayout();

            // 平滑滚动到底部
            SmoothScrollToBottom();
        }

        private void AddSystemMessage(string message)
        {
            messageContainer.SuspendLayout();

            var bubble = new MessageBubble("System", message, MessageBubble.MessageType.System);
            messagePanel.Controls.Add(bubble);
            bubble.BringToFront();

            messageContainer.ResumeLayout();

            // 平滑滚动到底部
            SmoothScrollToBottom();
        }

        private void SmoothScrollToBottom()
        {
            // 如果已经有滚动动画在进行，停止它
            if (scrollTimer != null && scrollTimer.Enabled)
            {
                scrollTimer.Stop();
            }

            // 计算目标位置
            messageContainer.PerformLayout();
            int maxScroll = messageContainer.VerticalScroll.Maximum - messageContainer.VerticalScroll.LargeChange + 1;
            targetScrollPosition = Math.Max(0, maxScroll);

            int currentPosition = messageContainer.VerticalScroll.Value;
            int distance = targetScrollPosition - currentPosition;

            // 如果距离很小，直接跳转
            if (Math.Abs(distance) < 10)
            {
                messageContainer.AutoScrollPosition = new Point(0, targetScrollPosition);
                return;
            }

            // 计算滚动步长
            scrollStep = distance / 15; // 15帧完成滚动
            if (scrollStep == 0) scrollStep = distance > 0 ? 1 : -1;

            // 创建定时器
            scrollTimer = new System.Windows.Forms.Timer();
            scrollTimer.Interval = 16; // 约60fps
            scrollTimer.Tick += ScrollTimer_Tick;
            scrollTimer.Start();
        }

        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            int currentPosition = messageContainer.VerticalScroll.Value;
            int newPosition = currentPosition + scrollStep;

            // 检查是否到达目标
            bool reachedTarget = false;
            if (scrollStep > 0 && newPosition >= targetScrollPosition)
            {
                newPosition = targetScrollPosition;
                reachedTarget = true;
            }
            else if (scrollStep < 0 && newPosition <= targetScrollPosition)
            {
                newPosition = targetScrollPosition;
                reachedTarget = true;
            }

            // 设置新位置
            messageContainer.AutoScrollPosition = new Point(0, Math.Abs(newPosition));

            // 如果到达目标，停止定时器
            if (reachedTarget)
            {
                scrollTimer.Stop();
                scrollTimer.Dispose();
                scrollTimer = null;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (scrollTimer != null)
            {
                scrollTimer.Stop();
                scrollTimer.Dispose();
            }
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
            btnStart.BackColor = Color.FromArgb(0, 132, 255);
            btnStart.Cursor = Cursors.Hand;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.Font = new Font("Microsoft YaHei", 10F);
            btnStart.ForeColor = Color.White;
            btnStart.Location = new Point(20, 20);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(100, 40);
            btnStart.TabIndex = 0;
            btnStart.Text = "录音";
            btnStart.UseVisualStyleBackColor = false;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.BackColor = Color.FromArgb(220, 53, 69);
            btnStop.Cursor = Cursors.Hand;
            btnStop.Enabled = false;
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.FlatStyle = FlatStyle.Flat;
            btnStop.Font = new Font("Microsoft YaHei", 10F);
            btnStop.ForeColor = Color.White;
            btnStop.Location = new Point(140, 20);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(100, 40);
            btnStop.TabIndex = 1;
            btnStop.Text = "停止";
            btnStop.UseVisualStyleBackColor = false;
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
            messageContainer.Size = new Size(360, 539);
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
            ClientSize = new Size(400, 644);
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