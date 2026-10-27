using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Properties;

namespace PineTree.UserControls
{
    public class UcMenu : UserControl
    {
        private readonly List<MenuItemInfo> _items = new List<MenuItemInfo>();
        private readonly Dictionary<AppScreen, Panel> _itemPanels = new Dictionary<AppScreen, Panel>();
        private readonly Dictionary<AppScreen, PictureBox> _itemIcons = new Dictionary<AppScreen, PictureBox>();
        private AppScreen _selectedScreen = AppScreen.Main;
        private Label _userAvatar;
        private Label _userName;
        private Label _userRole;

        public UcMenu()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Width = UiTheme.MenuWidth;
            Dock = DockStyle.Left;
            BackColor = UiTheme.MenuBackground;

            InitializeItems();
            BuildLayout();
            RefreshUserInfo();
        }

        public event EventHandler<MenuSelectedEventArgs> MenuSelected;

        public void RefreshUserInfo()
        {
            _userAvatar.Text = Global.UserBadge;
            _userName.Text = string.IsNullOrWhiteSpace(Global.UserID)
                ? string.Empty
                : (Global.IsAdministrator ? "관리자" : "사용자");
            string companyName = Global.CompanyName ?? string.Empty;
            _userRole.Text = Global.IsAdministrator
                ? (string.IsNullOrWhiteSpace(companyName) ? "전체권한" : companyName + " · 전체권한")
                : companyName;
        }

        public void SelectScreen(AppScreen screen)
        {
            _selectedScreen = screen;

            foreach (KeyValuePair<AppScreen, Panel> item in _itemPanels)
            {
                item.Value.BackColor = item.Key == _selectedScreen
                    ? UiTheme.MenuSelectedBackground
                    : UiTheme.MenuBackground;

                PictureBox icon;
                if (_itemIcons.TryGetValue(item.Key, out icon))
                {
                    icon.Image = GetMenuImage(item.Key, item.Key == _selectedScreen);
                }
            }
        }

        private void InitializeItems()
        {
            _items.Add(new MenuItemInfo(AppScreen.Main, "메인 / 현황", "▦", null));
            _items.Add(new MenuItemInfo(AppScreen.CustInfo, "고객정보", "○", null));
            _items.Add(new MenuItemInfo(AppScreen.PriceItems, "상품관리", "◇", null));
            _items.Add(new MenuItemInfo(AppScreen.Order, "주문관리", "▤", "12"));
            _items.Add(new MenuItemInfo(AppScreen.Print, "출력물관리", "▣", null));
            _items.Add(new MenuItemInfo(AppScreen.Process, "공정관리", "⌯", null));
            _items.Add(new MenuItemInfo(AppScreen.Settlement, "정산관리", "▥", null));
            _items.Add(new MenuItemInfo(AppScreen.Settings, "환경설정", "☼", null));
        }

        private void BuildLayout()
        {
            Controls.Clear();

            Panel menuBody = new Panel();
            menuBody.Dock = DockStyle.Fill;
            menuBody.BackColor = UiTheme.MenuBackground;
            Controls.Add(menuBody);

            Label logo = new Label();
            logo.Text = "홈  >  메인 / 현황";
            logo.ForeColor = UiTheme.TextPrimary;
            logo.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            logo.Location = new Point(28, 24);
            logo.Size = new Size(190, 24);
            menuBody.Controls.Add(logo);

            int top = 86;
            foreach (MenuItemInfo item in _items)
            {
                Panel itemPanel = CreateMenuItem(item, top);
                menuBody.Controls.Add(itemPanel);
                _itemPanels[item.Screen] = itemPanel;
                top += 52;
            }

            Panel userPanel = CreateUserPanel();
            menuBody.Controls.Add(userPanel);
            userPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            userPanel.Location = new Point(13, Height - 85);
            userPanel.Size = new Size(UiTheme.MenuWidth - 26, 62);
            Resize += delegate
            {
                userPanel.Location = new Point(13, Height - 85);
                userPanel.Size = new Size(UiTheme.MenuWidth - 26, 62);
            };

            SelectScreen(_selectedScreen);
        }

