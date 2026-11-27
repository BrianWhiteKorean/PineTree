using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using PineTree.UserControls;
using PineTree.Dialogs;
using PineTree.Classes;

namespace PineTree.Forms
{
    public partial class FormMain : Form
    {
        private static FormMain _instance;
        private UcContainer _managementContainer;
        private UcDesignModeContainer _designModeContainer;
        private Panel _modeSwitchPanel;
        private Button _modeButton;
        private bool _isManagementMode = true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;


                return cp;
            }
        }

        public static FormMain Instance
        {
            get
            {
                if (_instance == null || _instance.IsDisposed)
                {
                    _instance = new FormMain();
                }

                return _instance;
            }
        }

        public FormMain()
        {
            if (_instance != null && !_instance.IsDisposed && LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                throw new InvalidOperationException("FormMain is a singleton. Use FormMain.Instance.");
            }

            _instance = this;

            InitializeComponent();

            _managementContainer = new UcContainer();
            _managementContainer.Dock = DockStyle.Fill;
            panel1.Controls.Add(_managementContainer);
            InitializeModeControls();
        }

        private void InitializeModeControls()
        {
            _modeSwitchPanel = new Panel
            {
                Name = "modeSwitchPanel",
                BackColor = UiTheme.TopBarBackground,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(panel1.ClientSize.Width - 115, 0),
                Size = new Size(115, UiTheme.TopBarHeight)
            };
            _modeButton = new Button
            {
                Name = "modeButton",
                Text = "관리모드",
                AccessibleName = "관리모드",
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.FromArgb(31, 47, 86),
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                Location = new Point(5, 15),
                Size = new Size(90, 28)
            };
            _modeButton.FlatAppearance.BorderSize = 0;
            _modeButton.Click += ModeButton_Click;
            _modeSwitchPanel.Controls.Add(_modeButton);
            panel1.Controls.Add(_modeSwitchPanel);
            _modeSwitchPanel.BringToFront();
        }

        private void ModeButton_Click(object sender, EventArgs e)
        {
            try
            {
                SetManagementMode(!_isManagementMode);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "모드 전환 중 오류가 발생했습니다.\r\n" + ex.Message,
                    "화면 전환", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetManagementMode(bool managementMode)
        {
            if (!managementMode && _designModeContainer == null)
            {
                _designModeContainer = new UcDesignModeContainer();
                _designModeContainer.Visible = false;
                panel1.Controls.Add(_designModeContainer);
            }

            panel1.SuspendLayout();
            try
            {
                _managementContainer.Visible = managementMode;
                if (_designModeContainer != null) _designModeContainer.Visible = !managementMode;
                Control activeContainer = managementMode ? (Control)_managementContainer : _designModeContainer;
                activeContainer.BringToFront();
                _modeSwitchPanel.BringToFront();
                _isManagementMode = managementMode;
                _modeButton.Text = managementMode ? "관리모드" : "디자인모드";
                _modeButton.AccessibleName = _modeButton.Text;
            }
            finally
            {
                panel1.ResumeLayout(true);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Update();

            using (LoginDlg login = new LoginDlg(this))
            {
                if (login.ShowDialog(this) != DialogResult.OK)
                {
                    Close();
                    return;
                }
            }

            foreach (Control control in panel1.Controls)
            {
                UcContainer container = control as UcContainer;
                if (container != null)
                {
                    container.RefreshUserInfo();
                }
            }
        }
    }
}
