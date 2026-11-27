using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogPriceItemListExclusive : Form
    {
        private const string AllText = "[전체]";
        private readonly string _custCode = string.Empty;
        private readonly string _custName = string.Empty;

        public long SelectedPriceItemSeqNo { get; private set; }

        public long? SelectedPriceItemPriceId { get; private set; }

        public string SelectedItemCode { get; private set; }

        public string SelectedPriceItemName { get; private set; }

        public string SelectedSpecText { get; private set; }

        public string SelectedBillingBasis { get; private set; }

        public string SelectedUnitName { get; private set; }

        public string SelectedPriceName { get; private set; }

        public decimal SelectedUnitPrice { get; private set; }

        public string SelectedCustCode { get; private set; }
        public string SelectedPriceCategory { get; private set; }
        public string SelectedCategoryName { get; private set; }
        public string SelectedApplyFrom { get; private set; }

        public DialogPriceItemListExclusive() : this(string.Empty, string.Empty, string.Empty)
        {
        }

        public DialogPriceItemListExclusive(string priceItemName, string custCode, string custName)
        {
            InitializeComponent();
            _custCode = string.IsNullOrWhiteSpace(custCode) ? string.Empty : custCode.Trim();
            _custName = string.IsNullOrWhiteSpace(custName) ? string.Empty : custName.Trim();
            Text = "전용품목 선택";
            InitEvent();
            tbCustName.Text = _custName;
            tbItemName.Text = priceItemName;

            LoadCategoryCombo(AllText);
            LoadPriceItems();
        }

        private void InitEvent()
        {
            c1Button1.Click += delegate { LoadPriceItems(); };
            btnNewProduct.Click += btnNewProduct_Click;
            btnOK.Click += btnOK_Click;
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            FGrid1.DoubleClick += delegate { ConfirmSelectedPriceItem(); };
            FGrid1.KeyDown += FGrid1_KeyDown;
            tbItemName.KeyDown += tbCustName_KeyDown;
        }

        private void FGrid1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            ConfirmSelectedPriceItem();
        }

        private void tbCustName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            LoadPriceItems();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            ConfirmSelectedPriceItem();
        }

        private void btnNewProduct_Click(object sender, EventArgs e)
        {
            using (DialogPriceItem dialog = new DialogPriceItem())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string selectedCategory = cbCategory.Text;
                LoadCategoryCombo(selectedCategory);
                LoadPriceItems();
            }
        }

        private void LoadPriceItems()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_custCode))
                {
                    FGrid1.DataSource = null;
                    return;
                }

                string priceCategory = GetSelectedValue(cbCategory);
                DataTable priceItems = PriceItemRepository.GetPriceItems(priceCategory, string.Empty, tbItemName.Text, _custCode, true);
                PrepareGridData(priceItems);

                FGrid1.DataSource = priceItems;
                InitGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("품목 목록을 조회하지 못했습니다.\r\n" + ex.Message, "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadCategoryCombo(string selectedText)
        {
            cbCategory.Items.Clear();
            cbCategory.Items.Add(AllText);

            DataTable categories = PriceItemRepository.GetPriceCategories();
            foreach (DataRow row in categories.Rows)
            {
                cbCategory.Items.Add(row["PriceCategory"].ToString());
            }

            SelectComboItem(cbCategory, selectedText);
        }

        private string GetSelectedValue(C1.Win.C1Input.C1ComboBox comboBox)
        {
            string value = comboBox.Text.Trim();
            if (value == AllText)
            {
                return string.Empty;
            }

            return value;
        }

        private void SelectComboItem(C1.Win.C1Input.C1ComboBox comboBox, string selectedText)
        {
            string targetText = string.IsNullOrWhiteSpace(selectedText) ? AllText : selectedText.Trim();

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                object item = comboBox.Items[i];
                if (item != null && string.Equals(item.ToString(), targetText, StringComparison.Ordinal))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
        }

        private void PrepareGridData(DataTable priceItems)
        {
            if (!priceItems.Columns.Contains("No"))
            {
                priceItems.Columns.Add("No", typeof(int)).SetOrdinal(0);
            }

            for (int i = 0; i < priceItems.Rows.Count; i++)
            {
                priceItems.Rows[i]["No"] = i + 1;
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
            SetColumn("PriceItemPriceId", string.Empty, 0, TextAlignEnum.CenterCenter, false);
            SetColumn("No", "번호", 55, TextAlignEnum.CenterCenter, true);
            SetColumn("PriceCategory", "카테고리", 120, TextAlignEnum.CenterCenter, true);
            SetColumn("CategoryName", "품목분류", 130, TextAlignEnum.CenterCenter, true);
            SetColumn("PriceItemName", "품목명", 210, TextAlignEnum.LeftCenter, true);
            SetColumn("ItemCode", "품목코드", 100, TextAlignEnum.CenterCenter, true);
            SetColumn("SpecText", "규격", 120, TextAlignEnum.LeftCenter, true);
            SetColumn("UnitName", "단위", 70, TextAlignEnum.CenterCenter, true);
            SetColumn("BillingBasis", "과금기준", 85, TextAlignEnum.CenterCenter, true);
            SetColumn("PriceName", "현재 단가표", 150, TextAlignEnum.LeftCenter, true);
            SetColumn("CurrentUnitPrice", "현재판매가", 100, TextAlignEnum.RightCenter, true, "#,##0.##");
            SetColumn("ApplyFrom", "단가적용일", 105, TextAlignEnum.CenterCenter, true, "yyyy-MM-dd");
            SetColumn("CustCode", "전용 거래처코드", 115, TextAlignEnum.CenterCenter, false);
            SetColumn("SpecialCustName", "전용 거래처", 110, TextAlignEnum.LeftCenter, true);
            SetColumn("Remark", "비고", 160, TextAlignEnum.LeftCenter, true);

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

        private void ConfirmSelectedPriceItem()
        {
            if (FGrid1.Row < FGrid1.Rows.Fixed || FGrid1.Rows.Count <= FGrid1.Rows.Fixed)
            {
                MessageBox.Show("선택할 품목을 클릭해주세요.", "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            long seqNo;
            if (!TryGetLong("SeqNo", out seqNo) || seqNo <= 0)
            {
                MessageBox.Show("선택한 품목의 순번을 확인할 수 없습니다.", "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }

            SelectedPriceItemSeqNo = seqNo;
            SelectedPriceItemPriceId = TryGetLong("PriceItemPriceId", out seqNo) ? (long?)seqNo : null;
            SelectedItemCode = GetCellText("ItemCode");
            SelectedPriceItemName = GetCellText("PriceItemName");
            SelectedSpecText = GetCellText("SpecText");
            SelectedBillingBasis = GetCellText("BillingBasis");
            SelectedUnitName = GetCellText("UnitName");
            SelectedPriceName = GetCellText("PriceName");
            SelectedUnitPrice = GetCellDecimal("CurrentUnitPrice");

            SelectedCustCode = GetCellText("CustCode");
            SelectedPriceCategory = GetCellText("PriceCategory");
            SelectedCategoryName = GetCellText("CategoryName");
            SelectedApplyFrom = GetCellText("ApplyFrom");
            DialogResult = DialogResult.OK;
        }

        private string GetCellText(string columnName)
        {
            if (!FGrid1.Cols.Contains(columnName))
            {
                return string.Empty;
            }

            object value = FGrid1[FGrid1.Row, FGrid1.Cols[columnName].Index];
            return value == null || value == DBNull.Value ? string.Empty : value.ToString();
        }

        private bool TryGetLong(string columnName, out long value)
        {
            value = 0;
            return long.TryParse(GetCellText(columnName), out value);
        }

        private decimal GetCellDecimal(string columnName)
        {
            decimal value;
            return decimal.TryParse(GetCellText(columnName), out value) ? value : 0M;
        }

    }
}
