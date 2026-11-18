using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogOrderList : Form
    {
        private int _requestVersion;
        public long? SelectedOrderId { get; private set; }
        public string SelectedCustCode { get; private set; }
        public string SelectedCustName { get; private set; }
        public DialogOrderList() : this(string.Empty) { }
        public DialogOrderList(string customerName)
        {
            InitializeComponent();
            Text = "연결할 주문 선택";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            dtmDate.Value = DateTime.Today;
            tbCustName.Text = customerName;
            btnOK.Enabled = false;
            c1Button1.Click += async delegate { await LoadOrdersAsync(); };
            btnOK.Click += delegate { SelectCurrentOrder(); };
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            FGrid1.DoubleClick += delegate { if (btnOK.Enabled) SelectCurrentOrder(); };
            FGrid1.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (btnOK.Enabled) SelectCurrentOrder(); }
            };
            tbCustName.KeyDown += async delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadOrdersAsync(); }
            };
            Shown += async delegate { await LoadOrdersAsync(); };
        }
        private async Task LoadOrdersAsync()
        {
            int version = ++_requestVersion;
            DateTime date;
            if (!DateTime.TryParse(Convert.ToString(dtmDate.Value), out date)) date = DateTime.Today;
            string name = tbCustName.Text;
            btnOK.Enabled = false; UseWaitCursor = true;
            try
            {
                DataTable rows = await Task.Run(() => OrderRepository.GetOrderLinkCandidates(date, name));
                if (IsDisposed || version != _requestVersion) { rows.Dispose(); return; }
                BindOrders(rows);
            }
            catch (Exception ex)
            {
                if (!IsDisposed && version == _requestVersion)
                {
                    FGrid1.DataSource = null;
                    MessageBox.Show(this, "주문 목록을 조회하지 못했습니다.\r\n" + ex.Message, "주문 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally { if (!IsDisposed && version == _requestVersion) UseWaitCursor = false; }
        }
        private void BindOrders(DataTable rows)
        {
            var previous = FGrid1.DataSource as DataTable;
            FGrid1.DataSource = rows;
            if (previous != null && previous != rows) previous.Dispose();
            FGrid1.Cols.Fixed = 0;
            FGrid1.AllowEditing = false;
            FGrid1.AllowSorting = AllowSortingEnum.SingleColumn;
            FGrid1.SelectionMode = SelectionModeEnum.Row;
            FGrid1.BackColor = Color.MintCream;
            foreach (Column col in FGrid1.Cols) col.Visible = false;
            SetColumn("OrderNo", "주문번호", 145);
            SetColumn("OrderDate", "주문일", 90, "yyyy-MM-dd");
            SetColumn("CustName", "거래처", 160);
            SetColumn("OrderTitle", "품목명", 200);
            SetColumn("RequestedDueAt", "납기", 90, "yyyy-MM-dd");
            btnOK.Enabled = rows.Rows.Count > 0;
        }
        private void SetColumn(string name, string caption, int width, string format = null)
        {
            var col = FGrid1.Cols[name]; col.Visible = true; col.Caption = caption;
            col.Width = width; if (format != null) col.Format = format;
        }
        private void SelectCurrentOrder()
        {
            if (FGrid1.Row < FGrid1.Rows.Fixed || FGrid1.Row >= FGrid1.Rows.Count) return;
            var view = FGrid1.Rows[FGrid1.Row].DataSource as DataRowView;
            if (view == null) return;
            SelectedOrderId = Convert.ToInt64(view.Row["OrderId"]);
            SelectedCustCode = Convert.ToString(view.Row["CustCode"]);
            SelectedCustName = Convert.ToString(view.Row["CustName"]);
            DialogResult = DialogResult.OK;
        }
        private void DialogOrderList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        }
    }
}
