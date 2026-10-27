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
    public partial class UcCustInfo : UserControl, INavigationView
    {
        private readonly NavigationController _navigationController;
        private DataTable _customers;
        private long? _selectedCustomerSeqNo;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;


                return cp;
            }
        }

        public UcCustInfo()
        {
            InitializeComponent();

            _navigationController = new NavigationController(picBackground);
            _navigationController.NavigationRequested += NavigationController_NavigationRequested;
            Load += UcCustInfo_Load;
            btnSelect.Click += btnSelect_Click;
            tbCustName.KeyDown += SearchTextBox_KeyDown;
            tbCustCode.KeyDown += SearchTextBox_KeyDown;
            btnAdd.Click += btnAdd_Click;
            btnEdit.Click += btnEdit_Click;
            InitializeCustomerGrid();
            chkRecentOrder.CheckedChanged += chkRecentOrder_CheckedChanged;

            InitializeDefaultSearchCondition();
            ApplyRecentOrderConditionEnabled();
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;

        private void UcCustInfo_Load(object sender, EventArgs e)
        {
            LoadCustomers();
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            LoadCustomers();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            LoadCustomers();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (DialogCustInfo dialog = new DialogCustInfo())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadCustomers();
                }
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            OpenEditDialog();
        }

        private void CustomerGrid_CellDoubleClicked(object sender, PagedGridCellClickEventArgs e)
        {
            SelectCustomer(e);
            OpenEditDialog();
        }

        private void NavigationController_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigationRequested?.Invoke(this, e);
        }

        private async void LoadCustomers()
        {
            try
            {
                string customerName = tbCustName.Text;
                string customerCode = tbCustCode.Text;
                DateTime? lastOrderDateFrom = chkRecentOrder.Checked ? GetDateValue(dtmFrom.Value) : null;
                DateTime? lastOrderDateTo = chkRecentOrder.Checked ? GetDateValue(dtmTo.Value) : null;

                DataTable customers = await Task.Run(() => CustomerRepository.GetCustomers(
                    customerName,
                    customerCode,
                    lastOrderDateFrom,
                    lastOrderDateTo));

                if (IsDisposed)
                {
                    return;
                }

                BindCustomers(customers);
            }
            catch (Exception ex)
            {
                MessageBox.Show("고객정보를 조회하지 못했습니다.\r\n" + ex.Message, "조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeCustomerGrid()
        {
            customerGrid.PageSize = Math.Max(1, (customerGrid.Height - 36 - 52) / 41);
            customerGrid.Columns.Add(new PagedGridColumn("No", "번호", 60, ContentAlignment.MiddleCenter));
            customerGrid.Columns.Add(new PagedGridColumn("CustName", "거래처명", 0, ContentAlignment.MiddleLeft));
            customerGrid.Columns.Add(new PagedGridColumn("CustCode", "거래처코드", 110, ContentAlignment.MiddleCenter));
            customerGrid.Columns.Add(new PagedGridColumn("ECountCustomerCode", "이카운트 코드", 150, ContentAlignment.MiddleCenter));
            customerGrid.Columns.Add(new PagedGridColumn("Phone", "연락처", 150, ContentAlignment.MiddleCenter));
            customerGrid.Columns.Add(new PagedGridColumn("BusinessNum", "사업자등록번호", 140, ContentAlignment.MiddleCenter));
            customerGrid.Columns.Add(new PagedGridColumn("RepresentativeName", "대표자", 120, ContentAlignment.MiddleCenter));
            customerGrid.Columns.Add(new PagedGridColumn("LastOrderDate", "최종주문일", 120, ContentAlignment.MiddleCenter));
            customerGrid.PageChanged += delegate { BindCustomerPage(); };
            customerGrid.CellClicked += delegate(object sender, PagedGridCellClickEventArgs e) { SelectCustomer(e); };
            customerGrid.SelectionChanged += CustomerGrid_SelectionChanged;
            customerGrid.CellDoubleClicked += CustomerGrid_CellDoubleClicked;
            customerGrid.KeyDown += CustomerGrid_KeyDown;
            customerGrid.Resize += delegate
            {
                customerGrid.PageSize = Math.Max(1, (customerGrid.Height - 36 - 52) / 41);
                customerGrid.PageIndex = customerGrid.PageIndex;
                BindCustomerPage();
            };
        }

        private void CustomerGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || e.Modifiers != Keys.None || customerGrid.SelectedRowIndex < 0)
            {
                return;
            }

            e.SuppressKeyPress = true;
            OpenEditDialog();
        }

        private void BindCustomers(DataTable customers)
        {
            _customers = customers;
            customerGrid.TotalCount = customers.Rows.Count;
            customerGrid.PageIndex = 1;
            customerGrid.TotalText = string.Format("전체 {0:#,##0}건", customers.Rows.Count);
            BindCustomerPage();
        }

        private void BindCustomerPage()
        {
            _selectedCustomerSeqNo = null;
            List<Dictionary<string, PagedGridCell>> rows = new List<Dictionary<string, PagedGridCell>>();
            if (_customers != null)
            {
                int startIndex = (customerGrid.PageIndex - 1) * customerGrid.PageSize;
                int endIndex = Math.Min(startIndex + customerGrid.PageSize, _customers.Rows.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    DataRow customer = _customers.Rows[index];
                    Dictionary<string, PagedGridCell> row = new Dictionary<string, PagedGridCell>
                    {
                        { "SeqNo", new PagedGridCell(Convert.ToString(customer["SeqNo"])) },
                        { "No", new PagedGridCell((index + 1).ToString()) }
                    };
                    foreach (PagedGridColumn column in customerGrid.Columns)
                    {
                        if (column.Key == "No")
                        {
                            continue;
                        }

                        object value = customer[column.Key];
                        string text = value is DateTime
                            ? ((DateTime)value).ToString("yyyy-MM-dd")
                            : Convert.ToString(value);
                        row.Add(column.Key, new PagedGridCell(text));
                    }
                    rows.Add(row);
                }
            }
            customerGrid.SetRows(rows);
        }

        private void SelectCustomer(PagedGridCellClickEventArgs e)
        {
            PagedGridCell seqNoCell;
            long seqNo;
            if (e.Row.TryGetValue("SeqNo", out seqNoCell) && long.TryParse(seqNoCell.Text, out seqNo))
            {
                _selectedCustomerSeqNo = seqNo;
                customerGrid.SelectedRowIndex = e.RowIndex;
            }
        }

        private void CustomerGrid_SelectionChanged(object sender, EventArgs e)
        {
            _selectedCustomerSeqNo = null;
            int selectedIndex = customerGrid.SelectedRowIndex;
            if (_customers == null || selectedIndex < 0)
            {
                return;
            }

            int index = (customerGrid.PageIndex - 1) * customerGrid.PageSize + selectedIndex;
            long seqNo;
            if (index < _customers.Rows.Count && long.TryParse(Convert.ToString(_customers.Rows[index]["SeqNo"]), out seqNo))
            {
                _selectedCustomerSeqNo = seqNo;
            }
        }

        private void InitializeDefaultSearchCondition()
        {
            DateTime today = DateTime.Today;
            dtmFrom.Value = today.AddMonths(-1);
            dtmTo.Value = today;
            chkRecentOrder.Checked = false;
        }

        private void chkRecentOrder_CheckedChanged(object sender, EventArgs e)
        {
            ApplyRecentOrderConditionEnabled();
        }

        private void ApplyRecentOrderConditionEnabled()
        {
            dtmFrom.Enabled = chkRecentOrder.Checked;
            dtmTo.Enabled = chkRecentOrder.Checked;
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
            long seqNo;
            if (!TryGetCurrentSeqNo(out seqNo))
            {
                MessageBox.Show("수정할 고객을 선택해주세요.", "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (DialogCustInfo dialog = new DialogCustInfo(seqNo))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadCustomers();
                }
            }
        }

        private bool TryGetCurrentSeqNo(out long seqNo)
        {
            seqNo = _selectedCustomerSeqNo.GetValueOrDefault();
            return _selectedCustomerSeqNo.HasValue;
        }
    }
}
