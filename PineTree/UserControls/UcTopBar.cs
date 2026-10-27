using System;
using System.Drawing;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Properties;

namespace PineTree.UserControls
{
    public class UcTopBar : UserControl
    {
        private const int SearchImageRightOffset = 620;
        private const int CalendarButtonRightOffset = 150;

        private static readonly Rectangle SearchTextNativeBounds = new Rectangle(45, 17, 205, 20);
        private static readonly Rectangle CalendarIconSourceBounds = new Rectangle(1776, 22, 28, 28);

        private readonly PictureBox _searchImage;
        private readonly PictureBox _previousDateButton;
        private readonly Label _dateLabel;
        private readonly PictureBox _nextDateButton;
        private readonly PictureBox _calendarButton;
        private readonly Label _userButton;
        private readonly FocusSinkControl _focusSink;
        private TextBox _searchInputBox;
        private DateTime _selectedDate = DateTime.Today;
        private ToolStripDropDown _calendarDropDown;

        public event EventHandler SelectedDateChanged;

        public UcTopBar()
        {
            Dock = DockStyle.Top;
            Height = UiTheme.TopBarHeight;
            BackColor = UiTheme.TopBarBackground;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            MouseDown += TopBar_ExternalMouseDown;

            _focusSink = new FocusSinkControl();
            Controls.Add(_focusSink);

            _searchImage = new PictureBox();
            _searchImage.Image = Resources.주문번호_거래처검색;
            _searchImage.SizeMode = PictureBoxSizeMode.Normal;
            _searchImage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _searchImage.Size = Resources.주문번호_거래처검색.Size;
            _searchImage.Location = GetSearchImageLocation();
            _searchImage.Cursor = Cursors.IBeam;
            _searchImage.Click += SearchImage_Click;
            Controls.Add(_searchImage);

            _previousDateButton = new PictureBox();
            _previousDateButton.BackColor = Color.Transparent;
            _previousDateButton.Cursor = Cursors.Hand;
            _previousDateButton.Image = Resources.Arrow_Left;
            _previousDateButton.SizeMode = PictureBoxSizeMode.CenterImage;
            _previousDateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _previousDateButton.Size = Resources.Arrow_Left.Size;
            _previousDateButton.Click += PreviousDateButton_Click;
            Controls.Add(_previousDateButton);

            _dateLabel = new Label();
            _dateLabel.ForeColor = Color.FromArgb(175, 197, 237);
            _dateLabel.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            _dateLabel.TextAlign = ContentAlignment.MiddleCenter;
            _dateLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _dateLabel.Location = new Point(Width - 292, 15);
            _dateLabel.Size = new Size(130, 28);
            _dateLabel.MouseDown += TopBar_ExternalMouseDown;
            Controls.Add(_dateLabel);

            _nextDateButton = new PictureBox();
            _nextDateButton.BackColor = Color.Transparent;
            _nextDateButton.Cursor = Cursors.Hand;
            _nextDateButton.Image = Resources.Arrow_Right;
            _nextDateButton.SizeMode = PictureBoxSizeMode.CenterImage;
            _nextDateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _nextDateButton.Size = Resources.Arrow_Right.Size;
            _nextDateButton.Click += NextDateButton_Click;
            Controls.Add(_nextDateButton);

            _calendarButton = new PictureBox();
            _calendarButton.BackColor = Color.Transparent;
            _calendarButton.Cursor = Cursors.Hand;
            _calendarButton.Image = CreateCalendarIconImage();
            _calendarButton.SizeMode = PictureBoxSizeMode.CenterImage;
            _calendarButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _calendarButton.Location = new Point(Width - CalendarButtonRightOffset, 12);
            _calendarButton.Size = new Size(35, 35);
            _calendarButton.Click += CalendarButton_Click;
            Controls.Add(_calendarButton);

            _userButton = new Label();
            _userButton.ForeColor = UiTheme.TextPrimary;
            _userButton.BackColor = Color.FromArgb(31, 47, 86);
            _userButton.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            _userButton.TextAlign = ContentAlignment.MiddleCenter;
            _userButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _userButton.Location = new Point(Width - 90, 15);
            _userButton.Size = new Size(36, 28);
            _userButton.MouseDown += TopBar_ExternalMouseDown;
            Controls.Add(_userButton);
            RefreshUserInfo();

            Resize += UcTopBar_Resize;
            LayoutDateControls();
            SetScreen(AppScreen.Main, "메인 / 현황");
        }

