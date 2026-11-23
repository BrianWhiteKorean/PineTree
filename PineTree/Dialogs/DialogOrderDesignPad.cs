using C1.Win.C1FlexGrid;
using PineTree.Classes;
using PineTree.UserControlsOrder;
using PineTree.Dialogs;
using System.Collections.Generic;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace PineTree.Dialogs
{
    public partial class DialogOrderDesignPad : Form
    {
        private DialogMode m_DialogMode = DialogMode.New;
        private long m_orderId = -1;
        private bool m_isInitializing;
        private Dictionary<string, decimal> m_deliveryFees;
        private bool m_isSaving;
        private bool m_isLoadingOrderItems;
        private string m_sSelectedCustCode;
        private string m_productionStatus = OrderProductionStatusHelper.ToDbText(OrderProductionStatus.New);
        private DataTable m_orderItems;
        private string m_orderEditorCategory;
        private UserControl m_orderEditor;
        private Dictionary<DataRow, UserControl> m_orderItemEditors;
        private UserControl m_defaultOrderEditor;
        private Dictionary<string, UserControl> m_materialEditors;
        private Dictionary<string, DataRow> m_materialRows;
        private OrderSourceDocument m_sourceDocument;
        private OrderSourcePreview m_sourcePreview;
        private bool m_loadingProductSelection;
        private int m_priceCategoryRequestVersion;
        private int m_categoryRequestVersion;
        private int m_itemRequestVersion;
        private int? m_selectedEquipmentId;

        public event EventHandler OrderSaved;
        public event EventHandler CancelRequested;

        public bool OrderFound { get; private set; } = true;

        public DialogOrderDesignPad()
        {
            InitializeComponent();
            ApplyDesignPadVisualStyle();
            InitializeOrderPricing();
            tbOrderNo.Text = "저장 시 자동 할당됩니다";
            InitOrderItemTable();
            InitOrderEditor();
            InitPreviewEvent();
            InitOrderCombos();

            dtmOrder.Value = dtmDelivery.Value = DateTime.Today;
        }

        public DialogOrderDesignPad(long seqNo)
        {
            InitializeComponent();
            ApplyDesignPadVisualStyle();
            InitializeOrderPricing();
            tbOrderNo.Text = "저장 시 자동 할당됩니다";
            InitOrderItemTable();
            InitOrderEditor();
            InitPreviewEvent();
            InitOrderCombos();

            m_orderId = seqNo;
            inputGroupHeader1.Text = "주문 수정";
            m_DialogMode = DialogMode.Edit;
            LoadOrder();
            Shown += delegate { if (!OrderFound) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        private void ApplyDesignPadVisualStyle()
        {
            c1SplitContainer1.UseParentVisualStyle = false;
            c1SplitContainer1.VisualStyle = C1.Win.C1SplitContainer.VisualStyle.Office2010Black;
        }

        private static void InheritDesignPadVisualStyle(Control control)
        {
            var splitContainer = control as C1.Win.C1SplitContainer.C1SplitContainer;
            if (splitContainer != null) splitContainer.UseParentVisualStyle = true;
            foreach (Control child in control.Controls) InheritDesignPadVisualStyle(child);
        }

        public void ResetNewOrder()
        {
            m_isInitializing = true;
            m_isLoadingOrderItems = true;
            try
            {
                panelEdit.Controls.Clear();
                DisposeOrderEditors();
                m_orderEditor = null;
                m_defaultOrderEditor = null;
                m_orderEditorCategory = null;
                m_orderItems.Clear();
                if (m_sourceDocument != null) m_sourceDocument.Reset();
                m_DialogMode = DialogMode.New;
                m_hasDesignPadState = false;
                m_orderId = -1;
                m_savedSourcePreview = null;
                OrderFound = true;
                m_sSelectedCustCode = null;
                m_productionStatus = OrderProductionStatusHelper.ToDbText(OrderProductionStatus.New);
                inputGroupHeader1.Text = "주문 등록";
                tbOrderNo.Text = "저장 시 자동 할당됩니다";
                tbOrderName.Text = string.Empty;
                tbCustName.Text = tbCustCode.Text = tbRemark.Text = tbSpec.Text = tbAddress.Text = tbZipCode.Text = string.Empty;
                tbDeliveryFee.Value = 0;
                dtmOrder.Value = dtmDelivery.Value = DateTime.Today;
                cbChannel.SelectedIndex = cbChannel.Items.Count > 0 ? 0 : -1;
                cbDeliveryType.SelectedIndex = cbDeliveryType.Items.Count > 0 ? 0 : -1;
            }
            finally
            {
                m_isLoadingOrderItems = false;
                m_isInitializing = false;
            }
            ApplySelectedDeliveryFee();
            ResetOrderPricing();
            UpdateOrderItemTotals();
            UpdateSelectedOrderEditor();
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
            ApplySelectedDeliveryFee();
        }

        private void LoadTextCombo(C1.Win.C1InputPanel.InputComboBox comboBox, DataRow[] source)
        {
            comboBox.Items.Clear();
            bool delivery = ReferenceEquals(comboBox, cbDeliveryType);
            if (delivery) m_deliveryFees = new Dictionary<string, decimal>(StringComparer.Ordinal);

            foreach (DataRow row in source)
            {
                string text = row["ItemName"].ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text) && !comboBox.Items.ContainsText(text))
                {
                    comboBox.Items.AddText(text);
                    if (delivery)
                    {
                        decimal fee;
                        string value = row.Table.Columns.Contains("ItemValue") ? Convert.ToString(row["ItemValue"]) : string.Empty;
                        m_deliveryFees[text] = decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture, out fee) && fee >= 0m ? fee : 0m;
                    }
                }
            }

            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
        }

        private void ApplySelectedDeliveryFee()
        {
            if (m_isInitializing || !cbDeliveryType.Enabled) return;
            decimal fee;
            tbDeliveryFee.Value = m_deliveryFees != null && m_deliveryFees.TryGetValue(cbDeliveryType.Text.Trim(), out fee) ? fee : 0m;
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
            btn90CW.Click += delegate { TransformCurrentSource(90, false); };
            btn90CCW.Click += delegate { TransformCurrentSource(-90, false); };
            btnFlip.Click += delegate { TransformCurrentSource(0, true); };
            UpdateSourceTransformButtons();
            btnSave.Click += btnSave_Click;
            btnCancel.Click += btnCancel_Click;
            btnCustList.Click += btnCustList_Click;
            cbDeliveryType.SelectedIndexChanged += delegate { ApplySelectedDeliveryFee(); };
            btnAdd.Click += btnAdd_Click;
            c1Button2.Click += btnRemove_Click;
            FGrid1.DoubleClick += FGrid1_DoubleClick;
            FGrid1.AfterRowColChange += delegate { UpdateSelectedOrderEditor(); };
            FGrid1.AfterEdit += delegate(object sender, RowColEventArgs e) {
                if (e.Col != FGrid1.Cols["Qty"].Index) return;
                DataRow row = GetAccessoryRow(e.Row);
                if (row == null) return;
                row["Qty"] = Math.Max(0m, Math.Round(GetDecimal(FGrid1[e.Row, e.Col]), 0, MidpointRounding.AwayFromZero));
                CalculateAccessoryAmounts(row);
                UpdateOrderItemTotals();
            };

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
            m_materialEditors = new Dictionary<string, UserControl>();
            m_materialRows = new Dictionary<string, DataRow>();
            m_orderItemEditors = new Dictionary<DataRow, UserControl>();
            m_sourceDocument = new OrderSourceDocument { UseSavedPreviewsOnly = true };
            m_sourcePreview = new OrderSourcePreview(panelPreview, pictureBox1, panelCanvas, tbFileInfo);
            m_sourcePreview.Bind(m_sourceDocument);
            m_sourceDocument.Changed += delegate { UpdateSourceTransformButtons(); };
            cbCategory.DropDownStyle = C1.Win.C1InputPanel.InputComboBoxStyle.DropDownList;
            cbCategoryName.DropDownStyle = C1.Win.C1InputPanel.InputComboBoxStyle.DropDownList;
            cbItem.DropDownStyle = C1.Win.C1InputPanel.InputComboBoxStyle.DropDownList;
            BindProductCombo(cbCategory, null, "PriceCategory");
            BindProductCombo(cbCategoryName, null, "CategoryName");
            BindProductCombo(cbItem, null, "PriceItemName");
            foreach (var radio in new[] { rbEquipment1, rbEquipment2 })
                radio.CheckedChanged += EquipmentRadio_CheckedChanged;
            cbCategory.SelectedIndexChanged += PriceCategory_SelectedIndexChanged;
            cbCategoryName.SelectedIndexChanged += CategoryName_SelectedIndexChanged;
            cbItem.SelectedIndexChanged += async delegate {
                UpdateSpecEnabled();
                if (!m_loadingProductSelection && !m_isInitializing) await ReloadSelectedProductPriceAsync();
            };
            Load += async delegate
            {
                if (cbCategory.Items.Count == 0 && !string.IsNullOrEmpty(GetSelectedEquipmentName()))
                    await ReloadPriceCategoriesAsync();
            };
            Shown += async delegate
            {
                if (m_hasDesignPadState) await m_sourceDocument.EnsureLoadedAsync();
            };
            Disposed += delegate
            {
                ++m_priceCategoryRequestVersion;
                ++m_categoryRequestVersion;
                ++m_itemRequestVersion;
                m_sourcePreview.Dispose();
                DisposeOrderEditors();
                m_sourceDocument.Dispose();
            };
            UpdateSelectedOrderEditor();
        }

        private void UpdateSelectedOrderEditor()
        {
            if (m_orderItemEditors == null || m_isLoadingOrderItems) return;
            if (m_DialogMode == DialogMode.Edit && !m_hasDesignPadState) { ShowStoredOrderItem(); return; }
            string category = cbCategoryName.SelectedIndex >= 0 ? cbCategoryName.Text.Trim() : null;
            string editorKey = OrderItemEditorFactory.GetEditorKey(GetSelectedPriceCategory(), category);
            UserControl editor = null;
            if (editorKey != null)
            {
                if (!m_materialEditors.TryGetValue(editorKey, out editor) || editor.IsDisposed)
                {
                    editor = OrderItemEditorFactory.Create(editorKey);
                    ((IOrderSourceEditor)editor).AttachSourceDocument(m_sourceDocument);
                    m_materialEditors[editorKey] = editor;
                    var sourceEditor = (IOrderSourceEditor)editor;
                    sourceEditor.SourceViewChanged += delegate {
                        if (ReferenceEquals(m_orderEditor, editor)) UpdateSourceTransformPreview();
                    };
                    var materialEditor = (IOrderItemEditor)editor;
                    materialEditor.OrderValuesChanged += delegate
                    {
                        DataRow row;
                        if (m_materialRows.TryGetValue(editorKey, out row)) UpdateBannerOrderValues(row, materialEditor);
                        if (ReferenceEquals(m_orderEditor, editor)) UpdateOrderPriceSummary(true);
                    };
                }
            }
            if (m_orderEditor == editor) { UpdateOrderPriceSummary(true); UpdateSourceTransformPreview(); return; }
            panelEdit.SuspendLayout();
            try
            {
                panelEdit.Controls.Clear();
                m_orderEditor = editor;
                m_orderEditorCategory = category;
                if (editor != null)
                {
                    editor.Dock = DockStyle.Fill;
                    panelEdit.Controls.Add(editor);
                    InheritDesignPadVisualStyle(editor);
                }
            }
            finally { panelEdit.ResumeLayout(true); }
            UpdateOrderPriceSummary(true);
            UpdateSourceTransformPreview();
        }

        // Restore each saved item with its own source document and artboard settings.
        private void ShowStoredOrderItem()
        {
            DataRow row = GetAccessoryRow(FGrid1.Row);
            if (row == null) return;
            if (string.IsNullOrWhiteSpace(Convert.ToString(row["EditorStateJson"]))
                && string.IsNullOrWhiteSpace(Convert.ToString(row["RequestFilePath"]))) return;
            m_loadingProductSelection = true;
            try
            {
                SelectComboText(cbCategory, Convert.ToString(row["PriceCategory"]));
                SelectComboText(cbCategoryName, Convert.ToString(row["CategoryName"]));
                SelectComboText(cbItem, Convert.ToString(row["ItemName"]));
                cbCategory.Enabled = cbCategoryName.Enabled = cbItem.Enabled = false;
                rbEquipment1.Enabled = rbEquipment2.Enabled = false;
                tbSpec.Enabled = true;
            }
            finally { m_loadingProductSelection = false; }
            ShowOrderItemEditor(Convert.ToString(row["CategoryName"]).Trim(), row);
            UpdateOrderPriceSummary(true);
        }

        private void ShowOrderItemEditor(string categoryName, DataRow orderItem)
        {
            string priceCategory = Convert.ToString(orderItem?["PriceCategory"]).Trim();
            string editorCategory = OrderItemEditorFactory.GetEditorKey(priceCategory, categoryName);
            UserControl nextEditor;
            if (orderItem == null || editorCategory == null)
            {
                if (m_defaultOrderEditor == null) m_defaultOrderEditor = new UcOrderBase();
                nextEditor = m_defaultOrderEditor;
            }
            else if (!m_orderItemEditors.TryGetValue(orderItem, out nextEditor))
            {
                nextEditor = OrderItemEditorFactory.Create(editorCategory);
                m_orderItemEditors.Add(orderItem, nextEditor);
                var banner = nextEditor as IOrderItemEditor;
                if (banner != null)
                {
                    string json = orderItem["EditorStateJson"].ToString();
                    var state = BannerOrderEditorState.FromJson(json) ?? new BannerOrderEditorState {
                        WidthMm = GetDecimal(orderItem["WidthMm"]), HeightMm = GetDecimal(orderItem["HeightMm"]),
                        Quantity = GetDecimal(orderItem["Qty"]),
                        SourceFilePath = orderItem["RequestFilePath"] == DBNull.Value ? null : orderItem["RequestFilePath"].ToString()
                    };
                    // Typed item columns remain authoritative for legacy item-edit dialogs.
                    if (state.WidthMm != GetDecimal(orderItem["WidthMm"]) || state.HeightMm != GetDecimal(orderItem["HeightMm"]))
                        state.OutputServerFile = null;
                    if (state.ZoomType != "FIXED_SIZE" && (state.WidthMm != GetDecimal(orderItem["WidthMm"]) || state.HeightMm != GetDecimal(orderItem["HeightMm"]))) state.ZoomType = null;
                    state.WidthMm = GetDecimal(orderItem["WidthMm"]);
                    state.HeightMm = GetDecimal(orderItem["HeightMm"]);
                    state.Quantity = GetDecimal(orderItem["Qty"]);
                    string storedSourcePath = orderItem["RequestFilePath"] == DBNull.Value ? null : orderItem["RequestFilePath"].ToString();
                    if (!string.Equals(state.SourceFilePath, storedSourcePath, StringComparison.OrdinalIgnoreCase))
                        state.SourceServerFile = state.OutputServerFile = null;
                    state.SourceFilePath = storedSourcePath;
                    var storedSource = nextEditor as IOrderSourceEditor;
                    if (storedSource != null) storedSource.SourceDocument.UseSavedPreviewsOnly = true;
                    banner.RestoreEditorState(state);
                    if (storedSource != null) storedSource.SourceDocument.SetSavedPreview(m_savedSourcePreview);
                    banner.OrderValuesChanged += delegate { UpdateBannerOrderValues(orderItem, banner); };
                    var source = nextEditor as IOrderSourceEditor;
                    if (source != null) {
                        source.SourceViewChanged += delegate { if (ReferenceEquals(m_orderEditor, source)) UpdateSourceTransformPreview(); };
                        source.SourceDocument.Changed += delegate { UpdateSourceTransformButtons(); };
                    }
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
                InheritDesignPadVisualStyle(m_orderEditor);
                var sourceEditor = m_orderEditor as IOrderSourceEditor;
                m_sourcePreview.Bind(sourceEditor == null ? null : sourceEditor.SourceDocument);
            }
            finally { panelEdit.ResumeLayout(true); }
            UpdateSourceTransformPreview();
            UpdateOrderPriceSummary(true);
        }

        private void UpdateSourceTransformButtons()
        {
            var editor = m_orderEditor as IOrderSourceEditor;
            var document = editor == null ? null : editor.SourceDocument;
            bool ready = document != null && document.CurrentImage != null
                && !editor.IsProcessing && !document.OutputBusy && !document.IsPreviewBusy;
            btn90CW.Enabled = btn90CCW.Enabled = btnFlip.Enabled = ready;
        }

        private void UpdateSourceTransformPreview()
        {
            var editor = m_orderEditor as IOrderSourceEditor;
            if (m_sourcePreview != null)
                m_sourcePreview.SetTransform(editor == null ? 0 : editor.SourceRotationDegreesClockwise,
                    editor != null && editor.SourceFlipHorizontal);
            UpdateSourceTransformButtons();
        }

        private void TransformCurrentSource(int clockwiseDegrees, bool flip)
        {
            UpdateSourceTransformButtons();
            if (!btn90CW.Enabled) return;
            var editor = (IOrderSourceEditor)m_orderEditor;
            if (flip) editor.FlipSourceHorizontally();
            else editor.RotateSource(clockwiseDegrees);
            UpdateSourceTransformPreview();
        }

        private string GetSelectedPriceCategory()
        {
            return cbCategory.SelectedIndex >= 0 ? cbCategory.Text.Trim() : null;
        }

        private string GetSelectedEquipmentName()
        {
            var radio = rbEquipment1.Checked ? rbEquipment1 : rbEquipment2.Checked ? rbEquipment2 : null;
            return radio == null ? null : radio.Text.Replace("&&", "&").Trim();
        }

        private async void EquipmentRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (m_loadingProductSelection) return;
            var radio = sender as C1.Win.C1InputPanel.InputRadioButton;
            if (radio == null || !radio.Checked) return;
            await ReloadPriceCategoriesAsync();
        }

        private async void PriceCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_loadingProductSelection) return;
            await ReloadCategoryNamesAsync();
        }

        private async Task ReloadPriceCategoriesAsync()
        {
            int version = ++m_priceCategoryRequestVersion;
            ++m_categoryRequestVersion;
            ++m_itemRequestVersion;
            m_selectedEquipmentId = null;
            string equipmentName = GetSelectedEquipmentName();
            BindProductCombo(cbCategory, null, "PriceCategory");
            BindProductCombo(cbCategoryName, null, "CategoryName");
            BindProductCombo(cbItem, null, "PriceItemName");
            UpdateSelectedOrderEditor();
            if (string.IsNullOrEmpty(equipmentName)) return;
            try
            {
                int? equipmentId = await Task.Run(() =>
                {
                    using (DataTable equipment = PriceItemRepository.GetEquipment())
                        foreach (DataRow row in equipment.Rows)
                            if (string.Equals(Convert.ToString(row["EquipmentName"]).Trim(), equipmentName, StringComparison.Ordinal))
                                return (int?)Convert.ToInt32(row["EquipmentId"]);
                    return null;
                });
                if (IsDisposed || Disposing || version != m_priceCategoryRequestVersion) return;
                if (!equipmentId.HasValue)
                    throw new InvalidOperationException("선택한 장비가 활성 장비 목록에 등록되어 있지 않습니다: " + equipmentName);
                using (DataTable categories = await Task.Run(() => PriceItemRepository.GetPriceCategories(equipmentId)))
                {
                    if (IsDisposed || Disposing || version != m_priceCategoryRequestVersion) return;
                    m_selectedEquipmentId = equipmentId;
                    BindProductCombo(cbCategory, categories, "PriceCategory");
                }
                await ReloadCategoryNamesAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing && version == m_priceCategoryRequestVersion)
                    MessageBox.Show(this, "장비별 카테고리를 조회하지 못했습니다.\r\n" + ex.Message, "제품 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void CategoryName_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_loadingProductSelection) return;
            UpdateSelectedOrderEditor();
            await ReloadItemNamesAsync();
        }

        private async Task ReloadCategoryNamesAsync()
        {
            int version = ++m_categoryRequestVersion;
            ++m_itemRequestVersion;
            int? equipmentId = m_selectedEquipmentId;
            string priceCategory = GetSelectedPriceCategory();
            BindProductCombo(cbCategoryName, null, "CategoryName");
            BindProductCombo(cbItem, null, "PriceItemName");
            UpdateSelectedOrderEditor();
            if (!equipmentId.HasValue || string.IsNullOrEmpty(priceCategory)) return;
            try
            {
                using (DataTable categories = await Task.Run(() => PriceItemRepository.GetCategoryNames(priceCategory, equipmentId)))
                {
                    if (IsDisposed || Disposing || version != m_categoryRequestVersion) return;
                    BindProductCombo(cbCategoryName, categories, "CategoryName");
                }
                UpdateSelectedOrderEditor();
                await ReloadItemNamesAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing && version == m_categoryRequestVersion)
                    MessageBox.Show(this, "제품 분류를 조회하지 못했습니다.\r\n" + ex.Message, "제품 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task ReloadItemNamesAsync()
        {
            int version = ++m_itemRequestVersion;
            int? equipmentId = m_selectedEquipmentId;
            string priceCategory = GetSelectedPriceCategory();
            string categoryName = cbCategoryName.SelectedIndex >= 0 ? cbCategoryName.Text.Trim() : null;
            BindProductCombo(cbItem, null, "PriceItemName");
            if (!equipmentId.HasValue || string.IsNullOrEmpty(priceCategory) || string.IsNullOrEmpty(categoryName)) return;
            try
            {
                using (DataTable items = await Task.Run(() => PriceItemRepository.GetPriceItemNames(priceCategory, categoryName, equipmentId)))
                {
                    if (IsDisposed || Disposing || version != m_itemRequestVersion) return;
                    BindProductCombo(cbItem, items, "PriceItemName");
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing && version == m_itemRequestVersion)
                    MessageBox.Show(this, "품목 목록을 조회하지 못했습니다.\r\n" + ex.Message, "제품 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateSpecEnabled()
        {
            tbSpec.Enabled = true;
            tbSpec.ReadOnly = true;
        }

        private void BindProductCombo(C1.Win.C1InputPanel.InputComboBox combo, DataTable source, string columnName)
        {
            bool wasLoading = m_loadingProductSelection;
            m_loadingProductSelection = true;
            try
            {
                combo.Items.Clear();
                bool isItemCombo = ReferenceEquals(combo, cbItem);
                if (isItemCombo) combo.Items.AddText("[선택]");
                if (source != null)
                    foreach (DataRow row in source.Rows)
                    {
                        string text = Convert.ToString(row[columnName]).Trim();
                        if (!string.IsNullOrEmpty(text) && !combo.Items.ContainsText(text)) combo.Items.AddText(text);
                    }
                combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
                combo.Enabled = combo.Items.Count > (isItemCombo ? 1 : 0);
                if (isItemCombo) { UpdateSpecEnabled(); InvalidateSelectedProductPrice(); }
            }
            finally { m_loadingProductSelection = wasLoading; }
        }

        // Explicitly adding a product attaches the current material draft to that row.
        // Merely moving the grid selection never changes or replaces the material editor.
        private void BindMaterialRow(DataRow row)
        {
            string priceCategory = Convert.ToString(row["PriceCategory"]).Trim();
            string category = Convert.ToString(row["CategoryName"]).Trim();
            string editorKey = OrderItemEditorFactory.GetEditorKey(priceCategory, category);
            UserControl control;
            if (editorKey == null || !m_materialEditors.TryGetValue(editorKey, out control) || control.IsDisposed) return;
            DataRow previous;
            if (m_materialRows.TryGetValue(editorKey, out previous) && previous != row)
            {
                if (previous.RowState != DataRowState.Detached && previous.RowState != DataRowState.Deleted)
                    previous["EditorStateJson"] = ((IOrderItemEditor)control).CaptureEditorState().ToJson();
                m_orderItemEditors.Remove(previous);
            }
            m_materialRows[editorKey] = row;
            m_orderItemEditors[row] = control;
            UpdateBannerOrderValues(row, (IOrderItemEditor)control);
        }

        private void UpdateBannerOrderValues(DataRow row, IOrderItemEditor editor)
        {
            if (IsDisposed || row.RowState == DataRowState.Detached || row.RowState == DataRowState.Deleted) return;
            row["WidthMm"] = editor.WidthMm;
            row["HeightMm"] = editor.HeightMm;
            row["Qty"] = editor.Quantity;
            row["AreaQty"] = editor.AreaQuantity;
            row["SpecText"] = OrderDesignPadState.FormatSpecification(editor.WidthMm, editor.HeightMm, editor.Quantity);
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
                foreach (string category in new List<string>(m_materialRows.Keys))
                    if (m_materialRows[category] == row) m_materialRows.Remove(category);
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
            foreach (var editor in m_materialEditors.Values) editor.Dispose();
            m_materialEditors.Clear(); m_materialRows.Clear();
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
            SetColumn(ref columnIndex, "ItemCode", "품목코드", 70, TextAlignEnum.CenterCenter, false);
            SetColumn(ref columnIndex, "ItemName", "품목명", 150, TextAlignEnum.LeftCenter, true);
            SetColumn(ref columnIndex, "OrderName", string.Empty, 0, TextAlignEnum.LeftCenter, false);
            SetColumn(ref columnIndex, "UnitPrice", "단가", 70, TextAlignEnum.RightCenter, true, "#,##0.##");
            SetColumn(ref columnIndex, "Qty", "수량", 40, TextAlignEnum.RightCenter, true, "#,##0");
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

            FGrid1.Cols["Qty"].AllowEditing = true;

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

        private void btnAddress_Click(object sender, EventArgs e)
        {
            using (var dialog = new DialogAddressSearch())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var address = dialog.SelectedAddress;
                tbAddress.Text = address.RoadAddress;
                tbZipCode.Text = address.ZipCode;
            }
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
                RefreshSelectedProductPrice();
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
            RefreshSelectedProductPrice();
        }

        private void ClearSelectedCustomer()
        {
            m_sSelectedCustCode = null;
            tbCustCode.Text = string.Empty;
            RefreshSelectedProductPrice();
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

            DataRow row = GetAccessoryRow(FGrid1.Row);
            if (row == null) return;
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
                var banner = cached as IOrderItemEditor;
                if (banner != null)
                {
                    var state = banner.CaptureEditorState();
                    if (state.ZoomType != "FIXED_SIZE") state.ZoomType = null;
                    if (state.WidthMm != GetDecimal(row["WidthMm"]) || state.HeightMm != GetDecimal(row["HeightMm"]))
                        state.OutputServerFile = null;
                    state.WidthMm = GetDecimal(row["WidthMm"]); state.HeightMm = GetDecimal(row["HeightMm"]);
                    state.Quantity = GetDecimal(row["Qty"]);
                    string nextSourcePath = row["RequestFilePath"] == DBNull.Value ? null : row["RequestFilePath"].ToString();
                    if (!string.Equals(state.SourceFilePath, nextSourcePath, StringComparison.OrdinalIgnoreCase))
                        state.SourceServerFile = state.OutputServerFile = null;
                    state.SourceFilePath = nextSourcePath;
                    row["EditorStateJson"] = state.ToJson();
                }
                panelEdit.Controls.Remove(cached);
                if (m_orderEditor == cached) m_orderEditor = null;
                foreach (var key in new List<string>(m_materialEditors.Keys))
                    if (m_materialEditors[key] == cached) { m_materialEditors.Remove(key); m_materialRows.Remove(key); }
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

            string priceCategory = GetSelectedPriceCategory();
            if (string.IsNullOrWhiteSpace(priceCategory))
            {
                MessageBox.Show("장비의 대분류를 선택해주세요.", "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (DialogPriceItemList dialog = new DialogPriceItemList(itemName, m_sSelectedCustCode, tbCustName.Text, priceCategory))
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
                    Qty = 1m,
                    UnitPrice = dialog.SelectedUnitPrice,
                    Remark = dialog.SelectedRemark
                }, null);

                DataRow row = m_orderItems.Rows[m_orderItems.Rows.Count - 1];
                row["PriceCategory"] = dialog.SelectedPriceCategory;
                row["CategoryName"] = dialog.SelectedCategoryName;
                row["ApplyFrom"] = (object)GetDateValue(dialog.SelectedApplyFrom) ?? DBNull.Value;
                CalculateAccessoryAmounts(row);
                UpdateOrderItemTotals();
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
            m_orderItems.Rows.Add(row);
            UpdateOrderItemTotals();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            FGrid1.FinishEditing();
            DataRow row = GetAccessoryRow(FGrid1.Row);
            if (row == null) return;
            m_orderItems.Rows.Remove(row);
            ResetLineNumbers();
        }

        private DataRow GetAccessoryRow(int gridRow)
        {
            if (gridRow < FGrid1.Rows.Fixed || gridRow >= FGrid1.Rows.Count || FGrid1.Rows[gridRow].IsNode) return null;
            var view = FGrid1.Rows[gridRow].DataSource as DataRowView;
            return view == null ? null : view.Row;
        }

        private void CalculateAccessoryAmounts(DataRow row)
        {
            decimal supply = Math.Round(GetDecimal(row["UnitPrice"]) * GetDecimal(row["Qty"]), 0, MidpointRounding.AwayFromZero);
            decimal vat = Math.Round(supply * .1m, 0, MidpointRounding.AwayFromZero);
            row["SupplyAmount"] = supply;
            row["VatAmount"] = vat;
            row["TotalAmount"] = supply + vat;
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
            UpdateOrderPriceSummary(false);
            FGrid1.Subtotal(AggregateEnum.Clear);
            if (!FGrid1.Cols["SupplyAmount"].Visible && !FGrid1.Cols["VatAmount"].Visible && !FGrid1.Cols["TotalAmount"].Visible)
            {
                return;
            }

            FGrid1.SubtotalPosition = SubtotalPositionEnum.BelowData;
            if (m_orderItems.Rows.Count == 0)
            {
                FGrid1.Rows.InsertNode(FGrid1.Rows.Fixed, 0);
                FGrid1[FGrid1.Rows.Fixed, "TotalAmount"] = 0m;
            }
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

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (m_isSaving) return;
            FGrid1.FinishEditing();
            BindingContext[m_orderItems].EndCurrentEdit();
            if (!CheckValidation())
            {
                return;
            }

            m_isSaving = true;
            Enabled = false;
            string previousSaveText = btnSave.Text;
            btnSave.Text = "미리보기 저장 중…";
            try
            {
                var sourceEditor = m_orderEditor as IOrderSourceEditor;
                var document = sourceEditor == null ? m_sourceDocument : sourceEditor.SourceDocument;
                var snapshot = await document.CapturePreviewSnapshotAsync(System.Threading.CancellationToken.None);
                if (IsDisposed || Disposing) return;
                var order = CreateOrderSaveData();
                order.PreviewSnapshotJson = snapshot == null ? null : snapshot.ToJson();
                m_orderId = OrderRepository.UpsertOrder(order);
                m_savedSourcePreview = snapshot;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show("저장 중 오류가 발생했습니다.\r\n" + ex.Message, "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            finally
            {
                m_isSaving = false;
                if (!IsDisposed && !Disposing) { Enabled = true; btnSave.Text = previousSaveText; }
            }
            DialogResult = DialogResult.OK;
            Close();
            EventHandler handler = OrderSaved;
            if (handler != null) handler(this, EventArgs.Empty);
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
                OrderTitle = tbOrderName.Text.Trim(),
                SpecText = tbSpec.Text,
                OrderDescription = tbRemark.Text,
                DeliveryAddress = tbAddress.Text.Trim(),
                ProductionStatus = m_productionStatus,
                SourceChannel = "직접입력",
                CreatedBy = Environment.UserName,
                DesignPadStateJson = CaptureDesignPadState(),
                Items = CreateOrderItemSaveDataList()
            };
        }

        private System.Collections.Generic.List<OrderItemSaveData> CreateOrderItemSaveDataList()
        {
            System.Collections.Generic.List<OrderItemSaveData> items = new System.Collections.Generic.List<OrderItemSaveData>();
            foreach (DataRow row in m_orderItems.Rows)
            {
                UserControl cached;
                if (m_orderItemEditors != null && m_orderItemEditors.TryGetValue(row, out cached))
                {
                    var banner = cached as IOrderItemEditor;
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
                OrderFound = false;
                return;
            }

            m_savedSourcePreview = OrderPreviewSnapshotRepository.Get(m_orderId);
            string designPadState = OrderDesignPadStateRepository.Get(m_orderId);
            m_hasDesignPadState = !string.IsNullOrWhiteSpace(designPadState);
            ApplyLoadedOrder(row);
            LoadOrderItems();
            ApplyDesignPadState(designPadState);
            m_isInitializing = false;
        }

        private void ApplyLoadedOrder(DataRow row)
        {
            m_isInitializing = true;
            tbOrderNo.Text = Convert.ToString(row["OrderNo"]);
            tbOrderName.Text = row.Table.Columns.Contains("OrderTitle") ? Convert.ToString(row["OrderTitle"]) : string.Empty;
            bool linked = row.Table.Columns.Contains("OrderGroupSequence") && row["OrderGroupSequence"] != DBNull.Value;
            dtmOrder.Enabled = dtmDelivery.Enabled = tbCustName.Enabled = tbCustCode.Enabled = btnCustList.Enabled = !linked;
            cbChannel.Enabled = cbDeliveryType.Enabled = tbDeliveryFee.Enabled = !linked;
            tbSpec.Text = row.Table.Columns.Contains("SpecText") ? Convert.ToString(row["SpecText"]) : string.Empty;
            dtmOrder.Value = GetDateValue(row["OrderDate"]) ?? DateTime.Today;
            tbCustName.Text = row["CustName"].ToString();
            tbCustCode.Text = row["CustCode"].ToString();
            m_sSelectedCustCode = tbCustCode.Text;
            SelectComboText(cbChannel, row["OrderChannel"].ToString());
            SelectComboText(cbDeliveryType, row["DeliveryType"].ToString());
            tbDeliveryFee.Value = GetDecimal(row["DeliveryFee"]);
            m_productionStatus = row["ProductionStatus"].ToString();
            tbRemark.Text = row["OrderDescription"].ToString();
            tbAddress.Text = row.Table.Columns.Contains("DeliveryAddress") ? Convert.ToString(row["DeliveryAddress"]) : string.Empty;
            tbZipCode.Text = string.Empty;

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

            bool legacyOrder = m_DialogMode == DialogMode.Edit && !m_hasDesignPadState;
            if (!legacyOrder && (cbItem.SelectedIndex < 0 || string.IsNullOrWhiteSpace(cbItem.Text) || cbItem.Text == "[선택]"))
            {
                MessageBox.Show("제품을 선택해주세요", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            UpdateAutomaticSpecification();
            if (!legacyOrder && string.IsNullOrWhiteSpace(tbSpec.Text))
            {
                MessageBox.Show("원본파일을 불러와 규격을 확인해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            var activeEditor = m_orderEditor as IOrderItemEditor;
            if (activeEditor != null && activeEditor.IsProcessing)
            {
                MessageBox.Show("파일 처리가 완료된 후 저장해주세요.", "주문정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            if (m_orderItemEditors != null)
                foreach (UserControl editor in m_orderItemEditors.Values)
                {
                    var banner = editor as IOrderItemEditor;
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
            Close();
            EventHandler handler = CancelRequested;
            if (handler != null) handler(this, EventArgs.Empty);
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
