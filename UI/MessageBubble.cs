using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace AIEnglishCoachWithAgent
{
    public class MessageBubble : Panel
    {
        private Label lblMessage;
        private string _sender;
        private string _message;
        private Timer fadeTimer;
        private int fadeStep = 0;
        private const int FADE_STEPS = 10;

        public MessageType Type { get; private set; } // Make sure this property is public

        public enum MessageType
        {
            User,      // David
            AI,        // Stone
            System     // 系统消息
        }

        public MessageBubble(string sender, string message, MessageType type)
        {
            _sender = sender;
            _message = message;

            this.Type = type;

            this.Padding = new Padding(10);
            this.Margin = new Padding(10, 5, 10, 5);
            this.DoubleBuffered = true;

            lblMessage = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(300, 0),
                Text = message,
                Font = new Font("Microsoft YaHei", 11),
                Padding = new Padding(12, 8, 12, 8)
            };

            // 根据类型设置样式
            switch (type)
            {
                case MessageType.User:
                    // David - 蓝色气泡，右对齐
                    lblMessage.BackColor = Color.FromArgb(0, 132, 255);
                    lblMessage.ForeColor = Color.White;
                    this.Dock = DockStyle.Top;
                    lblMessage.Dock = DockStyle.Right;
                    break;

                case MessageType.AI:
                    // Stone - 灰色气泡，左对齐
                    lblMessage.BackColor = Color.FromArgb(229, 229, 234);
                    lblMessage.ForeColor = Color.Black;
                    this.Dock = DockStyle.Top;
                    lblMessage.Dock = DockStyle.Left;
                    break;

                case MessageType.System:
                    // 系统消息 - 居中显示
                    lblMessage.BackColor = Color.FromArgb(245, 245, 245);
                    lblMessage.ForeColor = Color.Gray;
                    lblMessage.Font = new Font("Microsoft YaHei", 9);
                    this.Dock = DockStyle.Top;
                    lblMessage.Dock = DockStyle.Fill;
                    lblMessage.TextAlign = ContentAlignment.MiddleCenter;
                    break;
            }

            this.Controls.Add(lblMessage);
            this.BackColor = Color.Transparent;

            // 设置圆角
            lblMessage.Paint += LblMessage_Paint;

            // 计算高度
            this.Height = lblMessage.PreferredHeight + 20;

            // 初始设置为透明
            this.Opacity = 0;

            // 启动淡入动画
            StartFadeIn();
        }

        private void StartFadeIn()
        {
            fadeTimer = new Timer();
            fadeTimer.Interval = 20; // 20ms 每帧
            fadeTimer.Tick += FadeTimer_Tick;
            fadeTimer.Start();
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            fadeStep++;
            double opacity = (double)fadeStep / FADE_STEPS;

            // 设置透明度
            this.Opacity = opacity;

            if (fadeStep >= FADE_STEPS)
            {
                fadeTimer.Stop();
                fadeTimer.Dispose();
                this.Opacity = 1.0;
            }
        }

        // 新增 Opacity 属性支持
        private double _opacity = 1.0;

        public double Opacity
        {
            get { return _opacity; }
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;
                _opacity = value;

                // 更新控件的透明度
                UpdateOpacity();
            }
        }

        private void UpdateOpacity()
        {
            if (lblMessage != null)
            {
                Color backColor = lblMessage.BackColor;
                Color foreColor = lblMessage.ForeColor;

                lblMessage.BackColor = Color.FromArgb(
                    (int)(255 * _opacity),
                    backColor.R,
                    backColor.G,
                    backColor.B
                );

                lblMessage.ForeColor = Color.FromArgb(
                    (int)(255 * _opacity),
                    foreColor.R,
                    foreColor.G,
                    foreColor.B
                );

                lblMessage.Invalidate();
            }
        }

        private void LblMessage_Paint(object sender, PaintEventArgs e)
        {
            Label lbl = sender as Label;
            if (lbl == null) return;

            // 绘制圆角背景
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = GetRoundedRectangle(lbl.ClientRectangle, 18))
            {
                using (SolidBrush brush = new SolidBrush(lbl.BackColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            // 绘制文本
            TextRenderer.DrawText(e.Graphics, lbl.Text, lbl.Font, lbl.ClientRectangle, lbl.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (fadeTimer != null)
                {
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}