        private Panel CreateMenuItem(MenuItemInfo item, int top)
        {
            Panel panel = new Panel();
            panel.Location = new Point(0, top);
            panel.Size = new Size(UiTheme.MenuWidth, 52);
            panel.Cursor = Cursors.Hand;
            panel.Tag = item;

            Label accent = new Label();
            accent.BackColor = UiTheme.Accent;
            accent.Location = new Point(0, 0);
            accent.Size = new Size(3, 52);
            panel.Controls.Add(accent);

            PictureBox icon = new PictureBox();
            icon.BackColor = Color.Transparent;
            icon.Image = GetMenuImage(item.Screen, item.Screen == _selectedScreen);
            icon.SizeMode = PictureBoxSizeMode.Normal;
            icon.Location = new Point(12, 6);
            icon.Size = new Size(40, 40);
            panel.Controls.Add(icon);
            _itemIcons[item.Screen] = icon;

            Label title = new Label();
            title.Text = item.Title;
            title.ForeColor = UiTheme.TextPrimary;
            title.Font = new Font("맑은 고딕", 10F, FontStyle.Bold, GraphicsUnit.Point, 129);
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Location = new Point(62, 0);
            title.Size = new Size(120, 52);
            panel.Controls.Add(title);

            if (!string.IsNullOrWhiteSpace(item.BadgeText))
            {
                Label badge = new Label();
                badge.Text = item.BadgeText;
                badge.ForeColor = Color.White;
                badge.BackColor = Color.FromArgb(20, 91, 132);
                badge.Font = new Font("맑은 고딕", 8F, FontStyle.Bold, GraphicsUnit.Point, 129);
                badge.TextAlign = ContentAlignment.MiddleCenter;
                badge.Location = new Point(154, 17);
                badge.Size = new Size(30, 18);
                panel.Controls.Add(badge);
            }

            panel.Click += MenuItem_Click;
            foreach (Control child in panel.Controls)
            {
                child.Click += MenuItem_Click;
                child.Tag = item;
            }

            return panel;
        }

        private static Bitmap GetMenuImage(AppScreen screen, bool selected)
        {
            switch (screen)
            {
                case AppScreen.Main:
                    return selected ? Resources.메인_Selected : Resources.메인_Normal;
                case AppScreen.CustInfo:
                    return selected ? Resources.고객정보_Selected : Resources.고객정보_Normal;
                case AppScreen.PriceItems:
                    return selected ? Resources.상품관리_Selected : Resources.상품관리_Normal;
                case AppScreen.Order:
                    return selected ? Resources.주문관리_Selected : Resources.주문관리_Normal;
                case AppScreen.Print:
                    return selected ? Resources.출력물관리_Selected : Resources.출력물관리_Normal;
                case AppScreen.Process:
                    return selected ? Resources.공정관리_Selected : Resources.공정관리_Normal;
                case AppScreen.Settlement:
                    return selected ? Resources.정산관리_Selected : Resources.정산관리_Normal;
                case AppScreen.Settings:
                    return selected ? Resources.환경설정_Selected : Resources.환경설정_Normal;
                default:
                    return null;
            }
        }

        private Panel CreateUserPanel()
        {
            Panel panel = new Panel();
            panel.BackColor = Color.FromArgb(27, 34, 69);

            Label avatar = new Label();
            avatar.ForeColor = UiTheme.TextPrimary;
            avatar.BackColor = Color.FromArgb(31, 47, 86);
            avatar.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            avatar.TextAlign = ContentAlignment.MiddleCenter;
            avatar.Location = new Point(14, 14);
            avatar.Size = new Size(34, 34);
            panel.Controls.Add(avatar);
            _userAvatar = avatar;

            Label userName = new Label();
            userName.ForeColor = UiTheme.TextPrimary;
            userName.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            userName.Location = new Point(56, 12);
            userName.Size = new Size(130, 20);
            panel.Controls.Add(userName);
            _userName = userName;

            Label role = new Label();
            role.ForeColor = UiTheme.TextSecondary;
            role.Font = new Font("맑은 고딕", 8F, FontStyle.Regular, GraphicsUnit.Point, 129);
            role.Location = new Point(56, 32);
            role.Size = new Size(145, 20);
            role.AutoEllipsis = true;
            panel.Controls.Add(role);
            _userRole = role;

            return panel;
        }

        private void MenuItem_Click(object sender, EventArgs e)
        {
            Control control = sender as Control;
            MenuItemInfo item = control == null ? null : control.Tag as MenuItemInfo;
            if (item == null)
            {
                return;
            }

            SelectScreen(item.Screen);
            MenuSelected?.Invoke(this, new MenuSelectedEventArgs(item));
        }
    }
}
