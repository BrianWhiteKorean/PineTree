using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogCustList : Form
    {
        public string SelectedCustCode { get; private set; }

        public string SelectedCustName { get; private set; }

        public DialogCustList()
        {
            InitializeComponent();
            InitEvent();
            LoadCustomers();
        }

        public DialogCustList(string customerName)
            : this()
        {
            tbCustName.Text = customerName;
            LoadCustomers();
        }

        private void InitEvent()
        {
            c1Button1.Click += delegate { LoadCustomers(); };
            btnOK.Click += btnOK_Click;
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            FGrid1.DoubleClick += delegate { SelectCurrentCustomer(); };
            FGrid1.KeyDown += FGrid1_KeyDown;
            tbCustName.KeyDown += tbCustName_KeyDown;
        }

        private void FGrid1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            SelectCurrentCustomer();
        }

        private void tbCustName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            LoadCustomers();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SelectCurrentCustomer();
        }

        private void LoadCustomers()
        {
            try
            {
                DataTable customers = CustomerRepository.GetCustomers(tbCustName.Text, string.Empty, null, null);
                PrepareGridData(customers);

                FGrid1.DataSource = customers;
                InitGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("거래처 목록을 조회하지 못했습니다.\r\n" + ex.Message, "거래처 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PrepareGridData(DataTable customers)
        {
            if (!customers.Columns.Contains("No"))
            {
                customers.Columns.Add("No", typeof(int)).SetOrdinal(0);
            }

            for (int i = 0; i < customers.Rows.Count; i++)
            {
                customers.Rows[i]["No"] = i + 1;
            }
        }

        private void InitGrid()
        {
            FGrid1.Cols.Fixed = 0;
            FGrid1.BackColor = Color.MintCream;
            FGrid1.AllowMergingFixed = AllowMergingEnum.FixedOnly;
            FGrid1.AllowSorting = AllowSortingEnum.SingleColumn;
            FGrid1.SelectionMode = SelectionModeEnum.Row;

            SetColumn("SeqNo", string.Empty, 0, TextAlignEnum.CenterCenter, false);
            SetColumn("No", "번호", 55, TextAlignEnum.CenterCenter, true);
            SetColumn("CustName", "거래처명", 210, TextAlignEnum.LeftCenter, true);
            SetColumn("CustCode", "거래처코드", 105, TextAlignEnum.CenterCenter, true);
            SetColumn("ECountCustomerCode", "이카운트 코드", 120, TextAlignEnum.CenterCenter, false);
            SetColumn("Phone", "연락처", 130, TextAlignEnum.CenterCenter, false);
            SetColumn("BusinessNum", "사업자등록번호", 130, TextAlignEnum.CenterCenter, false);
            SetColumn("RepresentativeName", "대표자", 90, TextAlignEnum.CenterCenter, false);
            SetColumn("LastOrderDate", "최종주문일", 110, TextAlignEnum.CenterCenter, true, "yyyy-MM-dd");

            FGrid1.Rows[0].TextAlign = TextAlignEnum.CenterCenter;
        }

        private void SetColumn(string columnName, string caption, int width, TextAlignEnum align, bool visible, string format = null)
        {
            if (!FGrid1.Cols.Contains(columnName))
            {
                return;
            }

            FGrid1.Cols[columnName].Caption = caption;
            FGrid1.Cols[columnName].TextAlign = align;
            FGrid1.Cols[columnName].AllowEditing = false;
            FGrid1.Cols[columnName].Visible = visible;
            FGrid1.Cols[columnName].Width = width;

            if (!string.IsNullOrWhiteSpace(format))
            {
                FGrid1.Cols[columnName].Format = format;
            }
        }

        private void SelectCurrentCustomer()
        {
            if (FGrid1.Row < FGrid1.Rows.Fixed || FGrid1.Rows.Count <= FGrid1.Rows.Fixed)
            {
                MessageBox.Show("선택할 거래처를 클릭해주세요.", "거래처 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            object custCodeValue = FGrid1[FGrid1.Row, FGrid1.Cols["CustCode"].Index];
            string custCode = custCodeValue == null ? string.Empty : custCodeValue.ToString().Trim();
            if (string.IsNullOrWhiteSpace(custCode))
            {
                MessageBox.Show("선택한 거래처의 거래처코드를 확인할 수 없습니다.", "거래처 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedCustCode = custCode;
            SelectedCustName = FGrid1[FGrid1.Row, FGrid1.Cols["CustName"].Index].ToString();
            DialogResult = DialogResult.OK;
        }

        private void DialogCustList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }
    }
}
