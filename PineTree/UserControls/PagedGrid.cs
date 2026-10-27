using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControls
{
    public class PagedGridColumn
    {
        public PagedGridColumn(string key, string headerText, int width, ContentAlignment alignment)
        {
            Key = key;
            HeaderText = headerText;
            Width = width;
            Alignment = alignment;
        }

        public string Key { get; private set; }
        public string HeaderText { get; private set; }
        public int Width { get; private set; }
        public ContentAlignment Alignment { get; private set; }
    }

    public class PagedGridCell
    {
        public PagedGridCell(string text)
        {
            Text = text;
            CellType = PagedGridCellType.Text;
        }

        public string Text { get; set; }
        public PagedGridCellType CellType { get; set; }
        public bool Checked { get; set; }
        public Image Image { get; set; }
    }

    public enum PagedGridCellType
    {
        Text,
        Badge,
        Link,
        CheckBox,
        Image
    }

    public class PagedGridCellClickEventArgs : EventArgs
    {
        public PagedGridCellClickEventArgs(int rowIndex, string columnKey, Dictionary<string, PagedGridCell> row)
        {
            RowIndex = rowIndex;
            ColumnKey = columnKey;
            Row = row;
        }

        public int RowIndex { get; private set; }
        public string ColumnKey { get; private set; }
        public Dictionary<string, PagedGridCell> Row { get; private set; }
    }

    public class PagedGrid : UserControl
    {
        private const int HeaderHeight = 36;
        private const int FooterHeight = 52;
        private const int RowHeight = 41;
        private const int HorizontalPadding = 22;

        private readonly List<PagedGridColumn> _columns = new List<PagedGridColumn>();
        private readonly List<Dictionary<string, PagedGridCell>> _rows = new List<Dictionary<string, PagedGridCell>>();
        private readonly List<PagerHitArea> _pagerHitAreas = new List<PagerHitArea>();

        private int _pageIndex = 1;
        private int _pageSize = 7;
        private int _totalCount;
        private string _countPrefix = "조회건수";
        private string _totalText = string.Empty;
        private int _selectedRowIndex = -1;

        public PagedGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            ForeColor = UiTheme.TextPrimary;
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
        }

        public event EventHandler PageChanged;
        public event EventHandler SelectionChanged;
        public event EventHandler<PagedGridCellClickEventArgs> CellClicked;
        public event EventHandler<PagedGridCellClickEventArgs> CellDoubleClicked;

        public int SelectedRowIndex
        {
            get { return _selectedRowIndex; }
            set
            {
                int selectedIndex = value >= 0 && value < Math.Min(_pageSize, _rows.Count) ? value : -1;
                if (_selectedRowIndex == selectedIndex)
                {
                    return;
                }

                _selectedRowIndex = selectedIndex;
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int PageIndex
        {
            get { return _pageIndex; }
            set
            {
                _pageIndex = Math.Min(Math.Max(1, value), GetTotalPages());
                Invalidate();
            }
        }

        public int PageSize
        {
            get { return _pageSize; }
            set
            {
                _pageSize = Math.Max(1, value);
                Invalidate();
            }
        }

        public int TotalCount
        {
            get { return _totalCount; }
            set
            {
                _totalCount = Math.Max(0, value);
                Invalidate();
            }
        }

        public string CountPrefix
        {
            get { return _countPrefix; }
            set
            {
                _countPrefix = string.IsNullOrEmpty(value) ? "조회건수" : value;
                Invalidate();
            }
        }

        public string TotalText
        {
            get { return _totalText; }
            set
            {
                _totalText = value ?? string.Empty;
                Invalidate();
            }
        }

        public IList<PagedGridColumn> Columns
        {
            get { return _columns; }
        }

        public void SetRows(IEnumerable<Dictionary<string, PagedGridCell>> rows)
        {
            _rows.Clear();
            SelectedRowIndex = -1;

            if (rows != null)
            {
                foreach (Dictionary<string, PagedGridCell> row in rows)
                {
                    _rows.Add(row);
                }
            }

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
            }

            foreach (PagerHitArea hitArea in _pagerHitAreas)
            {
                if (hitArea.Bounds.Contains(e.Location) && !hitArea.Disabled)
                {
                    if (_pageIndex != hitArea.PageIndex)
                    {
                        _pageIndex = hitArea.PageIndex;
                        Invalidate();
                        OnPageChanged(EventArgs.Empty);
                    }

                    return;
                }
            }

            PagedGridCellClickEventArgs cellClick = GetCellClick(e.Location);
            if (cellClick != null)
            {
                PagedGridCell cell = GetCell(cellClick.Row, cellClick.ColumnKey);
                if (cell.CellType == PagedGridCellType.CheckBox)
                {
                    if (e.Button != MouseButtons.Left)
                    {
                        return;
                    }

                    cell.Checked = !cell.Checked;
                    SelectedRowIndex = cellClick.RowIndex;
                    Invalidate();
                }
                OnCellClicked(cellClick);
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.PageDown || keyData == Keys.PageUp)
            {
                return true;
            }

            if (_selectedRowIndex >= 0 && (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Enter || keyData == Keys.Space))
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Modifiers == Keys.None && e.KeyCode == Keys.Space && _selectedRowIndex >= 0)
            {
                foreach (PagedGridColumn column in _columns)
                {
                    PagedGridCell cell = GetCell(_rows[_selectedRowIndex], column.Key);
                    if (cell.CellType == PagedGridCellType.CheckBox)
                    {
                        cell.Checked = !cell.Checked;
                        Invalidate();
                        OnCellClicked(new PagedGridCellClickEventArgs(_selectedRowIndex, column.Key, _rows[_selectedRowIndex]));
                        e.SuppressKeyPress = true;
                        break;
                    }
                }
            }

            if (e.Modifiers == Keys.None && (e.KeyCode == Keys.PageDown || e.KeyCode == Keys.PageUp))
            {
                int lastRowIndex = Math.Min(_pageSize, _rows.Count) - 1;
                if (lastRowIndex >= 0)
                {
                    if (e.KeyCode == Keys.PageDown)
                    {
                        if (_selectedRowIndex == lastRowIndex && _pageIndex < GetTotalPages())
                        {
                            PageIndex = _pageIndex + 1;
                            OnPageChanged(EventArgs.Empty);
                            SelectedRowIndex = _rows.Count > 0 ? 0 : -1;
                        }
                        else
                        {
                            SelectedRowIndex = lastRowIndex;
                        }
                    }
                    else if (_selectedRowIndex == 0 && _pageIndex > 1)
                    {
                        PageIndex = _pageIndex - 1;
                        OnPageChanged(EventArgs.Empty);
                        SelectedRowIndex = Math.Min(_pageSize, _rows.Count) - 1;
                    }
                    else
                    {
                        SelectedRowIndex = 0;
                    }
                }
                e.SuppressKeyPress = true;
            }

            if (_selectedRowIndex >= 0 && e.Modifiers == Keys.None &&
                (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                int lastRowIndex = Math.Min(_pageSize, _rows.Count) - 1;
                int direction = e.KeyCode == Keys.Up ? -1 : 1;
                if (direction > 0 && _selectedRowIndex == lastRowIndex && _pageIndex < GetTotalPages())
                {
                    PageIndex = _pageIndex + 1;
                    OnPageChanged(EventArgs.Empty);
                    SelectedRowIndex = _rows.Count > 0 ? 0 : -1;
                }
                else if (direction < 0 && _selectedRowIndex == 0 && _pageIndex > 1)
                {
                    PageIndex = _pageIndex - 1;
                    OnPageChanged(EventArgs.Empty);
                    SelectedRowIndex = Math.Min(_pageSize, _rows.Count) - 1;
                }
                else
                {
                    SelectedRowIndex = Math.Max(0, Math.Min(lastRowIndex, _selectedRowIndex + direction));
                }
                e.SuppressKeyPress = true;
            }

            base.OnKeyDown(e);
        }

        protected virtual void OnPageChanged(EventArgs e)
        {
            EventHandler handler = PageChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            PagedGridCellClickEventArgs cellClick = GetCellClick(e.Location);
            if (cellClick != null && CellDoubleClicked != null)
            {
                CellDoubleClicked(this, cellClick);
            }
        }

        protected virtual void OnCellClicked(PagedGridCellClickEventArgs e)
        {
            EventHandler<PagedGridCellClickEventArgs> handler = CellClicked;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawHeader(e.Graphics);
            DrawRows(e.Graphics);
            DrawFooter(e.Graphics);
        }

        private void DrawHeader(Graphics graphics)
        {
            Rectangle headerRect = new Rectangle(0, 0, Width - 1, HeaderHeight);
            using (SolidBrush brush = new SolidBrush(UiTheme.GridHeaderBackground))
            {
                graphics.FillRectangle(brush, headerRect);
            }

            using (Pen pen = new Pen(UiTheme.GridLine))
            {
                graphics.DrawLine(pen, 0, HeaderHeight - 1, Width, HeaderHeight - 1);
            }

            int x = HorizontalPadding;
            foreach (PagedGridColumn column in _columns)
            {
                Rectangle cellBounds = new Rectangle(x, 0, GetColumnWidth(column), HeaderHeight);
                DrawText(graphics, column.HeaderText, cellBounds, UiTheme.TextSecondary, 8F, FontStyle.Bold, column.Alignment);
                x += cellBounds.Width;
            }
        }

        private void DrawRows(Graphics graphics)
        {
            int visibleRows = Math.Min(_pageSize, _rows.Count);

            for (int rowIndex = 0; rowIndex < visibleRows; rowIndex++)
            {
                Dictionary<string, PagedGridCell> row = _rows[rowIndex];
                int x = HorizontalPadding;
                int y = HeaderHeight + (rowIndex * RowHeight);

                if (rowIndex == _selectedRowIndex)
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(22, 53, 79)))
                    {
                        graphics.FillRectangle(brush, 0, y, Width, RowHeight);
                    }
                }

                foreach (PagedGridColumn column in _columns)
                {
                    Rectangle cellBounds = new Rectangle(x, y, GetColumnWidth(column), RowHeight);
                    PagedGridCell cell = GetCell(row, column.Key);
                    DrawCell(graphics, cell, cellBounds, column.Alignment);
                    x += cellBounds.Width;
                }
            }
        }

        private void DrawFooter(Graphics graphics)
        {
            int footerTop = Math.Min(HeaderHeight + (_pageSize * RowHeight), Math.Max(HeaderHeight, Height - FooterHeight));

            using (Pen pen = new Pen(UiTheme.GridLine))
            {
                graphics.DrawLine(pen, 0, footerTop, Width, footerTop);
            }

            string countText = string.Format("{0} : {1}건", _countPrefix, _rows.Count);
            if (!string.IsNullOrEmpty(_totalText))
            {
                countText += " (" + _totalText + ")";
            }

            DrawText(graphics, countText, new Rectangle(HorizontalPadding, footerTop + 12, 300, 24), UiTheme.TextSecondary, 8F, FontStyle.Regular, ContentAlignment.MiddleLeft);
            DrawPager(graphics, footerTop);
        }

        private void DrawCell(Graphics graphics, PagedGridCell cell, Rectangle bounds, ContentAlignment alignment)
        {
            if (cell == null)
            {
                return;
            }

            if (cell.CellType == PagedGridCellType.Badge)
            {
                DrawBadge(graphics, cell.Text, bounds);
                return;
            }

            if (cell.CellType == PagedGridCellType.Image)
            {
                if (cell.Image != null)
                {
                    float scale = Math.Min(1F, Math.Min((float)Math.Max(1, bounds.Width - 8) / cell.Image.Width,
                        (float)Math.Max(1, bounds.Height - 8) / cell.Image.Height));
                    int width = Math.Max(1, (int)(cell.Image.Width * scale));
                    int height = Math.Max(1, (int)(cell.Image.Height * scale));
                    graphics.DrawImage(cell.Image, new Rectangle(bounds.Left + (bounds.Width - width) / 2,
                        bounds.Top + (bounds.Height - height) / 2, width, height));
                }
                return;
            }

            if (cell.CellType == PagedGridCellType.CheckBox)
            {
                Rectangle checkBounds = new Rectangle(bounds.Left + (bounds.Width - 16) / 2,
                    bounds.Top + (bounds.Height - 16) / 2, 16, 16);
                ControlPaint.DrawCheckBox(graphics, checkBounds, cell.Checked ? ButtonState.Checked : ButtonState.Normal);
                return;
            }

            Color color = cell.CellType == PagedGridCellType.Link ? UiTheme.Accent : Color.FromArgb(195, 225, 255);
            DrawText(graphics, cell.Text, bounds, color, 9F, FontStyle.Regular, alignment);
        }

        private void DrawBadge(Graphics graphics, string text, Rectangle bounds)
        {
            SizeF textSize;
            using (Font badgeFont = new Font("맑은 고딕", 8F, FontStyle.Bold, GraphicsUnit.Point, 129))
            {
                textSize = graphics.MeasureString(text, badgeFont);
            }

            int badgeWidth = Math.Max(62, (int)Math.Ceiling(textSize.Width) + 20);
            Rectangle badgeBounds = new Rectangle(bounds.Left, bounds.Top + 10, badgeWidth, 20);

            using (SolidBrush brush = new SolidBrush(GetBadgeColor(text)))
            using (System.Drawing.Drawing2D.GraphicsPath path = CreateRoundRectangle(badgeBounds, 10))
            {
                graphics.FillPath(brush, path);
            }

            DrawText(graphics, text, badgeBounds, Color.White, 8F, FontStyle.Bold, ContentAlignment.MiddleCenter);
        }

        private void DrawPager(Graphics graphics, int footerTop)
        {
            _pagerHitAreas.Clear();

            int totalPages = GetTotalPages();
            int firstPage = Math.Max(1, Math.Min(_pageIndex - 1, Math.Max(1, totalPages - 2)));
            string[] items;
            if (totalPages <= 1)
            {
                items = new[] { "<", "1", ">" };
            }
            else if (totalPages == 2)
            {
                items = new[] { "<", "1", "2", ">" };
            }
            else
            {
                items = new[] { "<", firstPage.ToString(), (firstPage + 1).ToString(), (firstPage + 2).ToString(), ">" };
            }
            int buttonWidth = 34;
            int totalWidth = items.Length * buttonWidth;
            int x = Math.Max(HorizontalPadding, (Width - totalWidth) / 2);
            int y = footerTop + 14;

            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i];
                bool active = item == _pageIndex.ToString();
                bool disabled = (item == "<" && _pageIndex <= 1) || (item == ">" && _pageIndex >= totalPages);
                int targetPage = GetTargetPage(item, totalPages);
                disabled = disabled || targetPage > totalPages;
                Color textColor = disabled ? Color.FromArgb(73, 91, 126) : UiTheme.TextSecondary;

                if (active)
                {
                    Rectangle activeBounds = new Rectangle(x + 5, y - 2, 24, 22);
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(18, 82, 117)))
                    using (System.Drawing.Drawing2D.GraphicsPath path = CreateRoundRectangle(activeBounds, 6))
                    {
                        graphics.FillPath(brush, path);
                    }

                    textColor = Color.FromArgb(180, 235, 255);
                }

                Rectangle hitBounds = new Rectangle(x, y - 3, buttonWidth, 26);
                _pagerHitAreas.Add(new PagerHitArea(hitBounds, targetPage, disabled));
                DrawText(graphics, item, hitBounds, textColor, 8F, FontStyle.Regular, ContentAlignment.MiddleCenter);
                x += buttonWidth;
            }
        }

        private int GetTargetPage(string item, int totalPages)
        {
            if (item == "<")
            {
                return Math.Max(1, _pageIndex - 1);
            }

            if (item == ">")
            {
                return Math.Min(totalPages, _pageIndex + 1);
            }

            int pageNumber;
            if (int.TryParse(item, out pageNumber))
            {
                return Math.Min(pageNumber, totalPages);
            }

            return _pageIndex;
        }

        private int GetTotalPages()
        {
            return Math.Max(1, (int)Math.Ceiling((double)Math.Max(_totalCount, _rows.Count) / _pageSize));
        }

        private int GetColumnWidth(PagedGridColumn column)
        {
            int fixedWidth = 0;
            int elasticCount = 0;

            foreach (PagedGridColumn item in _columns)
            {
                if (item.Width > 0)
                {
                    fixedWidth += item.Width;
                }
                else
                {
                    elasticCount++;
                }
            }

            if (column.Width > 0 || elasticCount == 0)
            {
                return column.Width;
            }

            int available = Math.Max(80, Width - (HorizontalPadding * 2) - fixedWidth);
            return Math.Max(80, available / elasticCount);
        }

        private PagedGridCellClickEventArgs GetCellClick(Point location)
        {
            int visibleRows = Math.Min(_pageSize, _rows.Count);
            int rowIndex = (location.Y - HeaderHeight) / RowHeight;
            if (location.Y < HeaderHeight || rowIndex < 0 || rowIndex >= visibleRows)
            {
                return null;
            }

            int x = HorizontalPadding;
            foreach (PagedGridColumn column in _columns)
            {
                int width = GetColumnWidth(column);
                Rectangle bounds = new Rectangle(x, HeaderHeight + (rowIndex * RowHeight), width, RowHeight);
                if (bounds.Contains(location))
                {
                    return new PagedGridCellClickEventArgs(rowIndex, column.Key, _rows[rowIndex]);
                }

                x += width;
            }

            return null;
        }

        private static PagedGridCell GetCell(Dictionary<string, PagedGridCell> row, string key)
        {
            PagedGridCell cell;
            if (row != null && row.TryGetValue(key, out cell))
            {
                return cell;
            }

            return new PagedGridCell(string.Empty);
        }

        private static void DrawText(Graphics graphics, string text, Rectangle bounds, Color color, float size, FontStyle style, ContentAlignment alignment)
        {
            TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

            if (alignment == ContentAlignment.MiddleCenter)
            {
                flags |= TextFormatFlags.HorizontalCenter;
            }
            else if (alignment == ContentAlignment.MiddleRight)
            {
                flags |= TextFormatFlags.Right;
            }
            else
            {
                flags |= TextFormatFlags.Left;
            }

            using (Font font = new Font("맑은 고딕", size, style, GraphicsUnit.Point, 129))
            {
                TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
            }
        }

        private static Color GetBadgeColor(string status)
        {
            switch (status)
            {
                case "출력 중":
                    return Color.FromArgb(21, 83, 118);
                case "후가공":
                    return Color.FromArgb(95, 72, 47);
                case "포장 대기":
                    return Color.FromArgb(103, 48, 82);
                case "포장 완료":
                    return Color.FromArgb(31, 104, 91);
                case "미작업":
                    return Color.FromArgb(45, 55, 90);
                default:
                    return Color.FromArgb(45, 55, 95);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundRectangle(Rectangle bounds, int radius)
        {
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));

            if (diameter <= 0)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }

            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        private class PagerHitArea
        {
            public PagerHitArea(Rectangle bounds, int pageIndex, bool disabled)
            {
                Bounds = bounds;
                PageIndex = pageIndex;
                Disabled = disabled;
            }

            public Rectangle Bounds { get; private set; }
            public int PageIndex { get; private set; }
            public bool Disabled { get; private set; }
        }
    }
}
