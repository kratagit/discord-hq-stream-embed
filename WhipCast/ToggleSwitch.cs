#pragma warning disable WFO1000
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WhipCast
{
    public class ToggleSwitch : Control
    {
        private bool _checked = false;
        public bool Checked
        {
            get => _checked;
            set { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        public event EventHandler? CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Size = new Size(50, 24);
            this.Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(this.Parent.BackColor);

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            int cornerRadius = this.Height - 1;

            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, cornerRadius, cornerRadius, 180, 90);
            path.AddArc(rect.Right - cornerRadius, rect.Y, cornerRadius, cornerRadius, 270, 90);
            path.AddArc(rect.Right - cornerRadius, rect.Bottom - cornerRadius, cornerRadius, cornerRadius, 0, 90);
            path.AddArc(rect.X, rect.Bottom - cornerRadius, cornerRadius, cornerRadius, 90, 90);
            path.CloseFigure();

            if (Checked)
                e.Graphics.FillPath(new SolidBrush(Color.FromArgb(0, 120, 215)), path);
            else
                e.Graphics.FillPath(new SolidBrush(Color.LightGray), path);

            int circleSize = this.Height - 6;
            int circleX = Checked ? this.Width - circleSize - 3 : 3;
            int circleY = 3;

            e.Graphics.FillEllipse(Brushes.White, circleX, circleY, circleSize, circleSize);
        }
    }
}
