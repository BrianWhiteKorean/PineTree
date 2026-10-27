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
    }
}
