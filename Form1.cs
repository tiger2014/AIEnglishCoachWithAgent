using AIEnglishCoachWithAgent.Agent;
using Microsoft.Agents.AI;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Text;
using System.Runtime.ExceptionServices;
using System.Threading;
using Timer = System.Windows.Forms.Timer;

namespace AIEnglishCoachWithAgent
{
    public partial class Form1 : Form, IDisposable
    {
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private Panel messageContainer;
        private ComboBox modelComboBox;
        private Panel messagePanel;

        private NAudioAudioRecorderService _recorder;
        private WhisperTranscriptionService _whisper;
        private SpeechSynthesizer _speaker;

        private AgentThread? _thread;
        OllamaAgent _ollamaAgent;

        // 平滑滚动相关
        private Timer scrollTimer;
        private int targetScrollPosition;
        private int scrollStep;

        // 键盘状态
        private bool isRecording = false;
        private bool isProcessing = false;

        // 实时转录相关
        private CancellationTokenSource? _recordingCts;
        private StringBuilder _confirmedTextBuilder = new StringBuilder();

        private Stopwatch _stopwatch;

        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
            this.Load += new System.EventHandler(Form1_Load);
            this.Shown += new System.EventHandler(Form1_Shown); // 新增 Shown 事件处理

            // 启用键盘事件
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetUiState(isInitializing: true);
            AddSystemMessage("Initializing, please wait...");
            _stopwatch = new Stopwatch();
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
            try
            {
                _recorder = new NAudioAudioRecorderService(enableRealtimeChannel: false);

                // 异步加载所有AI组件
                _whisper = new WhisperTranscriptionService("ggml-small.en.bin");
                await _whisper.InitializeAsync();
                //_vosk = await VoskRecognizer.CreateAsync();
                _speaker = new SpeechSynthesizer();

                InitializeAgentAndThread();
                // 订阅事件
                _ollamaAgent.OnRequestUrlInput += ShowUrlInputDialog;

                AddSystemMessage("Ready. Press and hold SPACE to record.");
                SetUiState(isRecording: false); // 初始化完成，启用UI
            }
            catch (Exception ex)
            {
                // 捕获任何初始化期间的致命错误，并友好地提示用户
                var errorMessage = $"Failed to initialize AI components: {ex.Message}\n\nThe application will now close.";
                MessageBox.Show(errorMessage, "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close(); // 关闭应用程序
            }
        }

        private void InitializeAgentAndThread()
        {
            string selectedModel = modelComboBox.SelectedItem.ToString();
            string instructions = "My name is David, Your name is Stone. You are an English conversation practice partner. Your responses must use vocabulary and sentence structures appropriate or below for the CET-4 (College English Test Band 4) level. Your goal is to conduct engaging daily conversations. IMPORTANT: For TTS compatibility, your output must only contain standard words and common punctuation marks like periods, commas, question marks, and exclamation points. Do not use any special characters, emojis, parentheses, quotation marks, or symbols; Don't use extra spaces in your sentence, for an example, it' s is not allowed.";
            _ollamaAgent = new OllamaAgent(selectedModel, instructions);
            _thread = _ollamaAgent.CreateNewChatThread();
        }
        
        private async Task<string> ShowUrlInputDialog()
        {
            // 方式1：使用自定义对话框
            using (var dialog = new UrlInputDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.urlTxt;
                }
            }

            // 方式2：使用简单的InputBox（需要自己实现或用第三方库）
            // string url = InputBox.Show("Please enter news URL:", "News URL");

            return string.Empty;
        }

