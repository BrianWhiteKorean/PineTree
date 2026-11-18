using System.Drawing;
using System.Windows.Forms;
using C1.Win.C1Input;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public sealed class UcOrderSourcePreview : UserControl
    {
        private readonly OrderSourcePreview _preview;
        public UcOrderSourcePreview()
        {
            Dock = DockStyle.Fill;
            var picture = new PictureBox { Dock = DockStyle.Fill };
            var canvas = new Panel { Dock = DockStyle.Top, Height = 50 };
            var info = new C1TextBox { Dock = DockStyle.Fill, Multiline = true, TextDetached = true };
            var infoPanel = new Panel { Dock = DockStyle.Top, Height = 67 };
            infoPanel.Controls.Add(info);
            infoPanel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 22, Text = "원본파일 정보", TextAlign = ContentAlignment.MiddleLeft });
            Controls.Add(picture); Controls.Add(canvas); Controls.Add(infoPanel);
            _preview = new OrderSourcePreview(this, picture, canvas, info);
        }
        public void Bind(OrderSourceDocument document) { _preview.Bind(document); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && _preview != null) _preview.Dispose();
            base.Dispose(disposing);
        }
    }
}