        public void RefreshUserInfo()
        {
            _userButton.Text = Global.UserBadge;
        }

        public void SetScreen(AppScreen screen, string title)
        {
            bool showSearch = screen == AppScreen.Main;
            _searchImage.Visible = showSearch;

            if (!showSearch)
            {
                RemoveSearchInputBox();
            }

            UpdateDateLabel();
        }

        public DateTime SelectedDate
        {
            get { return _selectedDate; }
        }

        public void SetSelectedDate(DateTime date)
        {
            _selectedDate = date.Date;
            UpdateDateLabel();
        }

        public void ReleaseSearchInputFocus()
        {
            if (_searchInputBox == null || _searchInputBox.IsDisposed)
            {
                MoveFocusToSink();
                return;
            }

            if (string.IsNullOrWhiteSpace(_searchInputBox.Text))
            {
                RemoveSearchInputBox();
                MoveFocusToSink();
                return;
            }

            MoveFocusToSink();
        }

        private void UcTopBar_Resize(object sender, EventArgs e)
        {
            _searchImage.Location = GetSearchImageLocation();
            UpdateSearchInputBounds();
            LayoutDateControls();
            _userButton.Location = new Point(Width - 90, 15);
        }

        private void SearchImage_Click(object sender, EventArgs e)
        {
            if (!_searchImage.Visible)
            {
                return;
            }

            ShowSearchInputBox();
        }

        private void PreviousDateButton_Click(object sender, EventArgs e)
        {
            SetSelectedDate(_selectedDate.AddDays(-1));
            OnSelectedDateChanged();
        }

        private void NextDateButton_Click(object sender, EventArgs e)
        {
            SetSelectedDate(_selectedDate.AddDays(1));
            OnSelectedDateChanged();
        }

        private void TopBar_ExternalMouseDown(object sender, MouseEventArgs e)
        {
            ReleaseSearchInputFocus();
        }

        private void CalendarButton_Click(object sender, EventArgs e)
        {
            ReleaseSearchInputFocus();
            ShowCalendarDropDown();
        }

        private void ShowSearchInputBox()
        {
            if (!_searchImage.Visible)
            {
                return;
            }

            if (_searchInputBox == null)
            {
                _searchInputBox = new TextBox();
                _searchInputBox.BorderStyle = BorderStyle.None;
                _searchInputBox.BackColor = Color.FromArgb(21, 23, 48);
                _searchInputBox.ForeColor = UiTheme.TextPrimary;
                _searchInputBox.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
                _searchInputBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _searchInputBox.KeyDown += SearchInputBox_KeyDown;
                _searchInputBox.Leave += SearchInputBox_Leave;
                Controls.Add(_searchInputBox);
                _searchInputBox.BringToFront();
                UpdateSearchInputBounds();
            }

            _searchInputBox.Focus();
        }

        private void SearchInputBox_Leave(object sender, EventArgs e)
        {
            TextBox searchInputBox = sender as TextBox;

            if (searchInputBox != null && string.IsNullOrWhiteSpace(searchInputBox.Text))
            {
                RemoveSearchInputBox();
            }
        }

