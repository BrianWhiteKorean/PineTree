using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.UserControls;

namespace PineTree.Forms
{
    public partial class FormMain : Form
    {
        private static FormMain _instance;
        private readonly Dictionary<AppScreen, UserControl> _screenCache = new Dictionary<AppScreen, UserControl>();
        private AppScreen _currentScreen;

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
            Shown += FormMain_Shown;
            NavigateTo(AppScreen.Main);
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(WarmUpScreens));
        }

        private void WarmUpScreens()
        {
            GetOrCreateScreen(AppScreen.CustInfo);
            GetOrCreateScreen(AppScreen.PriceItems);
        }

        private void NavigateTo(AppScreen screen)
        {
            if (panel1.Controls.Count > 0 && _currentScreen == screen)
            {
                return;
            }

            ShowContent(screen, GetOrCreateScreen(screen));
        }

        private void ShowContent(AppScreen screen, UserControl userControl)
        {
            panel1.SuspendLayout();

            foreach (Control control in panel1.Controls)
            {
                control.Visible = false;
            }

            userControl.Visible = true;
            userControl.BringToFront();

            panel1.ResumeLayout(false);
            _currentScreen = screen;
        }

        private void NavigationView_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigateTo(e.Screen);
        }

        private UserControl GetOrCreateScreen(AppScreen screen)
        {
            UserControl userControl;
            if (_screenCache.TryGetValue(screen, out userControl))
            {
                return userControl;
            }

            switch (screen)
            {
                case AppScreen.CustInfo:
                    userControl = new UcCustInfo();
                    break;
                case AppScreen.PriceItems:
                    userControl = new UcPriceItems();
                    break;
                default:
                    userControl = new UcMain();
                    screen = AppScreen.Main;
                    break;
            }

            userControl.Dock = DockStyle.Fill;
            userControl.Visible = false;

            INavigationView navigationView = userControl as INavigationView;
            if (navigationView != null)
            {
                navigationView.NavigationRequested += NavigationView_NavigationRequested;
            }

            _screenCache.Add(screen, userControl);
            panel1.Controls.Add(userControl);

            return userControl;
        }
    }
}
