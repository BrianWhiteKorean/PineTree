using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public class UcDashboardHome : UserControl
    {
        private const int ActionButtonsRightMargin = 70;

        private Panel _orderPanel;
        private PictureBox _todayOrderCard;
        private Label _todayOrderCardCountLabel;
        private PictureBox _printWaitingCard;
        private Label _printWaitingCardCountLabel;
        private PictureBox _cuttingWaitingCard;
        private Label _cuttingWaitingCardCountLabel;
        private PictureBox _completionWaitingCard;
        private Label _completionWaitingCardCountLabel;
        private Label _dashboardTitleLabel;
        private Label _dashboardDateLabel;
        private Label _orderPanelTitleLabel;
        private PagedGrid _todayOrderGrid;
        private DataTable _todayOrders;
        private DateTime _selectedDate = DateTime.Today;
        private bool _todayOrderCardSelected = false;
        private bool _printWaitingCardSelected = false;
        private bool _cuttingWaitingCardSelected = false;
        private bool _completionWaitingCardSelected = false;

        internal event EventHandler ProductionStatusChanged;

        public UcDashboardHome()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Dock = DockStyle.Fill;
            BackColor = UiTheme.AppBackground;
            BuildLayout();
            Load += UcDashboardHome_Load;
        }

        private void UcDashboardHome_Load(object sender, EventArgs e)
        {
            LoadSelectedDateOrders();
        }

        public void RefreshOrders()
        {
            LoadSelectedDateOrders(_todayOrderGrid.PageIndex);
        }

        public void SetSelectedDate(DateTime date)
        {
            DateTime selectedDate = date.Date;
            bool changed = _selectedDate != selectedDate;
            _selectedDate = selectedDate;
            UpdateDateTexts();

            if (changed || _todayOrders == null)
            {
                LoadSelectedDateOrders();
            }
        }

        private void BuildLayout()
        {
            Controls.Clear();

            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = UiTheme.AppBackground;
            content.Padding = new Padding(UiTheme.ContentPadding);
            Controls.Add(content);

            Label title = new Label();
            title.Text = "오늘의 현황";
            title.ForeColor = UiTheme.TextPrimary;
            title.Font = new Font("맑은 고딕", 16F, FontStyle.Bold, GraphicsUnit.Point, 129);
            title.Location = new Point(28, 18);
            title.Size = new Size(150, 34);
            _dashboardTitleLabel = title;
            content.Controls.Add(title);

            Label date = new Label();
            date.Text = _selectedDate.ToString("yyyy-MM-dd dddd");
            date.ForeColor = UiTheme.TextSecondary;
            date.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            date.Location = new Point(180, 25);
            date.Size = new Size(230, 24);
            _dashboardDateLabel = date;
            content.Controls.Add(date);

            CreateStatusCards(content);
            CreateOrderPanel(content);
            UpdateDateTexts();
        }

        private void CreateStatusCards(Control parent)
        {
            CreateTodayOrderStatusCard(parent, 28, 76);
            CreatePrintWaitingStatusCard(parent, 355, 76);
            CreateCuttingWaitingStatusCard(parent, 682, 76);
            CreateCompletionWaitingStatusCard(parent, 1009, 76);
        }

        private void CreateTodayOrderStatusCard(Control parent, int x, int y)
        {
            PictureBox card = new PictureBox();
            card.BackColor = Color.Transparent;
            card.Cursor = Cursors.Hand;
            card.Location = new Point(x, y);
            card.Size = PineTree.Properties.Resources.현재주문_Selected.Size;
            card.SizeMode = PictureBoxSizeMode.Normal;
            _todayOrderCard = card;
            parent.Controls.Add(card);

            Label countLabel = CreateLabel("0", 28, 48, 75, 45, Color.FromArgb(82, 220, 255), 25F, FontStyle.Bold);
            countLabel.Cursor = Cursors.Hand;
            _todayOrderCardCountLabel = countLabel;
            card.Controls.Add(countLabel);

            card.Click += TodayOrderCard_Click;
            countLabel.Click += TodayOrderCard_Click;
            ApplyTodayOrderCardState();
        }

        private void TodayOrderCard_Click(object sender, EventArgs e)
        {
            _todayOrderCardSelected = true;
            _printWaitingCardSelected = false;
            _cuttingWaitingCardSelected = false;
            _completionWaitingCardSelected = false;
            ApplyStatusCardStates();
        }

        private void CreatePrintWaitingStatusCard(Control parent, int x, int y)
        {
            PictureBox card = new PictureBox();
            card.BackColor = Color.Transparent;
            card.Cursor = Cursors.Hand;
            card.Location = new Point(x, y);
            card.Size = PineTree.Properties.Resources.출력대기_Selected.Size;
            card.SizeMode = PictureBoxSizeMode.Normal;
            _printWaitingCard = card;
            parent.Controls.Add(card);

            Label countLabel = CreateLabel("24", 28, 48, 75, 45, Color.FromArgb(82, 220, 255), 25F, FontStyle.Bold);
            countLabel.Cursor = Cursors.Hand;
            _printWaitingCardCountLabel = countLabel;
            card.Controls.Add(countLabel);

            card.Click += PrintWaitingCard_Click;
            countLabel.Click += PrintWaitingCard_Click;
            ApplyStatusCardStates();
        }

        private void PrintWaitingCard_Click(object sender, EventArgs e)
        {
            _todayOrderCardSelected = false;
            _printWaitingCardSelected = true;
            _cuttingWaitingCardSelected = false;
            _completionWaitingCardSelected = false;
            ApplyStatusCardStates();
        }

        private void CreateCuttingWaitingStatusCard(Control parent, int x, int y)
        {
            PictureBox card = new PictureBox();
            card.BackColor = Color.Transparent;
            card.Cursor = Cursors.Hand;
            card.Location = new Point(x, y);
            card.Size = PineTree.Properties.Resources.컷팅대기_Selected.Size;
            card.SizeMode = PictureBoxSizeMode.Normal;
            _cuttingWaitingCard = card;
            parent.Controls.Add(card);

            Label countLabel = CreateLabel("11", 28, 48, 75, 45, Color.FromArgb(82, 220, 255), 25F, FontStyle.Bold);
            countLabel.Cursor = Cursors.Hand;
            _cuttingWaitingCardCountLabel = countLabel;
            card.Controls.Add(countLabel);

            card.Click += CuttingWaitingCard_Click;
            countLabel.Click += CuttingWaitingCard_Click;
            ApplyStatusCardStates();
        }

        private void CuttingWaitingCard_Click(object sender, EventArgs e)
        {
            _todayOrderCardSelected = false;
            _printWaitingCardSelected = false;
            _cuttingWaitingCardSelected = true;
            _completionWaitingCardSelected = false;
            ApplyStatusCardStates();
        }

        private void CreateCompletionWaitingStatusCard(Control parent, int x, int y)
        {
            PictureBox card = new PictureBox();
            card.BackColor = Color.Transparent;
            card.Cursor = Cursors.Hand;
            card.Location = new Point(x, y);
            card.Size = PineTree.Properties.Resources.완료대기_Selected.Size;
            card.SizeMode = PictureBoxSizeMode.Normal;
            _completionWaitingCard = card;
            parent.Controls.Add(card);

            Label countLabel = CreateLabel("29", 28, 48, 75, 45, Color.FromArgb(82, 220, 255), 25F, FontStyle.Bold);
            countLabel.Cursor = Cursors.Hand;
            _completionWaitingCardCountLabel = countLabel;
            card.Controls.Add(countLabel);

            card.Click += CompletionWaitingCard_Click;
            countLabel.Click += CompletionWaitingCard_Click;
            ApplyStatusCardStates();
        }

        private void CompletionWaitingCard_Click(object sender, EventArgs e)
        {
            _todayOrderCardSelected = false;
            _printWaitingCardSelected = false;
            _cuttingWaitingCardSelected = false;
            _completionWaitingCardSelected = true;
            ApplyStatusCardStates();
        }

        private void ApplyStatusCardStates()
        {
            ApplyTodayOrderCardState();
            ApplyPrintWaitingCardState();
            ApplyCuttingWaitingCardState();
            ApplyCompletionWaitingCardState();
        }

        private void ApplyTodayOrderCardState()
        {
            if (_todayOrderCard == null)
            {
                return;
            }

            _todayOrderCard.Image = _todayOrderCardSelected
                ? PineTree.Properties.Resources.현재주문_Selected
                : PineTree.Properties.Resources.현재주문_Normal;

            if (_todayOrderCardCountLabel != null)
            {
                _todayOrderCardCountLabel.ForeColor = _todayOrderCardSelected
                    ? Color.FromArgb(82, 220, 255)
                    : UiTheme.TextPrimary;
            }
        }

        private void ApplyPrintWaitingCardState()
        {
            if (_printWaitingCard == null)
            {
                return;
            }

            _printWaitingCard.Image = _printWaitingCardSelected
                ? PineTree.Properties.Resources.출력대기_Selected
                : PineTree.Properties.Resources.출력대기_Normal;

            if (_printWaitingCardCountLabel != null)
            {
                _printWaitingCardCountLabel.ForeColor = _printWaitingCardSelected
                    ? Color.FromArgb(82, 220, 255)
                    : UiTheme.TextPrimary;
            }
        }

        private void ApplyCuttingWaitingCardState()
        {
            if (_cuttingWaitingCard == null)
            {
                return;
            }

            _cuttingWaitingCard.Image = _cuttingWaitingCardSelected
                ? PineTree.Properties.Resources.컷팅대기_Selected
                : PineTree.Properties.Resources.컷팅대기_Normal;

            if (_cuttingWaitingCardCountLabel != null)
            {
                _cuttingWaitingCardCountLabel.ForeColor = _cuttingWaitingCardSelected
                    ? Color.FromArgb(82, 220, 255)
                    : UiTheme.TextPrimary;
            }
        }

        private void ApplyCompletionWaitingCardState()
        {
            if (_completionWaitingCard == null)
            {
                return;
            }

            _completionWaitingCard.Image = _completionWaitingCardSelected
                ? PineTree.Properties.Resources.완료대기_Selected
                : PineTree.Properties.Resources.완료대기_Normal;

            if (_completionWaitingCardCountLabel != null)
            {
                _completionWaitingCardCountLabel.ForeColor = _completionWaitingCardSelected
                    ? Color.FromArgb(82, 220, 255)
                    : UiTheme.TextPrimary;
            }
        }

        private void UpdateTodayOrderCardCount(int count)
        {
            if (_todayOrderCardCountLabel == null)
            {
                return;
            }

            _todayOrderCardCountLabel.Text = count.ToString("#,##0");
        }

        private void CreateStatusCard(Control parent, string title, string count, string subText, int x, int y, Color accent)
        {
            Panel card = CreateCard(new Rectangle(x, y, 310, 118));
            parent.Controls.Add(card);

            Label titleLabel = CreateLabel(title, 28, 22, 130, 22, UiTheme.TextSecondary, 9F, FontStyle.Bold);
            card.Controls.Add(titleLabel);

            Label countLabel = CreateLabel(count, 28, 47, 78, 48, UiTheme.TextPrimary, 25F, FontStyle.Regular);
            card.Controls.Add(countLabel);

            Label unitLabel = CreateLabel("건", 86, 67, 40, 22, UiTheme.TextSecondary, 9F, FontStyle.Bold);
            card.Controls.Add(unitLabel);

            Label subLabel = CreateLabel(subText, 28, 90, 230, 20, UiTheme.TextSecondary, 8F, FontStyle.Regular);
            card.Controls.Add(subLabel);

            Label dot = new Label();
            dot.BackColor = accent;
            dot.Location = new Point(275, 30);
            dot.Size = new Size(12, 12);
            card.Controls.Add(dot);
        }

        private void CreateOrderPanel(Control parent)
        {
            Panel panel = CreateCard(new Rectangle(28, 218, 100, 695));
            panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _orderPanel = panel;
            parent.Controls.Add(panel);

            Label title = CreateLabel("오늘 주문", 28, 20, 130, 28, UiTheme.TextPrimary, 11F, FontStyle.Bold);
            _orderPanelTitleLabel = title;
            panel.Controls.Add(title);

            Button search = new Button();
            search.FlatStyle = FlatStyle.Flat;
            search.FlatAppearance.BorderSize = 0;
            search.FlatAppearance.MouseDownBackColor = Color.Transparent;
            search.FlatAppearance.MouseOverBackColor = Color.Transparent;
            search.BackColor = Color.Transparent;
            search.Image = PineTree.Properties.Resources.조회_Normal;
            search.ImageAlign = ContentAlignment.MiddleCenter;
            search.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            search.Size = PineTree.Properties.Resources.조회_Normal.Size;
            search.Location = GetSearchButtonLocation(parent.ClientSize.Width, search.Width);
            search.MouseDown += delegate { search.Image = PineTree.Properties.Resources.조회_Clicked; };
            search.MouseUp += delegate { search.Image = PineTree.Properties.Resources.조회_Normal; };
            search.MouseLeave += delegate { search.Image = PineTree.Properties.Resources.조회_Normal; };
            search.Click += delegate { LoadSelectedDateOrders(); };
            parent.Controls.Add(search);
            search.BringToFront();

            parent.Resize += delegate
            {
                search.Location = GetSearchButtonLocation(parent.ClientSize.Width, search.Width);
            };

            PagedGrid grid = CreateTodayOrderGrid();
            _todayOrderGrid = grid;
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.Location = new Point(0, 58);
            grid.PageSize = CalculatePageSize(panel.Height - 58);
            grid.Size = new Size(panel.Width, panel.Height - 58);
            grid.PageChanged += delegate { BindTodayOrderPage(); };
            grid.CellClicked += TodayOrderGrid_CellClicked;
            panel.Controls.Add(grid);

            panel.Resize += delegate
            {
                grid.PageSize = CalculatePageSize(panel.ClientSize.Height - 58);
                grid.Size = new Size(panel.ClientSize.Width, panel.ClientSize.Height - 58);
                grid.PageIndex = grid.PageIndex;
                BindTodayOrderPage();
            };
        }

        private void ShowEditOrderDialog(long orderId)
        {
            if (orderId <= 0)
            {
                return;
            }

            bool previousWaitCursor = UseWaitCursor;
            Cursor previousCursor = Cursor.Current;
            Action restoreCursor = delegate {
                UseWaitCursor = previousWaitCursor;
                Cursor.Current = previousCursor;
            };
            DialogResult result;
            try
            {
                UseWaitCursor = true;
                Cursor.Current = Cursors.WaitCursor;
                using (DialogOrderDesignPad dialog = new DialogOrderDesignPad(orderId))
                {
                    dialog.Shown += delegate { restoreCursor(); };
                    result = dialog.ShowDialog(this);
                }
            }
            finally { restoreCursor(); }
            if (result == DialogResult.OK) LoadSelectedDateOrders();
        }

        private async void TodayOrderGrid_CellClicked(object sender, PagedGridCellClickEventArgs e)
        {
            if (e == null || e.Row == null)
            {
                return;
            }

            _todayOrderGrid.SelectedRowIndex = e.RowIndex;

            PagedGridCell orderIdCell;
            if (!e.Row.TryGetValue("OrderId", out orderIdCell))
            {
                return;
            }

            long orderId;
            if (!long.TryParse(orderIdCell.Text, out orderId))
            {
                return;
            }

            if (e.ColumnKey == OrderProductionStatusCell.ColumnKey)
            {
                if (await OrderProductionStatusCell.FeedAsync(this, orderId, e.Row[e.ColumnKey]))
                {
                    RefreshOrders();
                    ProductionStatusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (e.ColumnKey == "Manage" && e.Row["Manage"].CellType == PagedGridCellType.Link)
            {
                ShowEditOrderDialog(orderId);
            }
        }

        private PagedGrid CreateTodayOrderGrid()
        {
            PagedGrid grid = new PagedGrid();
            grid.RowGroupKey = "OrderId";
            grid.PageIndex = 1;
            grid.PageSize = 14;
            grid.TotalCount = 0;
            grid.TotalText = CreateTotalText(0);

            grid.Columns.Add(new PagedGridColumn("OrderNo", "주문번호", 160, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn("Customer", "거래처명", 190, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn("Item", "품목", 210, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn("Size", "규격", 165, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn("Quantity", "수량", 100, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn("OrderTitle", "주문명", 230, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn("ConsigneeName", "주문자", 120, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn(OrderProductionStatusCell.ColumnKey, "진행상태", 120, ContentAlignment.MiddleLeft));
            grid.Columns.Add(new PagedGridColumn("Manage", "관리", 0, ContentAlignment.MiddleLeft) { MinimumFillWidth = 30 });

            grid.SetRows(new Dictionary<string, PagedGridCell>[0]);
            return grid;
        }

        private void LoadSelectedDateOrders(int pageIndex = 1)
        {
            try
            {
                _todayOrders = OrderRepository.GetOrdersByOrderDate(_selectedDate);
                UpdateTodayOrderCardCount(CountOrders(_todayOrders));
                UpdateStatusCardCounts(OrderRepository.GetStatusCountsByOrderDate(_selectedDate));

                if (_todayOrderGrid != null)
                {
                    _todayOrderGrid.TotalCount = _todayOrders.Rows.Count;
                    _todayOrderGrid.PageIndex = pageIndex;
                    _todayOrderGrid.TotalText = CreateTotalText(CountOrders(_todayOrders));
                    BindTodayOrderPage();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("선택한 날짜의 주문 내역을 조회하지 못했습니다.\r\n" + ex.Message, "주문 조회", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateStatusCardCounts(Dictionary<OrderProductionStatus, int> counts)
        {
            if (counts == null)
            {
                return;
            }

            int count;
            if (_printWaitingCardCountLabel != null)
            {
                _printWaitingCardCountLabel.Text =
                    counts.TryGetValue(OrderProductionStatus.Printing, out count) ? count.ToString("#,##0") : "0";
            }

            if (_cuttingWaitingCardCountLabel != null)
            {
                _cuttingWaitingCardCountLabel.Text =
                    counts.TryGetValue(OrderProductionStatus.PostProcess, out count) ? count.ToString("#,##0") : "0";
            }

            if (_completionWaitingCardCountLabel != null)
            {
                _completionWaitingCardCountLabel.Text =
                    counts.TryGetValue(OrderProductionStatus.PackingWaiting, out count) ? count.ToString("#,##0") : "0";
            }
        }

        private static string GetOrderItemDisplayName(DataRow row)
        {
            string itemName = ToText(row["ItemName"]);
            return string.IsNullOrWhiteSpace(itemName) ? ToText(row["CustomName"]) : itemName;
        }

        private void BindTodayOrderPage()
        {
            if (_todayOrderGrid == null)
            {
                return;
            }

            List<Dictionary<string, PagedGridCell>> rows = new List<Dictionary<string, PagedGridCell>>();
            if (_todayOrders != null)
            {
                int startIndex = (_todayOrderGrid.PageIndex - 1) * _todayOrderGrid.PageSize;
                int endIndex = Math.Min(startIndex + _todayOrderGrid.PageSize, _todayOrders.Rows.Count);

                for (int index = startIndex; index < endIndex; index++)
                {
                    DataRow row = _todayOrders.Rows[index];
                    bool firstItem = index == 0 || !Equals(row["OrderId"], _todayOrders.Rows[index - 1]["OrderId"]);
                    var displayRow = CreateOrderRow(
                        ToText(row["OrderId"]),
                        ToText(row["OrderNo"]),
                        ToText(row["CustName"]),
                        GetOrderItemDisplayName(row),
                        CreateSizeText(row),
                        CreateQuantityText(row),
                        firstItem ? ToText(row["StoredOrderTitle"]) : string.Empty,
                        firstItem ? ToText(row["ConsigneeName"]) : string.Empty,
                        ToText(row["ProductionStatus"]));
                    if (!firstItem) {
                        displayRow["OrderNo"] = new PagedGridCell(string.Empty);
                        displayRow["Manage"] = new PagedGridCell(string.Empty);
                    }
                    rows.Add(displayRow);
                }
            }

            _todayOrderGrid.SetRows(rows);
        }

        private static Dictionary<string, PagedGridCell> CreateOrderRow(string orderId, string orderNo, string customer,
            string item, string size, string quantity, string orderTitle, string consigneeName, string status)
        {
            return new Dictionary<string, PagedGridCell>
            {
                { "OrderId", new PagedGridCell(orderId) },
                { "OrderNo", new PagedGridCell(orderNo) },
                { "Customer", new PagedGridCell(customer) },
                { "Item", new PagedGridCell(item) },
                { "Size", new PagedGridCell(size) },
                { "Quantity", new PagedGridCell(quantity) },
                { "OrderTitle", new PagedGridCell(orderTitle) },
                { "ConsigneeName", new PagedGridCell(consigneeName) },
                { OrderProductionStatusCell.ColumnKey, OrderProductionStatusCell.Create(status) },
                { "Manage", new PagedGridCell("상세 >") { CellType = PagedGridCellType.Link } }
            };
        }

        private static string CreateSizeText(DataRow row)
        {
            string specText = ToText(row["SpecText"]);
            if (!string.IsNullOrWhiteSpace(specText))
            {
                return specText;
            }

            decimal width = ToDecimal(row["WidthMm"]);
            decimal height = ToDecimal(row["HeightMm"]);
            if (width <= 0 || height <= 0)
            {
                return string.Empty;
            }

            return string.Format("{0:#,##0.##} x {1:#,##0.##}", width, height);
        }

        private static string CreateQuantityText(DataRow row)
        {
            decimal qty = ToDecimal(row["Qty"]);
            decimal areaQty = ToDecimal(row["AreaQty"]);
            string unitName = ToText(row["UnitName"]);

            if (areaQty > 0 && unitName == "회배")
            {
                return string.Format("{0:#,##0.##}", areaQty);
            }

            if (qty <= 0)
            {
                return string.Empty;
            }

            return string.Format("{0:#,##0.##}{1}", qty, unitName);
        }

        private static string ToText(object value)
        {
            return value == null || value == DBNull.Value ? string.Empty : value.ToString();
        }

        private static decimal ToDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0m;
            }

            decimal result;
            return decimal.TryParse(value.ToString(), out result) ? result : 0m;
        }

        private static int CalculatePageSize(int gridHeight)
        {
            const int headerHeight = 36;
            const int footerHeight = 52;
            const int rowHeight = 41;
            return Math.Max(1, (gridHeight - headerHeight - footerHeight) / rowHeight);
        }

        private static Point GetSearchButtonLocation(int parentWidth, int searchButtonWidth)
        {
            int x = parentWidth - ActionButtonsRightMargin - searchButtonWidth;
            return new Point(Math.Max(500, x), 153);
        }

        private Panel CreateCard(Rectangle bounds)
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Bounds = bounds;
            panel.CornerRadius = 8;
            panel.BorderWidth = 1;
            panel.BorderColor = UiTheme.PanelBorder;
            panel.FillColor = UiTheme.PanelBackground;

            return panel;
        }

        private void UpdateDateTexts()
        {
            bool isToday = _selectedDate == DateTime.Today;
            string dayTitle = isToday ? "오늘" : "선택일";

            if (_dashboardTitleLabel != null)
            {
                _dashboardTitleLabel.Text = "오늘의 현황";
            }

            if (_dashboardDateLabel != null)
            {
                _dashboardDateLabel.Text = _selectedDate.ToString("yyyy-MM-dd dddd");
            }

            if (_orderPanelTitleLabel != null)
            {
                _orderPanelTitleLabel.Text = dayTitle + " 주문";
            }

            if (_todayOrderGrid != null)
            {
                int count = CountOrders(_todayOrders);
                _todayOrderGrid.TotalText = CreateTotalText(count);
            }
        }

        private static int CountOrders(DataTable orders)
        {
            return orders == null ? 0 : orders.AsEnumerable().Select(row => row["OrderId"]).Distinct().Count();
        }

        private string CreateTotalText(int count)
        {
            string prefix = _selectedDate == DateTime.Today ? "오늘 전체" : _selectedDate.ToString("yyyy-MM-dd") + " 전체";
            return string.Format("{0} {1:#,##0}건", prefix, count);
        }

        private Label CreateLabel(string text, int x, int y, int width, int height, Color color, float size, FontStyle style)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Font = new Font("맑은 고딕", size, style, GraphicsUnit.Point, 129);
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

    }
}
