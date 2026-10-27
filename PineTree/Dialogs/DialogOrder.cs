using C1.Win.C1FlexGrid;
using PineTree.Classes;
using PineTree.UserControlsOrder;
using System.Collections.Generic;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DialogOrder : Form
    {
        private DialogMode m_DialogMode = DialogMode.New;
        private long m_orderId = -1;
        private bool m_isInitializing;
        private bool m_isLoadingOrderItems;
        private string m_sSelectedCustCode;
        private string m_productionStatus = OrderProductionStatusHelper.ToDbText(OrderProductionStatus.New);
        private DataTable m_orderItems;
        private Dictionary<string, Func<UserControl>> m_orderEditorFactories;
        private string m_orderEditorCategory;
        private UserControl m_orderEditor;
        private Dictionary<DataRow, UserControl> m_orderItemEditors;
        private UserControl m_defaultOrderEditor;

        public DialogOrder()
        {
            InitializeComponent();
            InitOrderItemTable();
            InitOrderEditor();
            InitPreviewEvent();
            InitOrderCombos();

            dtmOrder.Value = dtmDelivery.Value = DateTime.Today;
            cbChannel.SelectedIndex = 0;
            cbDeliveryType.SelectedIndex = 0;
        }

        public DialogOrder(long seqNo)
        {
            InitializeComponent();
            InitOrderItemTable();
            InitOrderEditor();
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
            try
            {
                using (DataTable items = CommonCodeRepository.GetOrderEntryItems())
                {
                    LoadTextCombo(cbChannel, items.Select("GroupName = '주문채널'", "SeqNo"));
                    LoadTextCombo(cbDeliveryType, items.Select("GroupName = '배송방법'", "SeqNo"));
                }
            }
            finally
            {
                m_isInitializing = false;
            }
        }

        private void LoadTextCombo(C1.Win.C1InputPanel.InputComboBox comboBox, DataRow[] source)
        {
            comboBox.Items.Clear();

            foreach (DataRow row in source)
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
            FGrid1.AfterRowColChange += delegate { UpdateSelectedOrderEditor(); };

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

        private void InitOrderEditor()
        {
            // 품목분류별 편집 컨트롤은 이 목록에 등록한다.
            m_orderEditorFactories = new Dictionary<string, Func<UserControl>>(StringComparer.Ordinal)
            {
                { string.Empty, () => new UcOrderBase() },
                { "현수막", () => new UcOrder현수막() }
            };
            m_orderItemEditors = new Dictionary<DataRow, UserControl>();
            Disposed += delegate { DisposeOrderEditors(); };
            ShowOrderEditor(string.Empty);
        }

        private void UpdateSelectedOrderEditor()
        {
            if (m_orderEditorFactories == null || m_isLoadingOrderItems)
            {
                return;
            }

            string categoryName = string.Empty;
            DataRow selectedItem = null;
            int rowIndex = FGrid1.Row;
            if (rowIndex >= FGrid1.Rows.Fixed && rowIndex < FGrid1.Rows.Count && !FGrid1.Rows[rowIndex].IsNode)
            {
                categoryName = Convert.ToString(FGrid1[rowIndex, "CategoryName"]).Trim();
                int itemIndex = rowIndex - FGrid1.Rows.Fixed;
                if (itemIndex >= 0 && itemIndex < m_orderItems.Rows.Count) selectedItem = m_orderItems.Rows[itemIndex];
            }

            ShowOrderItemEditor(categoryName, selectedItem);
        }

        private void ShowOrderEditor(string categoryName)
        {
            ShowOrderItemEditor(categoryName, null);
        }

        private void ShowOrderItemEditor(string categoryName, DataRow orderItem)
        {
            string editorCategory = m_orderEditorFactories.ContainsKey(categoryName) ? categoryName : string.Empty;
            UserControl nextEditor;
            if (orderItem == null || editorCategory.Length == 0)
            {
                if (m_defaultOrderEditor == null) m_defaultOrderEditor = m_orderEditorFactories[string.Empty]();
                nextEditor = m_defaultOrderEditor;
            }
            else if (!m_orderItemEditors.TryGetValue(orderItem, out nextEditor))
            {
                nextEditor = m_orderEditorFactories[editorCategory]();
                m_orderItemEditors.Add(orderItem, nextEditor);
                var banner = nextEditor as UcOrder현수막;
                if (banner != null)
                {
                    string json = orderItem["EditorStateJson"].ToString();
                    var state = BannerOrderEditorState.FromJson(json) ?? new BannerOrderEditorState {
                        WidthMm = GetDecimal(orderItem["WidthMm"]), HeightMm = GetDecimal(orderItem["HeightMm"]),
                        Quantity = GetDecimal(orderItem["Qty"]),
                        SourceFilePath = orderItem["RequestFilePath"] == DBNull.Value ? null : orderItem["RequestFilePath"].ToString()
                    };
                    // Typed item columns remain authoritative for legacy item-edit dialogs.
                    if (state.WidthMm != GetDecimal(orderItem["WidthMm"]) || state.HeightMm != GetDecimal(orderItem["HeightMm"])) state.ZoomType = null;
                    state.WidthMm = GetDecimal(orderItem["WidthMm"]);
                    state.HeightMm = GetDecimal(orderItem["HeightMm"]);
                    state.Quantity = GetDecimal(orderItem["Qty"]);
                    state.SourceFilePath = orderItem["RequestFilePath"] == DBNull.Value ? null : orderItem["RequestFilePath"].ToString();
                    banner.RestoreEditorState(state);
                    banner.OrderValuesChanged += delegate { UpdateBannerOrderValues(orderItem, banner); };
                }
            }
            if (m_orderEditor == nextEditor) return;
            nextEditor.Dock = DockStyle.Fill;
            panelEdit.SuspendLayout();
            try
            {
                if (m_orderEditor != null) panelEdit.Controls.Remove(m_orderEditor);
                m_orderEditor = nextEditor;
                m_orderEditorCategory = editorCategory;
                panelEdit.Controls.Add(m_orderEditor);
            }
            finally { panelEdit.ResumeLayout(true); }
        }

        private void UpdateBannerOrderValues(DataRow row, UcOrder현수막 editor)
        {
            if (IsDisposed || row.RowState == DataRowState.Detached || row.RowState == DataRowState.Deleted) return;
            row["WidthMm"] = editor.WidthMm;
            row["HeightMm"] = editor.HeightMm;
            row["Qty"] = editor.Quantity;
            row["AreaQty"] = editor.AreaQuantity;
            string unit = row["UnitName"].ToString().Trim();
            bool byArea = string.Equals(row["BillingBasis"].ToString().Trim(), "면적", StringComparison.Ordinal)
                || unit == "회배" || unit == "회베" || unit == "㎡" || unit == "m²" || unit == "m2";
            decimal billingQty = byArea && editor.WidthMm > 0 && editor.HeightMm > 0 ? editor.AreaQuantity : editor.Quantity;
            decimal supply = Math.Round(GetDecimal(row["UnitPrice"]) * billingQty, 0, MidpointRounding.AwayFromZero);
            decimal vat = Math.Round(supply * .1m, 0, MidpointRounding.AwayFromZero);
            row["SupplyAmount"] = supply;
            row["VatAmount"] = vat;
            row["TotalAmount"] = supply + vat;
            UpdateOrderItemTotals();
            FGrid1.Refresh();
        }

        private void RemoveDeletedOrderEditors()
        {
            if (m_orderItemEditors == null) return;
            foreach (DataRow row in new List<DataRow>(m_orderItemEditors.Keys))
            {
                if (row.RowState != DataRowState.Detached && row.RowState != DataRowState.Deleted) continue;
                UserControl editor = m_orderItemEditors[row];
                panelEdit.Controls.Remove(editor);
                if (m_orderEditor == editor) m_orderEditor = null;
                editor.Dispose();
                m_orderItemEditors.Remove(row);
            }
        }

        private void DisposeOrderEditors()
        {
            if (m_orderItemEditors != null)
            {
                foreach (UserControl editor in m_orderItemEditors.Values) editor.Dispose();
                m_orderItemEditors.Clear();
            }
            if (m_defaultOrderEditor != null) m_defaultOrderEditor.Dispose();
        }

        private void InitOrderItemTable()
        {
            m_orderItems = new DataTable();
            m_orderItems.Columns.Add("LineNo", typeof(int));
            m_orderItems.Columns.Add("PriceCategory", typeof(string));
            m_orderItems.Columns.Add("CategoryName", typeof(string));
            m_orderItems.Columns.Add("ApplyFrom", typeof(DateTime));
            m_orderItems.Columns.Add("OrderItemId", typeof(long));
            m_orderItems.Columns.Add("PriceItemSeqNo", typeof(long));
            m_orderItems.Columns.Add("PriceItemPriceId", typeof(long));
            m_orderItems.Columns.Add("ItemCode", typeof(string));
            m_orderItems.Columns.Add("ItemName", typeof(string));
            m_orderItems.Columns.Add("CustomName", typeof(string));
            m_orderItems.Columns.Add("OrderName", typeof(string));
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
            m_orderItems.Columns.Add("EditorStateJson", typeof(string));
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
            FGrid1.AllowEditing = true;

            // SetColumn 호출 순서대로 실제 그리드 컬럼을 배치한다.
            // 컬럼의 순서를 변경하려면 호출 순서를 바꾸면 됨
            int columnIndex = 0;
            SetColumn(ref columnIndex, "LineNo", "순번", 35, TextAlignEnum.CenterCenter, true);
            SetColumn(ref columnIndex, "ItemCode", "품목코드", 70, TextAlignEnum.CenterCenter, true);
            SetColumn(ref columnIndex, "ItemName", "품목명", 150, TextAlignEnum.LeftCenter, true);
            SetColumn(ref columnIndex, "OrderName", "주문명", 170, TextAlignEnum.LeftCenter, true);
            SetColumn(ref columnIndex, "UnitPrice", "단가", 70, TextAlignEnum.RightCenter, true, "#,##0.##");
            SetColumn(ref columnIndex, "Qty", "수량", 80, TextAlignEnum.RightCenter, true, "#,##0.##");
            SetColumn(ref columnIndex, "TotalAmount", "합계금액", 90, TextAlignEnum.RightCenter, true, "#,##0.##");
            SetColumn(ref columnIndex, "PriceCategory", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "CategoryName", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "ApplyFrom", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "SpecText", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "UnitName", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "BillingBasis", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "PriceName", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "Remark", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "OrderItemId", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "PriceItemSeqNo", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "PriceItemPriceId", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "CustomName", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "WidthMm", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "HeightMm", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "AreaQty", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "SupplyAmount", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "VatAmount", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "RequestFilePath", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "OutputFilePath", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "EditorStateJson", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "ProcessRules", string.Empty, 0, TextAlignEnum.LeftCenter, false);

            // 주문명은 화면 입력용으로만 사용한다. 저장 컬럼 연결은 별도로 처리한다.
            FGrid1.Cols["OrderName"].AllowEditing = true;

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
            if (FGrid1.Col == FGrid1.Cols["OrderName"].Index)
            {
                return;
            }

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

            DialogPriceItemOrderItem item = CreateDialogOrderItem(row);
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

        private DialogPriceItemOrderItem CreateDialogOrderItem(DataRow row)
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
            row["OrderName"] = item.CustomName;
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
            UserControl cached;
            if (m_orderItemEditors != null && m_orderItemEditors.TryGetValue(row, out cached))
            {
                var banner = cached as UcOrder현수막;
                if (banner != null)
                {
                    var state = banner.CaptureEditorState();
                    state.ZoomType = null;
                    state.WidthMm = GetDecimal(row["WidthMm"]); state.HeightMm = GetDecimal(row["HeightMm"]);
                    state.Quantity = GetDecimal(row["Qty"]);
                    state.SourceFilePath = row["RequestFilePath"] == DBNull.Value ? null : row["RequestFilePath"].ToString();
                    row["EditorStateJson"] = state.ToJson();
                }
                panelEdit.Controls.Remove(cached);
                if (m_orderEditor == cached) m_orderEditor = null;
                cached.Dispose(); m_orderItemEditors.Remove(row);
            }
            UpdateOrderItemTotals();
            UpdateSelectedOrderEditor();
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

            using (DialogPriceItemList dialog = new DialogPriceItemList(itemName, m_sSelectedCustCode, tbCustName.Text))
            {
                PositionPriceItemDialog(dialog);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                AddOrderItem(new DialogPriceItemOrderItem
                {
                    PriceItemSeqNo = dialog.SelectedPriceItemSeqNo,
                    PriceItemPriceId = dialog.SelectedPriceItemPriceId,
                    ItemCode = dialog.SelectedItemCode,
                    PriceItemName = dialog.SelectedPriceItemName,
                    CustomName = dialog.SelectedPriceItemName,
                    SpecText = dialog.SelectedSpecText,
                    UnitName = dialog.SelectedUnitName,
                    BillingBasis = dialog.SelectedBillingBasis,
                    PriceName = dialog.SelectedPriceName,
                    UnitPrice = dialog.SelectedUnitPrice,
                    Remark = dialog.SelectedRemark
                }, null);

                DataRow row = m_orderItems.Rows[m_orderItems.Rows.Count - 1];
                row["PriceCategory"] = dialog.SelectedPriceCategory;
                row["CategoryName"] = dialog.SelectedCategoryName;
                row["ApplyFrom"] = (object)GetDateValue(dialog.SelectedApplyFrom) ?? DBNull.Value;
                FGrid1.Row = FGrid1.Rows.Fixed + m_orderItems.Rows.Count - 1;
                UpdateSelectedOrderEditor();
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
            RemoveDeletedOrderEditors();
            for (int i = 0; i < m_orderItems.Rows.Count; i++)
            {
                m_orderItems.Rows[i]["LineNo"] = i + 1;
            }
            UpdateOrderItemTotals();
            UpdateSelectedOrderEditor();
        }

        private void UpdateOrderItemTotals()
        {
            FGrid1.Subtotal(AggregateEnum.Clear);
            if (!FGrid1.Cols["SupplyAmount"].Visible && !FGrid1.Cols["VatAmount"].Visible && !FGrid1.Cols["TotalAmount"].Visible)
            {
                return;
            }

            FGrid1.SubtotalPosition = SubtotalPositionEnum.BelowData;
            foreach (string columnName in new[] { "SupplyAmount", "VatAmount", "TotalAmount" })
            {
                if (FGrid1.Cols[columnName].Visible)
                {
                    FGrid1.Subtotal(AggregateEnum.Sum, -1, -1, FGrid1.Cols[columnName].Index, "합계");
                }
            }

            if (FGrid1.Rows.Count > FGrid1.Rows.Fixed)
            {
                int totalRow = FGrid1.Rows.Count - 1;
                if (FGrid1.Rows[totalRow].IsNode)
                {
                    FGrid1[totalRow, "ItemName"] = "금액 합계";
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
            FGrid1.FinishEditing();
            BindingContext[m_orderItems].EndCurrentEdit();
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

            string firstItemName = m_orderItems.Rows[0]["OrderName"].ToString();
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
                UserControl cached;
                if (m_orderItemEditors != null && m_orderItemEditors.TryGetValue(row, out cached))
                {
                    var banner = cached as UcOrder현수막;
                    if (banner != null)
                    {
                        var state = banner.CaptureEditorState();
                        if (state.WidthMm != GetDecimal(row["WidthMm"]) || state.HeightMm != GetDecimal(row["HeightMm"]) || state.Quantity != GetDecimal(row["Qty"]))
                            UpdateBannerOrderValues(row, banner);
                        row["EditorStateJson"] = state.ToJson();
                        row["RequestFilePath"] = (object)state.SourceFilePath ?? DBNull.Value;
                    }
                }
                decimal? width = GetStoredDecimal(row["WidthMm"]);
                decimal? height = GetStoredDecimal(row["HeightMm"]);
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
                    CustomName = row["OrderName"].ToString(),
                    SpecText = row["SpecText"].ToString(),
                    BillingBasis = row["BillingBasis"].ToString(),
                    WidthMm = width,
                    HeightMm = height,
                    AreaPerQty = areaPerQty,
                    Qty = GetStoredDecimal(row["Qty"]),
                    AreaQty = GetStoredDecimal(row["AreaQty"]),
                    UnitPrice = GetStoredDecimal(row["UnitPrice"]),
                    SupplyAmount = GetDecimal(row["SupplyAmount"]),
                    VatAmount = GetDecimal(row["VatAmount"]),
                    TotalAmount = GetDecimal(row["TotalAmount"]),
                    ItemDescription = row["Remark"].ToString(),
                    ProductionStatus = m_productionStatus,
                    EditorStateJson = row["EditorStateJson"] == DBNull.Value ? null : row["EditorStateJson"].ToString(),
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

            ApplyLoadedOrder(row);
            LoadOrderItems();
            m_isInitializing = false;
        }

        private void ApplyLoadedOrder(DataRow row)
        {
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

        }

        private void LoadOrderItems()
        {
            DataTable items = OrderRepository.GetOrderItemsForEdit(m_orderId);
            items.Columns.Add("ProcessRules", typeof(System.Collections.Generic.List<OrderProcessRuleData>));
            foreach (DataRow item in items.Rows)
                item["ProcessRules"] = OrderProcessRuleRepository.GetRules(m_orderId, GetLong(item["OrderItemId"]));
            ApplyLoadedOrderItems(items);
        }

        private void ApplyLoadedOrderItems(DataTable items)
        {
            m_isLoadingOrderItems = true;
            try
            {
                m_orderItems.Rows.Clear();

                foreach (DataRow row in items.Rows)
                {
                    DialogPriceItemOrderItem item = CreateDialogOrderItem(row);
                    item.Remark = row["ItemDescription"].ToString();
                    if (row.Table.Columns.Contains("ProcessRules") && row["ProcessRules"] != DBNull.Value)
                        item.ProcessRules = (System.Collections.Generic.List<OrderProcessRuleData>)row["ProcessRules"];

                    AddOrderItem(item, GetNullableLong(row["OrderItemId"]));
                    DataRow addedRow = m_orderItems.Rows[m_orderItems.Rows.Count - 1];
                    addedRow["PriceCategory"] = row["PriceCategory"];
                    addedRow["CategoryName"] = row["CategoryName"];
                    addedRow["ApplyFrom"] = row["ApplyFrom"];
                    addedRow["OrderName"] = row["CustomName"];
                    if (row.Table.Columns.Contains("EditorStateJson")) addedRow["EditorStateJson"] = row["EditorStateJson"];
                }
            }
            finally { m_isLoadingOrderItems = false; }

            if (m_orderItems.Rows.Count > 0) FGrid1.Row = FGrid1.Rows.Fixed;
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

            foreach (DataRow row in m_orderItems.Rows)
            {
                if (row["OrderName"].ToString().Length > 50)
                {
                    MessageBox.Show("주문명은 50자 이내로 입력해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }
            if (m_orderItemEditors != null)
                foreach (UserControl editor in m_orderItemEditors.Values)
                {
                    var banner = editor as UcOrder현수막;
                    if (banner != null && banner.IsProcessing)
                    {
                        MessageBox.Show("파일 처리가 완료된 후 저장해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
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

        private decimal? GetStoredDecimal(object value)
        {
            decimal number;
            return value != null && value != DBNull.Value && decimal.TryParse(value.ToString(), out number) ? (decimal?)number : null;
        }

        private decimal? GetNullableDecimal(object value)
        {
            decimal result = GetDecimal(value);
            return result > 0M ? (decimal?)result : null;
        }
    }
}
