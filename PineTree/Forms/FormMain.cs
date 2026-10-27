using System;
using System.ComponentModel;
using System.Windows.Forms;
using PineTree.UserControls;
using PineTree.Dialogs;

namespace PineTree.Forms
{
    public partial class FormMain : Form
    {
        private static FormMain _instance;

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

            UcContainer container = new UcContainer();
            container.Dock = DockStyle.Fill;
            panel1.Controls.Add(container);
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