        private void SearchInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                QueueReleaseSearchInputFocus();
                return;
            }

            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            SearchOrderOrCustomerPseudoCode(_searchInputBox.Text.Trim());
            QueueReleaseSearchInputFocus();
        }

        private void SearchOrderOrCustomerPseudoCode(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            // TODO: DB 검색 의사코드
            // 1. keyword를 주문번호 또는 거래처명 검색 조건으로 사용한다.
            // 2. DB에서 주문/거래처 후보 목록을 조회한다.
            // 3. 검색 결과가 1건이면 해당 상세 화면으로 이동한다.
            // 4. 검색 결과가 여러 건이면 결과 선택 팝업 또는 목록을 표시한다.
            // 5. 검색 결과가 없으면 사용자에게 결과 없음 상태를 표시한다.
        }

        private void UpdateSearchInputBounds()
        {
            if (_searchInputBox == null)
            {
                return;
            }

            float scaleX = (float)_searchImage.Width / Resources.주문번호_거래처검색.Width;
            float scaleY = (float)_searchImage.Height / Resources.주문번호_거래처검색.Height;

            _searchInputBox.Location = new Point(
                _searchImage.Left + (int)Math.Round(SearchTextNativeBounds.Left * scaleX),
                _searchImage.Top + (int)Math.Round(SearchTextNativeBounds.Top * scaleY));

            _searchInputBox.Size = new Size(
                (int)Math.Round(SearchTextNativeBounds.Width * scaleX),
                (int)Math.Round(SearchTextNativeBounds.Height * scaleY));
        }

        private void RemoveSearchInputBox()
        {
            if (_searchInputBox == null)
            {
                return;
            }

            TextBox searchInputBox = _searchInputBox;
            _searchInputBox = null;
            Controls.Remove(searchInputBox);
            searchInputBox.Dispose();
        }

        private void QueueReleaseSearchInputFocus()
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new MethodInvoker(ReleaseSearchInputFocus));
                return;
            }

            ReleaseSearchInputFocus();
        }

        private void MoveFocusToSink()
        {
            ActiveControl = _focusSink;
            _focusSink.Focus();
            _focusSink.Select();
        }

        private Point GetSearchImageLocation()
        {
            return new Point(
                Width - SearchImageRightOffset,
                (UiTheme.TopBarHeight - _searchImage.Height) / 2);
        }

        private void LayoutDateControls()
        {
            int calendarLeft = Width - CalendarButtonRightOffset;
            int nextDateLeft = calendarLeft - _nextDateButton.Width - 8;
            int dateLeft = nextDateLeft - _dateLabel.Width - 8;
            int previousDateLeft = dateLeft - _previousDateButton.Width - 8;
            int centerY = (UiTheme.TopBarHeight - _dateLabel.Height) / 2;

            _previousDateButton.Location = new Point(previousDateLeft + 15, (UiTheme.TopBarHeight - _previousDateButton.Height) / 2);
            _dateLabel.Location = new Point(dateLeft, centerY);
            _nextDateButton.Location = new Point(nextDateLeft - 15, (UiTheme.TopBarHeight - _nextDateButton.Height) / 2);
            _calendarButton.Location = new Point(calendarLeft, 12);
        }

        private void ShowCalendarDropDown()
        {
            if (_calendarDropDown != null)
            {
                CloseCalendarDropDown();
            }

            MonthCalendar calendar = new MonthCalendar();
            calendar.MaxSelectionCount = 1;
            calendar.SelectionStart = _selectedDate;
            calendar.SelectionEnd = _selectedDate;
            calendar.DateSelected += Calendar_DateSelected;

            ToolStripControlHost host = new ToolStripControlHost(calendar);
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;

            _calendarDropDown = new ToolStripDropDown();
            _calendarDropDown.Padding = Padding.Empty;
            _calendarDropDown.Items.Add(host);
            _calendarDropDown.Closed += CalendarDropDown_Closed;
            _calendarDropDown.Show(_calendarButton, new Point(_calendarButton.Width - calendar.Width, _calendarButton.Height + 4));
        }

        private void Calendar_DateSelected(object sender, DateRangeEventArgs e)
        {
            SetSelectedDate(e.Start);
            CloseCalendarDropDown();
            OnSelectedDateChanged();
        }

        private void CalendarDropDown_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            ToolStripDropDown dropDown = sender as ToolStripDropDown;
            if (dropDown == null)
            {
                return;
            }

            if (_calendarDropDown == dropDown)
            {
                _calendarDropDown = null;
            }

            dropDown.Closed -= CalendarDropDown_Closed;
            dropDown.Dispose();
        }

        private void CloseCalendarDropDown()
        {
            if (_calendarDropDown == null)
            {
                return;
            }

            ToolStripDropDown dropDown = _calendarDropDown;
            _calendarDropDown = null;
            dropDown.Closed -= CalendarDropDown_Closed;

            if (!dropDown.IsDisposed)
            {
                dropDown.Close();
                dropDown.Dispose();
            }
        }

        private void OnSelectedDateChanged()
        {
            EventHandler handler = SelectedDateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void UpdateDateLabel()
        {
            _dateLabel.Text = _selectedDate.ToString("yyyy-MM-dd ddd");
        }

        private Image CreateCalendarIconImage()
        {
            Bitmap source = Resources.BackMain_01;
            Rectangle bounds = CalendarIconSourceBounds;
            bounds.Intersect(new Rectangle(Point.Empty, source.Size));

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return null;
            }

            return source.Clone(bounds, source.PixelFormat);
        }

        private class FocusSinkControl : Control
        {
            public FocusSinkControl()
            {
                SetStyle(ControlStyles.Selectable, true);
                TabStop = false;
                Location = new Point(-10, -10);
                Size = new Size(1, 1);
            }
        }
    }
}
