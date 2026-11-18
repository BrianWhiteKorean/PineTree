using System.Drawing;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControls
{
    public class UcPlaceholderPage : UserControl
    {
        private readonly string _title;
        private readonly bool _showNotice;

        public UcPlaceholderPage(string title, bool showNotice = true)
        {
            _title = title;
            _showNotice = showNotice;
            Dock = DockStyle.Fill;
            BackColor = UiTheme.AppBackground;
            BuildLayout();
        }

        private void BuildLayout()
        {
            Label titleLabel = new Label();
            titleLabel.Text = _title;
            titleLabel.ForeColor = UiTheme.TextPrimary;
            titleLabel.Font = new Font("맑은 고딕", 18F, FontStyle.Bold, GraphicsUnit.Point, 129);
            titleLabel.Location = new Point(36, 34);
            titleLabel.Size = new Size(300, 42);
            Controls.Add(titleLabel);

            if (!_showNotice)
            {
                return;
            }

            Panel panel = new Panel();
            panel.BackColor = UiTheme.PanelBackground;
            panel.Location = new Point(36, 98);
            panel.Size = new Size(760, 180);
            panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(panel);

            Label message = new Label();
            message.Text = "화면 골격이 준비되었습니다. DB 연결과 세부 UI는 요청 주시면 이어서 구현하겠습니다.";
            message.ForeColor = UiTheme.TextSecondary;
            message.Font = new Font("맑은 고딕", 10F, FontStyle.Regular, GraphicsUnit.Point, 129);
            message.Location = new Point(28, 32);
            message.Size = new Size(680, 80);
            panel.Controls.Add(message);
        }
    }
}