        private async void Form1_KeyDown(object sender, KeyEventArgs e)
        {

            // 按住 Space 开始录音
            if (e.KeyCode == Keys.Space && !isRecording && !isProcessing && btnStart.Enabled)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                StartRecording();
            }
        }

        private async void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            // 松开 Space 停止录音
            if (e.KeyCode == Keys.Space && isRecording)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await StopRecording();
            }
        }

        private void StartRecording()
        {
            if (isRecording || isProcessing) return;

            SetUiState(isRecording: true);

            // 重置实时转录状态
            _confirmedTextBuilder.Clear();
            //AddMessage("David", "...", MessageBubble.MessageType.User); // 添加一个占位气泡

            // 启动录音并获取实时音频通道
            var audioChannelReader = _recorder.StartRecording();

            if (audioChannelReader != null)
            {
                _recordingCts = new CancellationTokenSource();
                // 启动一个后台任务来处理实时音频流
                _ = ProcessAudioChannelAsync(audioChannelReader, _recordingCts.Token);
            }

            // 视觉反馈
            this.BackColor = Color.FromArgb(255, 240, 240); // 淡红色表示正在录音
        }

        private async Task ProcessAudioChannelAsync(ChannelReader<Stream> reader, CancellationToken token)
        {
            try
            {
                await foreach (var audioStream in reader.ReadAllAsync(token))
                {
                    using (audioStream)
                    {
                        var segmentText = await _whisper.TranscribeAsync(audioStream);
                        if (!string.IsNullOrWhiteSpace(segmentText))
                        {
                            _confirmedTextBuilder.Append(segmentText);
                            UpdateUserMessage(_confirmedTextBuilder.ToString(), isPartial: true);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when the user stops recording.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime-Transcription-Error] {ex.Message}");
            }
        }

        private async Task StopRecording()
        {
            if (!isRecording) return;

            AddSystemMessage("Processing, please wait...");

            SetUiState(isFinalizing: true);

            // 停止后台处理任务并停止录音
            _recordingCts?.Cancel();
            // Await the recorder to fully stop to prevent race conditions.
            await _recorder.StopRecording(); // Now just one call to await the full stop process.

            try
            {
                _stopwatch.Restart();
                // 使用 GetFullAudioStream 进行最终的、最准确的转录
                using var fullAudioStream = _recorder.GetFullAudioStream();
                string text = await _whisper.TranscribeAsync(fullAudioStream);
                Debug.WriteLine($"Whisper {_stopwatch.ElapsedMilliseconds}");

                // 使用Whisper的最终结果更新UI
                //UpdateUserMessage(text, isPartial: false);
                AddMessage("David", text, MessageBubble.MessageType.User);

                // If the transcribed text is empty or just whitespace, don't proceed to call the AI.
                if (string.IsNullOrWhiteSpace(text))
                {
                    // Optionally remove the placeholder bubble if nothing was said.
                    var lastBubble = messagePanel.Controls.OfType<MessageBubble>().LastOrDefault(b => b.Dock == DockStyle.Right);
                    if (lastBubble != null && lastBubble.Controls.OfType<Label>().FirstOrDefault()?.Text == "...")
                    {
                        messagePanel.Controls.Remove(lastBubble);
                    }
                    // We still need to fall through to the finally block to reset the state.
                    // So we just return from the try block.
                    return;
                }

                _stopwatch.Restart();
                var response = await _ollamaAgent._agent.RunAsync(text, _thread);
                Debug.WriteLine($"LLM {_stopwatch.ElapsedMilliseconds}");

                // 将AI的回复添加到UI并用语音播放
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
                // This block ensures that the UI state is always reset,
                // regardless of how the try block exits.
                this.BackColor = Color.FromArgb(240, 240, 240);
                SetUiState(isRecording: false);
            }
        }

        private void UpdateUserMessage(string text, bool isPartial = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateUserMessage(text, isPartial)));
                return;
            }

            // Find the last user bubble and update it, or create a new one.
            // We query by MessageType instead of Dock property because FlowLayoutPanel overrides Dock.
            // This assumes MessageBubble exposes a public property 'Type' of type MessageBubble.MessageType.
            var lastBubble = messagePanel.Controls.OfType<MessageBubble>().LastOrDefault(b => b.Type == MessageBubble.MessageType.User);

            if (lastBubble != null)
            {
                // Find the Label within the MessageBubble and update its text.
                var textLabel = lastBubble.Controls.OfType<Label>().FirstOrDefault();
                if (textLabel != null)
                {
                    textLabel.Text = text;
                    textLabel.ForeColor = isPartial ? Color.Gray : Color.Black;
                    lastBubble.Invalidate(); // Force a repaint to resize correctly
                }
            }
            else
            {
                // This case should ideally not happen if we pre-add a bubble in StartRecording
                AddMessage("David", text, MessageBubble.MessageType.User);
            }
            SmoothScrollToBottom();
        }

        private void SetUiState(bool isInitializing = false, bool isRecording = false, bool isFinalizing = false)
        {
            this.isRecording = isRecording;
            this.isProcessing = isRecording || isFinalizing;

            bool enableControls = !isInitializing && !isProcessing;

            btnStart.Enabled = enableControls;
            btnStop.Enabled = isRecording;
            modelComboBox.Enabled = enableControls;
            if (isInitializing)
            {
                btnStart.Enabled = false;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            StartRecording();
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            await StopRecording();
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
            scrollTimer = new Timer();
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
            _recorder?.Dispose();
        }

        private void InitializeComponent()
        {
            btnStart = new Button();
            btnStop = new Button();
            modelComboBox = new ComboBox();
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
            // modelComboBox
            // 
            modelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modelComboBox.FormattingEnabled = true;
            modelComboBox.Items.AddRange(new object[] { "qwen3:4b", "qwen3:14b" });
            modelComboBox.SelectedIndex = 0;
            modelComboBox.Location = new Point(296, 33);
            modelComboBox.Name = "modelComboBox";
            modelComboBox.Size = new Size(81, 23);
            modelComboBox.TabIndex = 4;
            // 
            // messageContainer
            // 
            messageContainer.AutoScroll = true;
            messageContainer.BackColor = Color.White;
            messageContainer.BorderStyle = BorderStyle.FixedSingle;
            messageContainer.Controls.Add(messagePanel);
            messageContainer.Location = new Point(20, 80);
            messageContainer.Name = "messageContainer";
            messageContainer.Size = new Size(360, 525);
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
            ClientSize = new Size(400, 631);
            Controls.Add(btnStart);
            Controls.Add(btnStop);
            Controls.Add(modelComboBox);
            Controls.Add(messageContainer);
            Name = "Form1";
            Text = "AI Coach - Hold SPACE to record";
            messageContainer.ResumeLayout(false);
            messageContainer.PerformLayout();
            ResumeLayout(false);
        }
    }
}