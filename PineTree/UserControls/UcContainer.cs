using System;
using System.Drawing;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControls
{
    public class UcContainer : UserControl
    {
        private readonly UcMenu _menu;
        private readonly UcTopBar _topBar;
        private readonly Panel _rightPanel;
        private readonly Panel _contentPanel;
        private readonly ScreenFactory _screenFactory = new ScreenFactory();
        private AppScreen _currentScreen;

        public UcContainer()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.AppBackground;

            _menu = new UcMenu();
            _menu.MenuSelected += Menu_MenuSelected;
            Controls.Add(_menu);

            _rightPanel = new Panel();
            _rightPanel.Dock = DockStyle.Fill;
            _rightPanel.BackColor = UiTheme.AppBackground;
            Controls.Add(_rightPanel);
            _rightPanel.BringToFront();

            _contentPanel = new Panel();
            _contentPanel.BackColor = UiTheme.AppBackground;
            _contentPanel.Padding = new Padding(0);
            _rightPanel.Controls.Add(_contentPanel);

            _topBar = new UcTopBar();
            _topBar.Dock = DockStyle.None;
            _topBar.SelectedDateChanged += TopBar_SelectedDateChanged;
            _rightPanel.Controls.Add(_topBar);
            _topBar.BringToFront();
            _rightPanel.Resize += RightPanel_Resize;
            LayoutRightPanel();

            NavigateTo(AppScreen.Main);
        }

        public void RefreshUserInfo()
        {
            _menu.RefreshUserInfo();
            _topBar.RefreshUserInfo();
        }

        private void RightPanel_Resize(object sender, EventArgs e)
        {
            LayoutRightPanel();
        }

        private void LayoutRightPanel()
        {
            _topBar.Location = new Point(0, 0);
            _topBar.Size = new Size(_rightPanel.Width, UiTheme.TopBarHeight);

            _contentPanel.Location = new Point(0, UiTheme.TopBarHeight);
            _contentPanel.Size = new Size(_rightPanel.Width, Math.Max(0, _rightPanel.Height - UiTheme.TopBarHeight));
        }

        public void NavigateTo(AppScreen screen)
        {
            if (_contentPanel.Controls.Count > 0 && _currentScreen == screen)
            {
                return;
            }

            UserControl control = _screenFactory.GetScreen(screen);
            if (!_contentPanel.Controls.Contains(control))
            {
                INavigationView navigationView = control as INavigationView;
                if (navigationView != null)
                {
                    navigationView.NavigationRequested += NavigationView_NavigationRequested;
                }

                control.Visible = false;
                _contentPanel.Controls.Add(control);
            }

            foreach (Control child in _contentPanel.Controls)
            {
                child.Visible = false;
            }

            control.Visible = true;
            control.BringToFront();
            _currentScreen = screen;
            _menu.SelectScreen(screen);
            _topBar.SetScreen(screen, GetTitle(screen));
            ApplySelectedDateToScreen(control);
        }

        private void Menu_MenuSelected(object sender, MenuSelectedEventArgs e)
        {
            NavigateTo(e.Item.Screen);
        }

        private void NavigationView_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigateTo(e.Screen);
        }

        private void TopBar_SelectedDateChanged(object sender, EventArgs e)
        {
            if (_currentScreen != AppScreen.Main)
            {
                return;
            }

            if (_contentPanel.Controls.Count == 0)
            {
                return;
            }

            foreach (Control child in _contentPanel.Controls)
            {
                if (child.Visible)
                {
                    ApplySelectedDateToScreen(child);
                    return;
                }
            }
        }

        private void ApplySelectedDateToScreen(Control control)
        {
            UcDashboardHome dashboard = control as UcDashboardHome;
            if (dashboard == null)
            {
                return;
            }

            dashboard.SetSelectedDate(_topBar.SelectedDate);
        }

        private string GetTitle(AppScreen screen)
        {
            switch (screen)
            {
                case AppScreen.Main:
                    return "메인 / 현황";
                case AppScreen.CustInfo:
                    return "고객정보";
                case AppScreen.PriceItems:
                    return "상품관리";
                case AppScreen.Order:
                    return "주문관리";
                case AppScreen.Print:
                    return "출력물관리";
                case AppScreen.Process:
                    return "공정관리";
                case AppScreen.Settlement:
                    return "정산관리";
                case AppScreen.Settings:
                    return "환경설정";
                default:
                    return "준비중";
            }
        }
    }
}
