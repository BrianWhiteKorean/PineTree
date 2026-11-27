using System.Drawing;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DialogProgressbar : Form
    {
        public DialogProgressbar()
        {
            InitializeComponent();
            label1.Visible = false;
            ClientSize = new Size(420, 42);
            Padding = new Padding(12);
            progressBar1.Dock = DockStyle.Fill;
            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.MarqueeAnimationSpeed = 30;
            ShowInTaskbar = false;
            ControlBox = false;
            UseWaitCursor = true;
        }

        protected override void OnShown(System.EventArgs e)
        {
            base.OnShown(e);
            if (Owner != null) CenterToParent();
        }

        public void ReportProgress(string message, int? percent = null)
        {
            SuspendLayout();
            label1.AutoSize = false;
            label1.Dock = DockStyle.Top;
            label1.Height = 28;
            label1.TextAlign = ContentAlignment.MiddleLeft;
            label1.AutoEllipsis = true;
            label1.Text = message;
            label1.Visible = true;
            ClientSize = new Size(420, 78);
            progressBar1.Dock = DockStyle.Bottom;
            progressBar1.Height = 18;
            progressBar1.Style = percent.HasValue ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            if (percent.HasValue) progressBar1.Value = System.Math.Max(0, System.Math.Min(100, percent.Value));
            ResumeLayout();
        }
    }
}
