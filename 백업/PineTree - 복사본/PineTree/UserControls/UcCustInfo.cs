using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcCustInfo : UserControl, INavigationView
    {
        private readonly NavigationController _navigationController;

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
            btnAdd.Click += btnAdd_Click;
            btnEdit.Click += btnEdit_Click;
            FGrid1.DoubleClick += FGrid1_DoubleClick;
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

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (DailogCustInfo dialog = new DailogCustInfo())
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

        private void FGrid1_DoubleClick(object sender, EventArgs e)
        {
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

        private void BindCustomers(DataTable customers)
        {
            PrepareGridData(customers);

            FGrid1.DataSource = customers;
            InitGrid();

            labelCount.Text = string.Format("조회건수 : {0}건", customers.Rows.Count);
        }

        private void InitGrid()
        {
            FGrid1.Cols.Fixed = 0;
            FGrid1.BackColor = Color.MintCream;
            FGrid1.AllowMergingFixed = AllowMergingEnum.FixedOnly;
            FGrid1.AllowSorting = AllowSortingEnum.SingleColumn;
            FGrid1.SelectionMode = SelectionModeEnum.Row;

            FGrid1.Cols["SeqNo"].Visible = false;

            FGrid1.Cols["IsSelected"].Caption = "선택";
            FGrid1.Cols["IsSelected"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["IsSelected"].AllowEditing = true;
            FGrid1.Cols["IsSelected"].Width = 55;

            FGrid1.Cols["No"].Caption = "번호";
            FGrid1.Cols["No"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["No"].AllowEditing = false;
            FGrid1.Cols["No"].Width = 60;

            FGrid1.Cols["CustomerName"].Caption = "거래처명";
            FGrid1.Cols["CustomerName"].TextAlign = TextAlignEnum.LeftCenter;
            FGrid1.Cols["CustomerName"].AllowEditing = false;
            FGrid1.Cols["CustomerName"].Width = 220;

            FGrid1.Cols["CustCode"].Caption = "거래처코드";
            FGrid1.Cols["CustCode"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["CustCode"].AllowEditing = false;
            FGrid1.Cols["CustCode"].Width = 110;

            FGrid1.Cols["ECountCustomerCode"].Caption = "이카운트 코드";
            FGrid1.Cols["ECountCustomerCode"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["ECountCustomerCode"].AllowEditing = false;
            FGrid1.Cols["ECountCustomerCode"].Width = 150;

            FGrid1.Cols["Phone"].Caption = "연락처";
            FGrid1.Cols["Phone"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["Phone"].AllowEditing = false;
            FGrid1.Cols["Phone"].Width = 150;

            FGrid1.Cols["BusinessNum"].Caption = "사업자등록번호";
            FGrid1.Cols["BusinessNum"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["BusinessNum"].AllowEditing = false;
            FGrid1.Cols["BusinessNum"].Width = 140;

            FGrid1.Cols["RepresentativeName"].Caption = "대표자";
            FGrid1.Cols["RepresentativeName"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["RepresentativeName"].AllowEditing = false;
            FGrid1.Cols["RepresentativeName"].Width = 120;

            FGrid1.Cols["LastOrderDate"].Caption = "최종주문일";
            FGrid1.Cols["LastOrderDate"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["LastOrderDate"].AllowEditing = false;
            FGrid1.Cols["LastOrderDate"].Width = 120;
            FGrid1.Cols["LastOrderDate"].Format = "yyyy-MM-dd";

            FGrid1.Rows[0].TextAlign = TextAlignEnum.CenterCenter;
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

        private void PrepareGridData(DataTable customers)
        {
            if (!customers.Columns.Contains("IsSelected"))
            {
                customers.Columns.Add("IsSelected", typeof(bool)).SetOrdinal(0);
            }

            if (!customers.Columns.Contains("No"))
            {
                customers.Columns.Add("No", typeof(int)).SetOrdinal(1);
            }

            for (int i = 0; i < customers.Rows.Count; i++)
            {
                customers.Rows[i]["IsSelected"] = false;
                customers.Rows[i]["No"] = i + 1;
            }
        }

        private void OpenEditDialog()
        {
            long seqNo;
            if (!TryGetCurrentSeqNo(out seqNo))
            {
                MessageBox.Show("수정할 고객을 선택해주세요.", "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (DailogCustInfo dialog = new DailogCustInfo(seqNo))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadCustomers();
                }
            }
        }

        private bool TryGetCurrentSeqNo(out long seqNo)
        {
            seqNo = 0;

            if (FGrid1.Row < FGrid1.Rows.Fixed || FGrid1.Rows.Count <= FGrid1.Rows.Fixed)
            {
                return false;
            }

            int seqNoCol = FGrid1.Cols["SeqNo"].Index;
            object value = FGrid1[FGrid1.Row, seqNoCol];
            return value != null && long.TryParse(value.ToString(), out seqNo);
        }
    }
}
