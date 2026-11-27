using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcOrderInfo : UserControl, INavigationView
    {
        private const string AllChannelsText = "[전체]";
        private const string CheckColumn = "Checked";
        private const string WorkSheetColumn = "WorkSheet";
        private readonly NavigationController _navigationController;
        private readonly HashSet<long> _checkedOrderIds = new HashSet<long>();
        private DataTable _orders;
        private int _loadVersion;
        private bool _isLoaded;
        private bool _deleting;
        private bool _retryingFileCleanup;
        private bool _openingWorkOrder;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;

                return cp;
            }
        }

        public UcOrderInfo()
        {
            InitializeComponent();

            _navigationController = new NavigationController(picBackground);
            _navigationController.NavigationRequested += NavigationController_NavigationRequested;
            Load += UcOrderInfo_Load;
            VisibleChanged += UcOrderInfo_VisibleChanged;
            cbChannel.BeforeDropDownOpen += delegate { RefreshOrderChannels(); };
            cbChannel.Items.Add(AllChannelsText);
            cbChannel.SelectedIndex = 0;
            btnSelect.Click += btnSelect_Click;
            tbCustName.KeyDown += SearchTextBox_KeyDown;
            tbCustCode.KeyDown += SearchTextBox_KeyDown;
            btnAdd.Click += btnAdd_Click;
            btnEdit.Click += btnEdit_Click;
            btnDelete.Click += btnDelete_Click;
            btnClearSelection.Click += btnClearSelection_Click;
            InitializeOrderGrid();

            InitializeDefaultSearchCondition();
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;
        internal event EventHandler ProductionStatusChanged;

        internal void RefreshOrders()
        {
            if (_isLoaded) LoadOrders(orderGrid.PageIndex);
        }

        private void UcOrderInfo_Load(object sender, EventArgs e)
        {
            _isLoaded = true;
            RefreshOrderChannels();
            LoadOrders();
            RetryFileCleanup();
        }

        private void UcOrderInfo_VisibleChanged(object sender, EventArgs e)
        {
            if (_isLoaded && Visible && IsHandleCreated)
            {
                RefreshOrderChannels();
                RetryFileCleanup();
            }
        }

        private void RefreshOrderChannels()
        {
            try
            {
                DataTable channels = CommonCodeRepository.GetItems("주문채널");
                string selectedChannel = cbChannel.SelectedIndex > 0 ? cbChannel.Text : null;
                List<string> items = new List<string> { AllChannelsText };
                foreach (DataRow row in channels.Rows)
                {
                    string name = Convert.ToString(row["ItemName"]).Trim();
                    if (!string.IsNullOrEmpty(name) && !items.Contains(name))
                    {
                        items.Add(name);
                    }
                }

                cbChannel.Items.Clear();
                foreach (string item in items)
                {
                    cbChannel.Items.Add(item);
                }
                int selectedIndex = selectedChannel == null ? 0 : items.IndexOf(selectedChannel);
                cbChannel.SelectedIndex = Math.Max(0, selectedIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show("주문채널을 불러오지 못했습니다.\r\n" + ex.Message, "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            LoadOrders();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            LoadOrders();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (DialogOrder dialog = new DialogOrder())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadOrders();
                }
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (_checkedOrderIds.Count != 1)
            {
                string message = _checkedOrderIds.Count == 0
                    ? "수정할 주문을 선택해주세요."
                    : "수정할 주문은 한 건만 선택해주세요.";
                MessageBox.Show(this, message, "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenEditDialog(_checkedOrderIds.Single());
        }

        private async void btnDelete_Click(object sender, EventArgs e)
        {
            if (_deleting) return;
            long[] orderIds = _checkedOrderIds.ToArray();
            if (orderIds.Length == 0)
            {
                MessageBox.Show(this, "삭제할 대상을 선택해주세요", "주문 삭제", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this, $"체크한 주문 {orderIds.Length}건과 해당 서버 파일을 정말 삭제하시겠습니까?\r\n"
                + "다른 주문에서 사용하는 파일은 보존됩니다.", "주문 삭제", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            int pageIndex = orderGrid.PageIndex;
            _deleting = true;
            btnDelete.Enabled = btnEdit.Enabled = btnAdd.Enabled = btnSelect.Enabled = btnClearSelection.Enabled = orderGrid.Enabled = false;
            ++_loadVersion;
            try
            {
                await Task.Run(() => OrderRepository.DeleteOrders(orderIds));
                if (IsDisposed) return;
                _checkedOrderIds.ExceptWith(orderIds);
                if (_orders != null)
                {
                    var deletedIds = new HashSet<long>(orderIds);
                    foreach (DataRow row in _orders.Rows.Cast<DataRow>().Where(row => deletedIds.Contains(GetOrderId(row))).ToArray())
                        _orders.Rows.Remove(row);
                    BindOrders(_orders, pageIndex);
                }
                LoadOrders(pageIndex);
                await Task.Run(() => OrderRepository.CleanUpDeletedOrderFiles());
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show(this, ex.Message, "주문 삭제", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _deleting = false;
                if (!IsDisposed)
                    btnDelete.Enabled = btnEdit.Enabled = btnAdd.Enabled = btnSelect.Enabled = btnClearSelection.Enabled = orderGrid.Enabled = true;
            }
        }

        private async void RetryFileCleanup()
        {
            if (_deleting || _retryingFileCleanup) return;
            _retryingFileCleanup = true;
            try
            {
                await Task.Run(() => OrderRepository.CleanUpDeletedOrderFiles());
            }
            catch (Exception ex)
            {
                if (!IsDisposed && Visible)
                    MessageBox.Show(this, ex.Message, "주문 파일 삭제", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _retryingFileCleanup = false;
            }
        }

        private void OrderGrid_HeaderClicked(object sender, PagedGridCellClickEventArgs e)
        {
            if (e.ColumnKey != CheckColumn || _orders == null) return;
            var orderIds = _orders.AsEnumerable().Skip(CurrentPageStartIndex).Take(orderGrid.PageSize)
                .Select(GetOrderId).Distinct().ToArray();
            if (orderIds.All(_checkedOrderIds.Contains))
                _checkedOrderIds.ExceptWith(orderIds);
            else
                _checkedOrderIds.UnionWith(orderIds);
            BindOrderPage();
        }

        private void btnClearSelection_Click(object sender, EventArgs e)
        {
            _checkedOrderIds.Clear();
            BindOrderPage();
        }

        private int CurrentPageStartIndex => (orderGrid.PageIndex - 1) * orderGrid.PageSize;

        private void OrderGrid_CellDoubleClicked(object sender, PagedGridCellClickEventArgs e)
        {
            if (e.ColumnKey != WorkSheetColumn && e.ColumnKey != CheckColumn && e.ColumnKey != OrderProductionStatusCell.ColumnKey)
                OpenEditDialog(long.Parse(e.Row["OrderId"].Text));
        }

        private async void OrderGrid_CellClicked(object sender, PagedGridCellClickEventArgs e)
        {
            orderGrid.SelectedRowIndex = e.RowIndex;
            long orderId = long.Parse(e.Row["OrderId"].Text);
            if (e.ColumnKey == OrderProductionStatusCell.ColumnKey)
            {
                if (await OrderProductionStatusCell.FeedAsync(this, orderId, e.Row[e.ColumnKey]))
                {
                    RefreshOrders();
                    ProductionStatusChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            if (e.ColumnKey == CheckColumn && e.Row[CheckColumn].CellType == PagedGridCellType.CheckBox)
            {
                if (e.Row[CheckColumn].Checked)
                    _checkedOrderIds.Add(orderId);
                else
                    _checkedOrderIds.Remove(orderId);
                UpdateGridCount();
                return;
            }

            if (e.ColumnKey != WorkSheetColumn || e.Row[WorkSheetColumn].CellType != PagedGridCellType.Button
                || _openingWorkOrder)
                return;

            _openingWorkOrder = true;
            try
            {
                await DialogWorkOrder.ShowSavedOrderAsync(this, orderId);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show(this, "작업지시서를 열 수 없습니다.\r\n" + ex.Message,
                        "작업지시서", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _openingWorkOrder = false;
            }
        }

        private void NavigationController_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigationRequested?.Invoke(this, e);
        }

        private async void LoadOrders(int pageIndex = 1)
        {
            try
            {
                string customerName = tbCustName.Text;
                string customerCode = tbCustCode.Text;
                string orderChannel = cbChannel.SelectedIndex > 0 ? cbChannel.Text : null;
                DateTime? orderDateFrom = GetDateValue(dtmFrom.Value);
                DateTime? orderDateTo = GetDateValue(dtmTo.Value);
                if (!orderDateFrom.HasValue || !orderDateTo.HasValue || orderDateFrom > orderDateTo)
                {
                    MessageBox.Show("조회 기간을 올바르게 입력해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int requestVersion = ++_loadVersion;

                DataTable orders = await Task.Run(() => OrderRepository.GetOrders(
                    customerName,
                    customerCode,
                    orderDateFrom.Value,
                    orderDateTo.Value,
                    orderChannel));

                if (IsDisposed || requestVersion != _loadVersion)
                {
                    return;
                }

                BindOrders(orders, pageIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show("주문정보를 조회하지 못했습니다.\r\n" + ex.Message, "조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeOrderGrid()
        {
            orderGrid.RowGroupKey = "OrderId";
            orderGrid.PageSize = Math.Max(1, (orderGrid.Height - 36 - 52) / 41);
            orderGrid.Columns.Add(new PagedGridColumn(CheckColumn, "선택", 56, ContentAlignment.MiddleCenter));
            orderGrid.Columns.Add(new PagedGridColumn("OrderNo", "주문번호", 210, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Customer", "거래처명", 240, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Item", "품목", 260, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Size", "규격", 215, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Quantity", "수량/회배", 150, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("ConsigneeName", "주문자", 120, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn(OrderProductionStatusCell.ColumnKey, "진행상태", 0, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn(WorkSheetColumn, "작업서", 100, ContentAlignment.MiddleCenter));
            orderGrid.PageChanged += delegate { BindOrderPage(); };
            orderGrid.HeaderClicked += OrderGrid_HeaderClicked;
            orderGrid.CellClicked += OrderGrid_CellClicked;
            orderGrid.CellDoubleClicked += OrderGrid_CellDoubleClicked;
            orderGrid.KeyDown += OrderGrid_KeyDown;
            orderGrid.Resize += delegate
            {
                orderGrid.PageSize = Math.Max(1, (orderGrid.Height - 36 - 52) / 41);
                orderGrid.PageIndex = orderGrid.PageIndex;
                BindOrderPage();
            };
        }

        private void OrderGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || e.Modifiers != Keys.None || orderGrid.SelectedRowIndex < 0)
            {
                return;
            }

            e.SuppressKeyPress = true;
            int index = CurrentPageStartIndex + orderGrid.SelectedRowIndex;
            OpenEditDialog(GetOrderId(_orders.Rows[index]));
        }

        private void BindOrders(DataTable orders, int pageIndex)
        {
            _orders = orders;
            orderGrid.TotalCount = orders.Rows.Count;
            orderGrid.PageIndex = pageIndex;
            BindOrderPage();
        }

        private void UpdateGridCount()
        {
            var orderIds = new HashSet<long>(_orders.AsEnumerable().Select(GetOrderId));
            int hiddenCount = _checkedOrderIds.Count(id => !orderIds.Contains(id));
            string text = $"전체 {orderIds.Count:N0}건 / 품목 {_orders.Rows.Count:N0}개 / 선택 {_checkedOrderIds.Count:N0}건";
            orderGrid.TotalText = hiddenCount > 0 ? $"{text} (조회 밖 {hiddenCount:N0}건)" : text;
        }

        private static long GetOrderId(DataRow row)
        {
            return Convert.ToInt64(row["OrderId"]);
        }

        private static string GetOrderItemDisplayName(DataRow row, bool firstItem)
        {
            string title = row.Table.Columns.Contains("OrderTitle") ? Convert.ToString(row["OrderTitle"]) : string.Empty;
            if (firstItem && !string.IsNullOrWhiteSpace(title)) return title;
            string customName = row.Table.Columns.Contains("CustomName") ? Convert.ToString(row["CustomName"]) : string.Empty;
            return string.IsNullOrWhiteSpace(customName) ? Convert.ToString(row["ItemName"]) : customName;
        }

        private void BindOrderPage()
        {
            List<Dictionary<string, PagedGridCell>> rows = new List<Dictionary<string, PagedGridCell>>();
            if (_orders != null)
            {
                UpdateGridCount();
                int startIndex = CurrentPageStartIndex;
                int endIndex = Math.Min(startIndex + orderGrid.PageSize, _orders.Rows.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    DataRow order = _orders.Rows[index];
                    long orderId = GetOrderId(order);
                    bool firstItem = index == 0 || !Equals(order["OrderId"], _orders.Rows[index - 1]["OrderId"]);
                    bool firstOnPage = firstItem || index == startIndex;
                    Dictionary<string, PagedGridCell> row = new Dictionary<string, PagedGridCell>
                    {
                        { CheckColumn, firstOnPage ? new PagedGridCell(string.Empty)
                            { CellType = PagedGridCellType.CheckBox, Checked = _checkedOrderIds.Contains(orderId) } : new PagedGridCell(string.Empty) },
                        { "OrderId", new PagedGridCell(orderId.ToString()) },
                        { "OrderNo", new PagedGridCell(firstOnPage ? Convert.ToString(order["OrderNo"]) : string.Empty) },
                        { "Customer", new PagedGridCell(Convert.ToString(order["CustName"])) },
                        { "Item", new PagedGridCell(GetOrderItemDisplayName(order, firstItem)) },
                        { "Size", new PagedGridCell(CreateSizeText(order)) },
                        { "Quantity", new PagedGridCell(CreateQuantityText(order)) },
                        { "ConsigneeName", new PagedGridCell(firstOnPage ? Convert.ToString(order["ConsigneeName"]) : string.Empty) },
                        { OrderProductionStatusCell.ColumnKey, OrderProductionStatusCell.Create(Convert.ToString(order["ProductionStatus"])) },
                        { WorkSheetColumn, firstOnPage ? new PagedGridCell("보기") { CellType = PagedGridCellType.Button } : new PagedGridCell(string.Empty) }
                    };
                    rows.Add(row);
                }
            }
            orderGrid.SetRows(rows);
        }

        private void InitializeDefaultSearchCondition()
        {
            DateTime today = DateTime.Today;
            dtmFrom.Value = today.AddMonths(-1);
            dtmTo.Value = today;
        }

        private DateTime? GetDateValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            DateTime date;
            if (DateTime.TryParse(value.ToString(), out date))
            {
                return date.Date;
            }

            return null;
        }

        private void OpenEditDialog(long orderId)
        {
            try
            {
                using (var dialog = new DialogOrderDesignPad(orderId))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        LoadOrders();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "수정할 주문을 열 수 없습니다.\r\n" + ex.Message,
                    "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

    }
}
