using C1.Win.C1FlexGrid;
using PineTree.Classes;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DialogOrderBackup : Form
    {
        private DialogMode m_DialogMode = DialogMode.New;
        private long m_orderId = -1;
        private bool m_isInitializing;
        private string m_sSelectedCustCode;
        private string m_productionStatus = OrderProductionStatusHelper.ToDbText(OrderProductionStatus.New);
        private DataTable m_orderItems;

        public DialogOrderBackup()
        {
            InitializeComponent();
            InitOrderItemTable();
            InitPreviewEvent();
            InitOrderCombos();

            dtmOrder.Value = dtmDelivery.Value = DateTime.Today;
            cbChannel.SelectedIndex = 0;
            cbDeliveryType.SelectedIndex = 0;
        }

        public DialogOrderBackup(long seqNo)
        {
            InitializeComponent();
            InitOrderItemTable();
            InitPreviewEvent();
            InitOrderCombos();

            m_orderId = seqNo;
            inputGroupHeader1.Text = "주문 수정";
            m_DialogMode = DialogMode.Edit;
            LoadOrder();
        }

        private void InitOrderCombos()
        {
            m_isInitializing = true;
            LoadTextCombo(cbChannel, CommonCodeRepository.GetItems("주문채널"));
            LoadTextCombo(cbDeliveryType, CommonCodeRepository.GetItems("배송방법"));
            m_isInitializing = false;
        }

        private void LoadTextCombo(C1.Win.C1InputPanel.InputComboBox comboBox, DataTable source)
        {
            comboBox.Items.Clear();

            foreach (DataRow row in source.Rows)
            {
                string text = row["ItemName"].ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text) && !comboBox.Items.ContainsText(text))
                {
                    comboBox.Items.AddText(text);
                }
            }

            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
        }

        private void SelectComboText(C1.Win.C1InputPanel.InputComboBox comboBox, string text)
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

        private void InitPreviewEvent()
        {
            btnSave.Click += btnSave_Click;
            btnCancel.Click += btnCancel_Click;
            btnCustList.Click += btnCustList_Click;
            btnAdd.Click += btnAdd_Click;
            c1Button2.Click += btnRemove_Click;
            FGrid1.DoubleClick += FGrid1_DoubleClick;

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
        }

        private void InitOrderItemTable()
        {
            m_orderItems = new DataTable();
            m_orderItems.Columns.Add("LineNo", typeof(int));
            m_orderItems.Columns.Add("OrderItemId", typeof(long));
            m_orderItems.Columns.Add("PriceItemSeqNo", typeof(long));
            m_orderItems.Columns.Add("PriceItemPriceId", typeof(long));
            m_orderItems.Columns.Add("ItemCode", typeof(string));
            m_orderItems.Columns.Add("ItemName", typeof(string));
            m_orderItems.Columns.Add("CustomName", typeof(string));
            m_orderItems.Columns.Add("SpecText", typeof(string));
            m_orderItems.Columns.Add("UnitName", typeof(string));
            m_orderItems.Columns.Add("BillingBasis", typeof(string));
            m_orderItems.Columns.Add("PriceName", typeof(string));
            m_orderItems.Columns.Add("WidthMm", typeof(decimal));
            m_orderItems.Columns.Add("HeightMm", typeof(decimal));
            m_orderItems.Columns.Add("Qty", typeof(decimal));
            m_orderItems.Columns.Add("AreaQty", typeof(decimal));
            m_orderItems.Columns.Add("UnitPrice", typeof(decimal));
            m_orderItems.Columns.Add("SupplyAmount", typeof(decimal));
            m_orderItems.Columns.Add("VatAmount", typeof(decimal));
            m_orderItems.Columns.Add("TotalAmount", typeof(decimal));
            m_orderItems.Columns.Add("Remark", typeof(string));
            m_orderItems.Columns.Add("RequestFilePath", typeof(string));
            m_orderItems.Columns.Add("OutputFilePath", typeof(string));
            m_orderItems.Columns.Add("ProcessRules", typeof(System.Collections.Generic.List<OrderProcessRuleData>));

            FGrid1.DataSource = m_orderItems;
            InitOrderItemGrid();
            UpdateOrderItemTotals();
        }

        private void InitOrderItemGrid()
        {
            FGrid1.Cols.Fixed = 0;
            FGrid1.BackColor = Color.MintCream;
            FGrid1.AllowSorting = AllowSortingEnum.None;
            FGrid1.SelectionMode = SelectionModeEnum.Row;

            // SetColumn 호출 순서대로 실제 그리드 컬럼을 배치한다.
            // 컬럼의 순서를 변경하려면 호출 순서를 바꾸면 됨
            int columnIndex = 0;
            SetColumn(ref columnIndex, "RequestFilePath", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "OutputFilePath", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "ProcessRules", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "LineNo", "번호", 45, TextAlignEnum.CenterCenter, true);
            SetColumn(ref columnIndex, "OrderItemId", string.Empty, 0, TextAlignEnum.CenterCenter, false);
            SetColumn(ref columnIndex, "PriceItemSeqNo", string.Empty, 0, TextAlignEnum.CenterCenter, false);
            SetColumn(ref columnIndex, "PriceItemPriceId", string.Empty, 0, TextAlignEnum.CenterCenter, false);
            SetColumn(ref columnIndex, "ItemCode", "품목코드", 90, TextAlignEnum.CenterCenter, false);
            SetColumn(ref columnIndex, "CustomName", "주문명", 160, TextAlignEnum.LeftCenter, true);
            SetColumn(ref columnIndex, "Remark", "적요", 160, TextAlignEnum.LeftCenter, true);
            SetColumn(ref columnIndex, "SpecText", "규격", 150, TextAlignEnum.CenterCenter, true);
            SetColumn(ref columnIndex, "UnitName", "단위", 60, TextAlignEnum.CenterCenter, true);
            SetColumn(ref columnIndex, "BillingBasis", "과금기준", 75, TextAlignEnum.CenterCenter, false);
            SetColumn(ref columnIndex, "PriceName", "단가표", 120, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "WidthMm", "가로", 70, TextAlignEnum.RightCenter, false, "#,##0.##");
            SetColumn(ref columnIndex, "HeightMm", "세로", 70, TextAlignEnum.RightCenter, false, "#,##0.##");
            SetColumn(ref columnIndex, "Qty", "수량", 65, TextAlignEnum.RightCenter, true, "#,##0.##");
            SetColumn(ref columnIndex, "AreaQty", "수량/회배", 85, TextAlignEnum.RightCenter, true, "#,##0.##");
            SetColumn(ref columnIndex, "UnitPrice", "단가", 85, TextAlignEnum.RightCenter, true, "#,##0");
            SetColumn(ref columnIndex, "SupplyAmount", "공급가액", 95, TextAlignEnum.RightCenter, true, "#,##0");
            SetColumn(ref columnIndex, "VatAmount", "부가세", 85, TextAlignEnum.RightCenter, true, "#,##0");
            SetColumn(ref columnIndex, "TotalAmount", "합계", 95, TextAlignEnum.RightCenter, true, "#,##0");
            SetColumn(ref columnIndex, "ItemName", "품목명", 160, TextAlignEnum.LeftCenter, true);

            if (FGrid1.Rows.Count > 0)
            {
                FGrid1.Rows[0].TextAlign = TextAlignEnum.CenterCenter;
            }
        }

        private void SetColumn(ref int columnIndex, string columnName, string caption, int width, TextAlignEnum align, bool visible, string format = null)
        {
            if (!FGrid1.Cols.Contains(columnName))
            {
                return;
            }

            FGrid1.Cols.Move(FGrid1.Cols[columnName].Index, columnIndex++);
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

        private void ShowCustomerListDialog(string custName)
        {
            using (DialogCustList dialog = new DialogCustList(custName))
            {
                PositionCustomerDialog(dialog);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                m_isInitializing = true;
                tbCustName.Text = dialog.SelectedCustName;
                m_sSelectedCustCode = tbCustCode.Text = dialog.SelectedCustCode;
                m_isInitializing = false;
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

            m_isInitializing = true;
            tbCustName.Text = row["CustName"].ToString();
            m_sSelectedCustCode = custCode;
            tbCustCode.Text = custCode;
            m_isInitializing = false;
        }

        private void ClearSelectedCustomer()
        {
            m_sSelectedCustCode = null;
            tbCustCode.Text = string.Empty;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            ShowPriceItemListDialog(string.Empty);
        }

        private void FGrid1_DoubleClick(object sender, EventArgs e)
        {
            if (!string.Equals(m_productionStatus == null ? string.Empty : m_productionStatus.Trim(),
                OrderProductionStatusHelper.ToDbText(OrderProductionStatus.New), StringComparison.Ordinal))
            {
                MessageBox.Show("진행상태가 '신규'인 주문만 품목주문정보를 수정할 수 있습니다.",
                    "품목주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (FGrid1.Row < FGrid1.Rows.Fixed)
            {
                return;
            }

            int rowIndex = FGrid1.Row - FGrid1.Rows.Fixed;
            if (rowIndex < 0 || rowIndex >= m_orderItems.Rows.Count)
            {
                return;
            }

            DataRow row = m_orderItems.Rows[rowIndex];
            long orderItemId = GetLong(row["OrderItemId"]);
            if (orderItemId <= 0 || m_orderId <= 0)
            {
                MessageBox.Show("저장된 주문품목만 수정할 수 있습니다.", "품목주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogPriceItemOrderItem item = CreateDialogOrderBackupItem(row);
            using (DialogOrderInfoEdit dialog = new DialogOrderInfoEdit(m_orderId, orderItemId, item, m_sSelectedCustCode, tbCustName.Text))
            {
                PositionOrderInfoEditDialog(dialog);

                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedOrderItem == null)
                {
                    return;
                }

                UpdateOrderItemRow(row, dialog.SelectedOrderItem);
            }
        }

        private DialogPriceItemOrderItem CreateDialogOrderBackupItem(DataRow row)
        {
            return new DialogPriceItemOrderItem
            {
                PriceItemSeqNo = GetLong(row["PriceItemSeqNo"]),
                PriceItemPriceId = GetNullableLong(row["PriceItemPriceId"]),
                ItemCode = row["ItemCode"].ToString(),
                PriceItemName = row["ItemName"].ToString(),
                CustomName = row["CustomName"].ToString(),
                SpecText = row["SpecText"].ToString(),
                BillingBasis = row["BillingBasis"].ToString(),
                UnitName = row["UnitName"].ToString(),
                PriceName = row["PriceName"].ToString(),
                WidthMm = GetNullableDecimal(row["WidthMm"]),
                HeightMm = GetNullableDecimal(row["HeightMm"]),
                Qty = GetDecimal(row["Qty"]),
                AreaQty = GetNullableDecimal(row["AreaQty"]),
                UnitPrice = GetDecimal(row["UnitPrice"]),
                SupplyAmount = GetDecimal(row["SupplyAmount"]),
                VatAmount = GetDecimal(row["VatAmount"]),
                TotalAmount = GetDecimal(row["TotalAmount"]),
                Remark = row["Remark"].ToString(),
                RequestFilePath = row.Table.Columns.Contains("RequestFilePath") && row["RequestFilePath"] != DBNull.Value ? row["RequestFilePath"].ToString() : null,
                OutputFilePath = row.Table.Columns.Contains("OutputFilePath") && row["OutputFilePath"] != DBNull.Value ? row["OutputFilePath"].ToString() : null,
                ProcessRules = row.Table.Columns.Contains("ProcessRules") && row["ProcessRules"] != DBNull.Value
                    ? (System.Collections.Generic.List<OrderProcessRuleData>)row["ProcessRules"] : null
            };
        }

        private void UpdateOrderItemRow(DataRow row, DialogPriceItemOrderItem item)
        {
            row["PriceItemSeqNo"] = item.PriceItemSeqNo;
            row["PriceItemPriceId"] = item.PriceItemPriceId.HasValue ? (object)item.PriceItemPriceId.Value : DBNull.Value;
            row["ItemCode"] = item.ItemCode;
            row["ItemName"] = item.PriceItemName;
            row["CustomName"] = item.CustomName;
            row["SpecText"] = item.SpecText;
            row["UnitName"] = item.UnitName;
            row["BillingBasis"] = item.BillingBasis;
            row["PriceName"] = item.PriceName;
            row["WidthMm"] = item.WidthMm.HasValue ? (object)item.WidthMm.Value : DBNull.Value;
            row["HeightMm"] = item.HeightMm.HasValue ? (object)item.HeightMm.Value : DBNull.Value;
            row["Qty"] = item.Qty;
            row["AreaQty"] = item.AreaQty.HasValue ? (object)item.AreaQty.Value : DBNull.Value;
            row["UnitPrice"] = item.UnitPrice;
            row["SupplyAmount"] = item.SupplyAmount;
            row["VatAmount"] = item.VatAmount;
            row["TotalAmount"] = item.TotalAmount;
            row["Remark"] = item.Remark;
            row["RequestFilePath"] = (object)item.RequestFilePath ?? DBNull.Value;
            row["OutputFilePath"] = (object)item.OutputFilePath ?? DBNull.Value;
            row["ProcessRules"] = (object)item.ProcessRules ?? DBNull.Value;
            UpdateOrderItemTotals();
            FGrid1.Refresh();
        }

        private void PositionOrderInfoEditDialog(Form dialog)
        {
            Point parentTopLeft = PointToScreen(Point.Empty);
            Point parentRightTop = PointToScreen(new Point(ClientSize.Width, 0));
            Rectangle screen = Screen.FromControl(this).WorkingArea;

            int x = parentRightTop.X - dialog.Width - 50;
            int y = parentTopLeft.Y + 100;
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - dialog.Width));
            y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - dialog.Height));

            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(this.Location.X + x, y);
        }

        private void ShowPriceItemListDialog(string itemName)
        {
            if (string.IsNullOrWhiteSpace(m_sSelectedCustCode))
            {
                MessageBox.Show("거래처를 먼저 선택해주세요.", "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (DialogOrderItem dialog = new DialogOrderItem(itemName, m_sSelectedCustCode, tbCustName.Text))
            {
                PositionPriceItemDialog(dialog);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (dialog.SelectedOrderItem == null)
                {
                    return;
                }

                AddOrderItem(dialog.SelectedOrderItem, null);
            }
        }

        private void AddOrderItem(DialogPriceItemOrderItem item, long? orderItemId)
        {
            DataRow row = m_orderItems.NewRow();
            row["LineNo"] = m_orderItems.Rows.Count + 1;
            row["OrderItemId"] = orderItemId.HasValue ? (object)orderItemId.Value : DBNull.Value;
            row["PriceItemSeqNo"] = item.PriceItemSeqNo;
            row["PriceItemPriceId"] = item.PriceItemPriceId.HasValue ? (object)item.PriceItemPriceId.Value : DBNull.Value;
            row["ItemCode"] = item.ItemCode;
            row["ItemName"] = item.PriceItemName;
            row["CustomName"] = item.CustomName;
            row["SpecText"] = item.SpecText;
            row["UnitName"] = item.UnitName;
            row["BillingBasis"] = item.BillingBasis;
            row["PriceName"] = item.PriceName;
            row["WidthMm"] = item.WidthMm.HasValue ? (object)item.WidthMm.Value : DBNull.Value;
            row["HeightMm"] = item.HeightMm.HasValue ? (object)item.HeightMm.Value : DBNull.Value;
            row["Qty"] = item.Qty;
            row["AreaQty"] = item.AreaQty.HasValue ? (object)item.AreaQty.Value : DBNull.Value;
            row["UnitPrice"] = item.UnitPrice;
            row["SupplyAmount"] = item.SupplyAmount;
            row["VatAmount"] = item.VatAmount;
            row["TotalAmount"] = item.TotalAmount;
            row["Remark"] = item.Remark;
            row["RequestFilePath"] = (object)item.RequestFilePath ?? DBNull.Value;
            row["OutputFilePath"] = (object)item.OutputFilePath ?? DBNull.Value;
            row["ProcessRules"] = (object)item.ProcessRules ?? DBNull.Value;
            m_orderItems.Rows.Add(row);
            UpdateOrderItemTotals();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (FGrid1.Row < FGrid1.Rows.Fixed)
            {
                return;
            }

            int rowIndex = FGrid1.Row - FGrid1.Rows.Fixed;
            if (rowIndex < 0 || rowIndex >= m_orderItems.Rows.Count)
            {
                return;
            }

            m_orderItems.Rows.RemoveAt(rowIndex);
            ResetLineNumbers();
        }

        private void ResetLineNumbers()
        {
            for (int i = 0; i < m_orderItems.Rows.Count; i++)
            {
                m_orderItems.Rows[i]["LineNo"] = i + 1;
            }
            UpdateOrderItemTotals();
        }

        private void UpdateOrderItemTotals()
        {
            FGrid1.Subtotal(AggregateEnum.Clear);
            FGrid1.SubtotalPosition = SubtotalPositionEnum.BelowData;
            foreach (string columnName in new[] { "SupplyAmount", "VatAmount", "TotalAmount" })
            {
                FGrid1.Subtotal(AggregateEnum.Sum, -1, -1, FGrid1.Cols[columnName].Index, "합계");
            }

            if (FGrid1.Rows.Count > FGrid1.Rows.Fixed)
            {
                int totalRow = FGrid1.Rows.Count - 1;
                if (FGrid1.Rows[totalRow].IsNode)
                {
                    FGrid1[totalRow, "CustomName"] = "금액 합계";
                    FGrid1.Rows[totalRow].AllowEditing = false;
                }
            }
        }

        private void PositionCustomerDialog(Form dialog)
        {
            Rectangle buttonBounds = c1InputPanel1.GetItemBounds(btnCustList);
            Point buttonRight = c1InputPanel1.PointToScreen(new Point(buttonBounds.Right, buttonBounds.Top));
            PositionDialog(dialog, buttonRight);
        }

        private void PositionPriceItemDialog(Form dialog)
        {
            Point location = PointToScreen(new Point(ClientSize.Width / 2 - 20, 20));
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int x = Math.Max(workingArea.Left, Math.Min(location.X, workingArea.Right - dialog.Width));
            int y = Math.Max(workingArea.Top, Math.Min(location.Y, workingArea.Bottom - dialog.Height));
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(x, y);
        }

        private void PositionDialog(Form dialog, Point basePoint)
        {
            Rectangle screen = Screen.FromControl(this).WorkingArea;
            int x = basePoint.X + 6;
            int y = basePoint.Y;

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

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!CheckValidation())
            {
                return;
            }

            try
            {
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
            return new OrderSaveData
            {
                OrderId = m_DialogMode == DialogMode.Edit && m_orderId > 0 ? (long?)m_orderId : null,
                OrderDate = GetDateValue(dtmOrder.Value) ?? DateTime.Today,
                CustCode = m_sSelectedCustCode,
                OrderChannel = cbChannel.Text.Trim(),
                DeliveryType = cbDeliveryType.Text.Trim(),
                DeliveryFee = GetDecimal(tbDeliveryFee.Value),
                RequestedDueAt = GetDateValue(dtmDelivery.Value),
                OrderTitle = GetOrderTitle(),
                OrderDescription = tbRemark.Text,
                ProductionStatus = m_productionStatus,
                SourceChannel = "직접입력",
                CreatedBy = Environment.UserName,
                Items = CreateOrderItemSaveDataList()
            };
        }

        private string GetOrderTitle()
        {
            if (m_orderItems.Rows.Count == 0)
            {
                return string.Empty;
            }

            string firstItemName = m_orderItems.Rows[0]["CustomName"].ToString();
            if (string.IsNullOrWhiteSpace(firstItemName))
            {
                firstItemName = m_orderItems.Rows[0]["ItemName"].ToString();
            }
            if (m_orderItems.Rows.Count == 1)
            {
                return firstItemName;
            }

            return string.Format("{0} 외 {1}건", firstItemName, m_orderItems.Rows.Count - 1);
        }

        private System.Collections.Generic.List<OrderItemSaveData> CreateOrderItemSaveDataList()
        {
            System.Collections.Generic.List<OrderItemSaveData> items = new System.Collections.Generic.List<OrderItemSaveData>();
            foreach (DataRow row in m_orderItems.Rows)
            {
                decimal? width = GetNullableDecimal(row["WidthMm"]);
                decimal? height = GetNullableDecimal(row["HeightMm"]);
                decimal? areaPerQty = width.HasValue && height.HasValue
                    ? (decimal?)(width.Value * height.Value / 1000000M)
                    : null;

                items.Add(new OrderItemSaveData
                {
                    OrderItemId = GetNullableLong(row["OrderItemId"]),
                    LineNo = GetInt(row["LineNo"]),
                    PriceItemSeqNo = GetLong(row["PriceItemSeqNo"]),
                    PriceItemPriceId = GetNullableLong(row["PriceItemPriceId"]),
                    ItemCode = row["ItemCode"].ToString(),
                    ItemName = row["ItemName"].ToString(),
                    CustomName = row["CustomName"].ToString(),
                    SpecText = row["SpecText"].ToString(),
                    BillingBasis = row["BillingBasis"].ToString(),
                    WidthMm = width,
                    HeightMm = height,
                    AreaPerQty = areaPerQty,
                    Qty = GetNullableDecimal(row["Qty"]),
                    AreaQty = GetNullableDecimal(row["AreaQty"]),
                    UnitPrice = GetNullableDecimal(row["UnitPrice"]),
                    SupplyAmount = GetDecimal(row["SupplyAmount"]),
                    VatAmount = GetDecimal(row["VatAmount"]),
                    TotalAmount = GetDecimal(row["TotalAmount"]),
                    ItemDescription = row["Remark"].ToString(),
                    ProductionStatus = m_productionStatus,
                    RequestFilePath = row["RequestFilePath"] == DBNull.Value ? null : row["RequestFilePath"].ToString(),
                    OutputFilePath = row["OutputFilePath"] == DBNull.Value ? null : row["OutputFilePath"].ToString(),
                    ProcessRules = row["ProcessRules"] == DBNull.Value ? null : (System.Collections.Generic.List<OrderProcessRuleData>)row["ProcessRules"]
                });
            }

            return items;
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
            SelectComboText(cbChannel, row["OrderChannel"].ToString());
            SelectComboText(cbDeliveryType, row["DeliveryType"].ToString());
            tbDeliveryFee.Value = GetDecimal(row["DeliveryFee"]);
            m_productionStatus = row["ProductionStatus"].ToString();
            tbRemark.Text = row["OrderDescription"].ToString();

            DateTime? dueAt = GetDateValue(row["RequestedDueAt"]);
            if (dueAt.HasValue)
            {
                dtmDelivery.Value = dueAt.Value;
            }

            LoadOrderItems();
            m_isInitializing = false;
        }

        private void LoadOrderItems()
        {
            DataTable items = OrderRepository.GetOrderItemsForEdit(m_orderId);
            m_orderItems.Rows.Clear();

            foreach (DataRow row in items.Rows)
            {
                DialogPriceItemOrderItem item = CreateDialogOrderBackupItem(row);
                item.Remark = row["ItemDescription"].ToString();
                item.ProcessRules = OrderProcessRuleRepository.GetRules(m_orderId, GetLong(row["OrderItemId"]));

                AddOrderItem(item, GetNullableLong(row["OrderItemId"]));
            }

            ResetLineNumbers();
        }

        private bool CheckValidation()
        {
            if (string.IsNullOrWhiteSpace(m_sSelectedCustCode))
            {
                MessageBox.Show("거래처를 선택해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (m_orderItems.Rows.Count == 0)
            {
                MessageBox.Show("품목을 1개 이상 추가해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
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

        private long GetLong(object value)
        {
            long result;
            return value != null && value != DBNull.Value && long.TryParse(value.ToString(), out result) ? result : 0L;
        }

        private long? GetNullableLong(object value)
        {
            long result = GetLong(value);
            return result > 0L ? (long?)result : null;
        }

        private int GetInt(object value)
        {
            int result;
            return value != null && value != DBNull.Value && int.TryParse(value.ToString(), out result) ? result : 0;
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
            return result > 0M ? (decimal?)result : null;
        }
    }
}
