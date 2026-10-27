using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcOrderInfo : UserControl, INavigationView
    {
        private const string AllChannelsText = "[전체]";
        private readonly NavigationController _navigationController;
        private DataTable _orders;
        private int _loadVersion;
        private bool _isLoaded;
        private long? _selectedOrderId;

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
            InitializeOrderGrid();

            InitializeDefaultSearchCondition();
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;

        private void UcOrderInfo_Load(object sender, EventArgs e)
        {
            _isLoaded = true;
            RefreshOrderChannels();
            LoadOrders();
        }

        private void UcOrderInfo_VisibleChanged(object sender, EventArgs e)
        {
            if (_isLoaded && Visible && IsHandleCreated)
            {
                RefreshOrderChannels();
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
            OpenEditDialog();
        }

        private void OrderGrid_CellDoubleClicked(object sender, PagedGridCellClickEventArgs e)
        {
            SelectOrder(e);
            OpenEditDialog();
        }

        private void NavigationController_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigationRequested?.Invoke(this, e);
        }

        private async void LoadOrders()
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

                BindOrders(orders);
            }
            catch (Exception ex)
            {
                MessageBox.Show("주문정보를 조회하지 못했습니다.\r\n" + ex.Message, "조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeOrderGrid()
        {
            orderGrid.PageSize = Math.Max(1, (orderGrid.Height - 36 - 52) / 41);
            orderGrid.Columns.Add(new PagedGridColumn("OrderNo", "주문번호", 210, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Customer", "거래처명", 240, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Item", "품목", 260, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Size", "규격 (mm)", 215, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Quantity", "수량/회배", 150, ContentAlignment.MiddleLeft));
            orderGrid.Columns.Add(new PagedGridColumn("Status", "진행상태", 0, ContentAlignment.MiddleLeft));
            orderGrid.PageChanged += delegate { BindOrderPage(); };
            orderGrid.CellClicked += delegate(object sender, PagedGridCellClickEventArgs e) { SelectOrder(e); };
            orderGrid.SelectionChanged += OrderGrid_SelectionChanged;
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
            OpenEditDialog();
        }

        private void BindOrders(DataTable orders)
        {
            _orders = orders;
            orderGrid.TotalCount = orders.Rows.Count;
            orderGrid.PageIndex = 1;
            orderGrid.TotalText = string.Format("전체 {0:#,##0}건", orders.Rows.Count);
            BindOrderPage();
        }

        private void BindOrderPage()
        {
            _selectedOrderId = null;
            List<Dictionary<string, PagedGridCell>> rows = new List<Dictionary<string, PagedGridCell>>();
            if (_orders != null)
            {
                int startIndex = (orderGrid.PageIndex - 1) * orderGrid.PageSize;
                int endIndex = Math.Min(startIndex + orderGrid.PageSize, _orders.Rows.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    DataRow order = _orders.Rows[index];
                    Dictionary<string, PagedGridCell> row = new Dictionary<string, PagedGridCell>
                    {
                        { "OrderId", new PagedGridCell(Convert.ToString(order["OrderId"])) },
                        { "OrderNo", new PagedGridCell(Convert.ToString(order["OrderNo"])) },
                        { "Customer", new PagedGridCell(Convert.ToString(order["CustName"])) },
                        { "Item", new PagedGridCell(Convert.ToString(order["ItemName"])) },
                        { "Size", new PagedGridCell(CreateSizeText(order)) },
                        { "Quantity", new PagedGridCell(CreateQuantityText(order)) },
                        { "Status", new PagedGridCell(CreateStatusText(Convert.ToString(order["ProductionStatus"]))) { CellType = PagedGridCellType.Badge } }
                    };
                    rows.Add(row);
                }
            }
            orderGrid.SetRows(rows);
        }

        private void SelectOrder(PagedGridCellClickEventArgs e)
        {
            PagedGridCell orderIdCell;
            long orderId;
            if (e != null && e.Row != null && e.Row.TryGetValue("OrderId", out orderIdCell) && long.TryParse(orderIdCell.Text, out orderId) && orderId > 0)
            {
                _selectedOrderId = orderId;
                orderGrid.SelectedRowIndex = e.RowIndex;
            }
        }

        private void OrderGrid_SelectionChanged(object sender, EventArgs e)
        {
            _selectedOrderId = null;
            int selectedIndex = orderGrid.SelectedRowIndex;
            if (_orders == null || selectedIndex < 0)
            {
                return;
            }

            int index = (orderGrid.PageIndex - 1) * orderGrid.PageSize + selectedIndex;
            long orderId;
            if (index < _orders.Rows.Count && long.TryParse(Convert.ToString(_orders.Rows[index]["OrderId"]), out orderId))
            {
                _selectedOrderId = orderId;
            }
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

        private void OpenEditDialog()
        {
            long orderId;
            if (!TryGetCurrentOrderId(out orderId))
            {
                MessageBox.Show("수정할 주문을 선택해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (DialogOrder dialog = new DialogOrder(orderId))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadOrders();
                }
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

        private static string CreateStatusText(string status)
        {
            string normalized = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().Replace(" ", string.Empty);
            switch (normalized)
            {
                case "출력중":
                    return "출력 중";
                case "포장대기":
                    return "포장 대기";
                case "포장완료":
                    return "포장 완료";
                case "신규":
                    return "미작업";
                default:
                    return string.IsNullOrWhiteSpace(status) ? "미작업" : status.Trim();
            }
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

        private bool TryGetCurrentOrderId(out long orderId)
        {
            orderId = _selectedOrderId.GetValueOrDefault();
            return _selectedOrderId.HasValue && orderId > 0;
        }
    }
}
