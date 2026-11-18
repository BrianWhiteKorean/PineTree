using C1.Win.C1InputPanel;
using PineTree.Classes;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DialogOrderOld : Form
    {
        private DialogMode m_DialogMode = DialogMode.New;
        private long m_orderId = -1;
        private long? m_orderItemId;
        private bool m_isDuplicateChecked;
        private string m_checkedItemCode = string.Empty;
        private DateTime m_applyFrom = DateTime.Today;
        private bool m_isInitializing;
        private string m_sSelectedCustCode;
        private long? m_selectedPriceItemSeqNo;
        private long? m_selectedPriceItemPriceId;
        private string m_selectedUnitName = string.Empty;
        private string m_productionStatus = OrderProductionStatusHelper.ToDbText(OrderProductionStatus.New);

        public DialogOrderOld()
        {
            InitializeComponent();
            InitPreviewEvent();

            dtmOrder.Value = DateTime.Today;
            LoadBillingBasisCombo();
            //InitEvent();
            //InitCombo();
        }

        public DialogOrderOld(long seqNo)
        {
            InitializeComponent();
            InitPreviewEvent();
            LoadBillingBasisCombo();

            m_orderId = seqNo;
            inputGroupHeader1.Text = "주문 수정";
            m_DialogMode = DialogMode.Edit;
            LoadOrder();
        }

        private void InitPreviewEvent()
        {
            btnSave.Click += btnSave_Click;
            btnCancel.Click += btnCancel_Click;
            btnCustList.Click += btnCustList_Click;
            btnPriceItemList.Click += btnPriceItemList_Click;
            tbCustName.TextChanged += delegate
            {
                if (m_isInitializing)
                {
                    return;
                }

                m_sSelectedCustCode = null;
                tbCustCode.Text = string.Empty;
            };
            tbCustName.TextBox.KeyDown += tbCustName_KeyDown;
            tbItemName.TextChanged += delegate
            {
                if (m_isInitializing)
                {
                    return;
                }

                m_selectedPriceItemSeqNo = null;
                m_selectedPriceItemPriceId = null;
                m_selectedUnitName = string.Empty;
                tbItemCode.Text = string.Empty;
                UpdateOrderCalculations();
            };
            tbItemName.TextBox.KeyDown += tbItemName_KeyDown;
            tbWidth.ValueChanged += DimensionOrQty_ValueChanged;
            tbHeight.ValueChanged += DimensionOrQty_ValueChanged;
            tbQty.ValueChanged += DimensionOrQty_ValueChanged;
            tbPrice.ValueChanged += Price_ValueChanged;
        }

        private void tbCustName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            ApplyCustomerByCustNameSearch();
        }

        private void ApplyCustomerByCustNameSearch()
        {
            string custName = tbCustName.Text.Trim();
            if (string.IsNullOrWhiteSpace(custName))
            {
                ClearSelectedCustomer();
                return;
            }

            DataTable customers = CustomerRepository.GetCustomers(custName, string.Empty, null, null);
            if (customers.Rows.Count == 0)
            {
                ClearSelectedCustomer();
                return;
            }

            if (customers.Rows.Count == 1)
            {
                ApplySelectedCustomer(customers.Rows[0]);
                return;
            }

            ShowCustomerListDialog(custName);
        }

        private void btnCustList_Click(object sender, EventArgs e)
        {
            ShowCustomerListDialog(tbCustName.Text);
        }

        private void tbItemName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            ShowPriceItemListDialog(tbItemName.Text);
        }

        private void btnPriceItemList_Click(object sender, EventArgs e)
        {
            ShowPriceItemListDialog(tbItemName.Text);
        }

        private void ShowCustomerListDialog(string custName)
        {
            using (DialogCustList dialog = new DialogCustList(custName))
            {
                PositionCustomerDialog(dialog);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                tbCustName.Text = dialog.SelectedCustName;
                m_sSelectedCustCode = tbCustCode.Text = dialog.SelectedCustCode;
            }
        }

        private void ShowPriceItemListDialog(string itemName)
        {
            if (string.IsNullOrWhiteSpace(m_sSelectedCustCode))
            {
                MessageBox.Show("거래처를 먼저 선택해주세요.", "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (DialogPriceItemList dialog = new DialogPriceItemList(itemName, m_sSelectedCustCode, tbCustName.Text))
            {
                PositionPriceItemDialog(dialog);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ApplySelectedPriceItem(dialog);
            }
        }

        private void ApplySelectedPriceItem(DialogPriceItemList dialog)
        {
            tbItemName.Text = dialog.SelectedPriceItemName;
            tbItemCode.Text = dialog.SelectedItemCode;
            tbSpecText.Text = dialog.SelectedSpecText;
            SelectComboText(cbBillingBasis, dialog.SelectedUnitName);
            SelectComboText(cbApplyPrice, dialog.SelectedPriceName);
            tbPrice.Value = dialog.SelectedUnitPrice;
            m_selectedPriceItemSeqNo = dialog.SelectedPriceItemSeqNo;
            m_selectedPriceItemPriceId = dialog.SelectedPriceItemPriceId;
            m_selectedUnitName = dialog.SelectedUnitName;
            UpdateOrderCalculations();
        }

        private void DimensionOrQty_ValueChanged(object sender, EventArgs e)
        {
            UpdateOrderCalculations();
        }

        private void Price_ValueChanged(object sender, EventArgs e)
        {
            UpdateAmounts();
        }

        private void UpdateOrderCalculations()
        {
            UpdateAreaAndSpec();
            UpdateAmounts();
        }

        private void UpdateAreaAndSpec()
        {
            if (!string.Equals(m_selectedUnitName, "회배", StringComparison.Ordinal))
            {
                tbArea.Text = string.Empty;
                return;
            }

            decimal width = GetDecimal(tbWidth.Value);
            decimal height = GetDecimal(tbHeight.Value);
            decimal qty = GetQtyForCalculation();
            decimal area = width * height * qty / 1000000M;
            tbArea.Text = area == 0M ? string.Empty : area.ToString("#,##0.00");

            if (width > 0M && height > 0M)
            {
                tbSpecText.Text = string.Format("{0}*{1}_{2}장", FormatDimension(width), FormatDimension(height), FormatQty(qty));
            }
        }

        private decimal GetQtyForCalculation()
        {
            decimal qty = GetDecimal(tbQty.Value);
            return qty <= 0M ? 1M : qty;
        }

        private string FormatDimension(decimal value)
        {
            return value.ToString("0.####");
        }

        private string FormatQty(decimal value)
        {
            return value.ToString("0.####");
        }

        private void UpdateAmounts()
        {
            decimal unitPrice = GetDecimal(tbPrice.Value);
            decimal billingQty = GetBillingQtyForAmount();
            decimal supplyAmount = Math.Round(unitPrice * billingQty, 0, MidpointRounding.AwayFromZero);
            decimal vat = Math.Round(supplyAmount * 0.1M, 0, MidpointRounding.AwayFromZero);
            decimal totalAmount = supplyAmount + vat;

            tbVat.Value = vat;
            tbSum.Value = totalAmount;
        }

        private decimal GetBillingQtyForAmount()
        {
            if (string.Equals(m_selectedUnitName, "회배", StringComparison.Ordinal))
            {
                decimal width = GetDecimal(tbWidth.Value);
                decimal height = GetDecimal(tbHeight.Value);
                return width * height * GetQtyForCalculation() / 1000000M;
            }

            return GetQtyForCalculation();
        }

        private void LoadBillingBasisCombo()
        {
            try
            {
                LoadTextCombo(cbBillingBasis, CommonCodeRepository.GetItems("단위"), "ItemName", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("단가적용기준 목록을 불러오지 못했습니다.\r\n" + ex.Message, "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplySelectedCustomer(DataRow row)
        {
            string custCode = row["CustCode"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(custCode))
            {
                ClearSelectedCustomer();
                return;
            }

            tbCustName.Text = row["CustName"].ToString();
            m_sSelectedCustCode = custCode;
            tbCustCode.Text = custCode;
        }

        private void ClearSelectedCustomer()
        {
            m_sSelectedCustCode = null;
            tbCustCode.Text = string.Empty;
        }

        private void PositionCustomerDialog(Form dialog)
        {
            Rectangle buttonBounds = c1InputPanel1.GetItemBounds(btnCustList);
            Point buttonRight = c1InputPanel1.PointToScreen(new Point(buttonBounds.Right, buttonBounds.Top));
            Rectangle screen = Screen.FromControl(this).WorkingArea;

            int x = buttonRight.X + 6;
            int y = buttonRight.Y;

            if (x + dialog.Width > screen.Right)
            {
                x = Math.Max(screen.Left, PointToScreen(Point.Empty).X - dialog.Width - 6);
            }

            if (y + dialog.Height > screen.Bottom)
            {
                y = Math.Max(screen.Top, screen.Bottom - dialog.Height);
            }

            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(x, y);
        }

        private void PositionPriceItemDialog(Form dialog)
        {
            Rectangle buttonBounds = c1InputPanel1.GetItemBounds(btnPriceItemList);
            Point buttonRight = c1InputPanel1.PointToScreen(new Point(buttonBounds.Right, buttonBounds.Top));
            Rectangle screen = Screen.FromControl(this).WorkingArea;

            int x = buttonRight.X + 6;
            int y = buttonRight.Y;

            if (x + dialog.Width > screen.Right)
            {
                x = Math.Max(screen.Left, PointToScreen(Point.Empty).X - dialog.Width - 6);
            }

            if (y + dialog.Height > screen.Bottom)
            {
                y = Math.Max(screen.Top, screen.Bottom - dialog.Height);
            }

            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(x, y);
        }

        private void InitEvent()
        {
            //c1Button1.Click += btnSave_Click;
            //c1Button2.Click += btnCancel_Click;
            //tbItemCode.TextChanged += tbItemCode_TextChanged;
            //cbCategory.SelectedIndexChanged += cbCategory_SelectedIndexChanged;
            //cbApplyPrice.SelectedIndexChanged += cbApplyPrice_SelectedIndexChanged;
        }

        private void InitCombo()
        {
            m_isInitializing = true;

            //LoadTextCombo(cbCategory, PriceItemRepository.GetPriceCategories(), "PriceCategory", false);
            //LoadCategoryNameCombo();
            //LoadTextCombo(cbApplyPrice, PriceItemRepository.GetPriceNames(), "PriceName", false);
            //LoadTextCombo(cbUnit, CommonCodeRepository.GetItems("단위"), "ItemName", false);
            //LoadTextCombo(cbBillingBasis, CommonCodeRepository.GetItems("단가적용기준"), "ItemName", false);

            //cbIsActive.SelectedIndex = 0;

            //m_isInitializing = false;
            //tbPrice.Value = 0M;
            //m_applyFrom = DateTime.Today;
        }

        private void LoadTextCombo(InputComboBox comboBox, DataTable source, string columnName, bool allowEmpty)
        {
            comboBox.Items.Clear();

            if (allowEmpty)
            {
                comboBox.Items.AddText(string.Empty);
            }

            foreach (DataRow row in source.Rows)
            {
                string text = row[columnName].ToString();
                if (!string.IsNullOrWhiteSpace(text) && !comboBox.Items.ContainsText(text))
                {
                    comboBox.Items.AddText(text);
                }
            }

            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
        }

        private void LoadCategoryNameCombo()
        {
            //LoadTextCombo(cbCategoryName, PriceItemRepository.GetCategoryNames(cbCategory.Text), "CategoryName", false);
        }

        private void cbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_isInitializing)
            {
                return;
            }

            LoadCategoryNameCombo();
        }

        private void cbApplyPrice_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_isInitializing)
            {
                return;
            }

            if (m_DialogMode == DialogMode.New)
            {
                tbPrice.Value = 0M;
                m_applyFrom = DateTime.Today;
                return;
            }

            ApplySelectedPriceName();
        }

        private void ApplySelectedPriceName()
        {
            string priceName = cbApplyPrice.Text.Trim();
            if (string.IsNullOrWhiteSpace(priceName))
            {
                tbPrice.Value = 0M;
                m_applyFrom = DateTime.Today;
                return;
            }

            DataRow row = PriceItemRepository.GetLatestPriceByPriceName(priceName);
            if (row == null)
            {
                tbPrice.Value = 0M;
                m_applyFrom = DateTime.Today;
                return;
            }

            decimal unitPrice;
            if (decimal.TryParse(row["UnitPrice"].ToString(), out unitPrice))
            {
                tbPrice.Value = unitPrice;
            }

            DateTime applyFrom;
            m_applyFrom = DateTime.TryParse(row["ApplyFrom"].ToString(), out applyFrom)
                ? applyFrom.Date
                : DateTime.Today;

            if (string.IsNullOrWhiteSpace(tbRemark.Text) && row.Table.Columns.Contains("Remark"))
            {
                tbRemark.Text = row["Remark"].ToString();
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!CheckValidation())
            {
                return;
            }

            try
            {
                UpdateOrderCalculations();
                m_orderId = OrderRepository.UpsertOrder(CreateOrderSaveData());
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류가 발생했습니다.\r\n" + ex.Message, "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private OrderSaveData CreateOrderSaveData()
        {
            decimal width = GetDecimal(tbWidth.Value);
            decimal height = GetDecimal(tbHeight.Value);
            decimal qty = GetQtyForCalculation();
            decimal areaPerQty = width > 0M && height > 0M ? width * height / 1000000M : 0M;
            decimal billingQty = GetBillingQtyForAmount();
            decimal supplyAmount = Math.Round(GetDecimal(tbPrice.Value) * billingQty, 0, MidpointRounding.AwayFromZero);
            decimal vatAmount = GetDecimal(tbVat.Value);
            decimal totalAmount = GetDecimal(tbSum.Value);

            return new OrderSaveData
            {
                OrderId = m_DialogMode == DialogMode.Edit && m_orderId > 0 ? (long?)m_orderId : null,
                OrderItemId = m_orderItemId,
                OrderDate = GetDateValue(dtmOrder.Value) ?? DateTime.Today,
                CustCode = m_sSelectedCustCode,
                RequestedDueAt = GetDateValue(dtmDelivery.Value),
                OrderTitle = tbItemName.Text.Trim(),
                OrderDescription = tbRemark.Text,
                ProductionStatus = m_productionStatus,
                SourceChannel = "직접입력",
                PriceItemSeqNo = m_selectedPriceItemSeqNo.Value,
                PriceItemPriceId = m_selectedPriceItemPriceId,
                ItemCode = tbItemCode.Text,
                ItemName = tbItemName.Text,
                SpecText = tbSpecText.Text,
                BillingBasis = cbBillingBasis.Text,
                WidthMm = width > 0M ? (decimal?)width : null,
                HeightMm = height > 0M ? (decimal?)height : null,
                AreaPerQty = areaPerQty > 0M ? (decimal?)areaPerQty : null,
                Qty = qty,
                AreaQty = string.Equals(m_selectedUnitName, "회배", StringComparison.Ordinal) && billingQty > 0M ? (decimal?)billingQty : null,
                UnitPrice = GetNullableDecimal(tbPrice.Value),
                SupplyAmount = supplyAmount,
                VatAmount = vatAmount,
                TotalAmount = totalAmount,
                ItemDescription = tbRemark.Text,
                RequestFilePath = tbSourceFile.Text,
                OutputFilePath = tbDestFileName.Text,
                CreatedBy = Environment.UserName
            };
        }

        private void LoadOrder()
        {
            DataRow row = OrderRepository.GetOrderForEdit(m_orderId);
            if (row == null)
            {
                MessageBox.Show("주문정보를 찾을 수 없습니다.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                return;
            }

            m_isInitializing = true;

            dtmOrder.Value = GetDateValue(row["OrderDate"]) ?? DateTime.Today;
            tbCustName.Text = row["CustName"].ToString();
            tbCustCode.Text = row["CustCode"].ToString();
            m_sSelectedCustCode = tbCustCode.Text;
            m_orderItemId = GetNullableLong(row["OrderItemId"]);
            m_selectedPriceItemSeqNo = GetNullableLong(row["PriceItemSeqNo"]);
            m_selectedPriceItemPriceId = GetNullableLong(row["PriceItemPriceId"]);
            m_productionStatus = row["ProductionStatus"].ToString();
            tbItemName.Text = row["ItemName"].ToString();
            tbItemCode.Text = row["ItemCode"].ToString();
            tbSpecText.Text = row["SpecText"].ToString();
            SelectComboText(cbBillingBasis, row["BillingBasis"].ToString());
            SelectComboText(cbApplyPrice, row["PriceName"].ToString());
            m_selectedUnitName = cbBillingBasis.Text;
            tbWidth.Value = GetDecimal(row["WidthMm"]);
            tbHeight.Value = GetDecimal(row["HeightMm"]);
            tbQty.Value = GetDecimal(row["Qty"]);
            tbPrice.Value = GetDecimal(row["UnitPrice"]);
            tbVat.Value = GetDecimal(row["VatAmount"]);
            tbSum.Value = GetDecimal(row["TotalAmount"]);
            tbRemark.Text = row["OrderDescription"].ToString();
            tbSourceFile.Text = row["RequestFilePath"].ToString();
            tbDestFileName.Text = row["OutputFilePath"].ToString();

            DateTime? dueAt = GetDateValue(row["RequestedDueAt"]);
            if (dueAt.HasValue)
            {
                dtmDelivery.Value = dueAt.Value;
            }

            m_isInitializing = false;
            UpdateOrderCalculations();
        }

        private DateTime? GetDateValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            DateTime result;
            return DateTime.TryParse(value.ToString(), out result) ? (DateTime?)result : null;
        }

        private long? GetNullableLong(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            long result;
            return long.TryParse(value.ToString(), out result) ? (long?)result : null;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }


        private void tbItemCode_TextChanged(object sender, EventArgs e)
        {
            m_isDuplicateChecked = string.Equals(m_checkedItemCode, tbItemCode.Text.Trim(), StringComparison.Ordinal);
        }

        private bool CheckValidation()
        {
            string itemCode = tbItemCode.Text.Trim();
            string itemName = tbItemName.Text.Trim();

            if (string.IsNullOrWhiteSpace(m_sSelectedCustCode))
            {
                MessageBox.Show("거래처를 선택해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                MessageBox.Show("품목명은 필수항목입니다.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!m_selectedPriceItemSeqNo.HasValue)
            {
                MessageBox.Show("품목 검색을 통해 품목을 선택해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                MessageBox.Show("품목코드는 필수항목입니다.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(cbApplyPrice.Text))
            {
                MessageBox.Show("적용단가표는 필수항목입니다.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (GetDecimal(tbPrice.Value) <= 0)
            {
                MessageBox.Show("단가는 0보다 커야 합니다.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private decimal GetDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0M;
            }

            decimal result;
            return decimal.TryParse(value.ToString(), out result) ? result : 0M;
        }

        private decimal? GetNullableDecimal(object value)
        {
            decimal result = GetDecimal(value);
            return result > 0 ? (decimal?)result : null;
        }

        private void LoadPriceItem()
        {
            //DataRow row = PriceItemRepository.GetPriceItem(m_seqNo);
            //if (row == null)
            //{
            //    MessageBox.Show("상품정보를 찾을 수 없습니다.", "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //    DialogResult = DialogResult.Cancel;
            //    return;
            //}

            //m_isInitializing = true;

            //SelectComboText(cbCategory, row["PriceCategory"].ToString());
            //LoadCategoryNameCombo();
            //SelectComboText(cbCategoryName, row["CategoryName"].ToString());
            //tbItemName.Text = row["PriceItemName"].ToString();
            //tbItemCode.Text = row["ItemCode"].ToString();
            //tbSpecText.Text = row["SpecText"].ToString();
            //SelectComboText(cbUnit, row["UnitName"].ToString());
            //SelectComboText(cbBillingBasis, row["BillingBasis"].ToString());
            //tbDefaultWidth.Value = GetDecimal(row["DefaultWidth"]);
            //tbDefaultHeight.Value = GetDecimal(row["DefaultHeight"]);
            //SelectComboText(cbApplyPrice, row["PriceName"].ToString());
            //tbPrice.Value = GetDecimal(row["UnitPrice"]);
            //tbRemark.Text = row["Remark"].ToString();
            //cbIsActive.SelectedIndex = IsActive(row["IsActive"]) ? 0 : 1;

            //DateTime applyFrom;
            //m_applyFrom = DateTime.TryParse(row["ApplyFrom"].ToString(), out applyFrom)
            //    ? applyFrom.Date
            //    : DateTime.Today;

            //m_checkedItemCode = tbItemCode.Text.Trim();
            //m_isDuplicateChecked = true;
            //m_isInitializing = false;
        }

        private void SelectComboText(InputComboBox comboBox, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!comboBox.Items.ContainsText(text))
            {
                comboBox.Items.AddText(text);
            }

            comboBox.Text = text;
        }

        private bool IsActive(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return true;
            }

            return value.ToString() == "1" || value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
