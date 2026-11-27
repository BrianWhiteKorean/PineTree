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
    public partial class UcPriceItems : UserControl, INavigationView
    {
        private const string AllText = "[전체]";
        private const string NoCustomerText = "[해당없음]";
        private DataTable _customerFilterItems;
        private readonly NavigationController _navigationController;
        private bool _isInitializingCombo;
        private DataTable _priceItems;
        private long? _selectedPriceItemSeqNo;
        private int _customerComboLoadVersion;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;


                return cp;
            }
        }

        public UcPriceItems()
        {
            InitializeComponent();
            cbCustName.DropDownStyle = C1.Win.C1Input.DropDownStyle.DropDownList;
            cbCustName.ItemsDisplayMember = "CustName";
            cbCustName.ItemsValueMember = "CustCode";
            BindCustomerCombo(new DataTable(), null);

            _navigationController = new NavigationController(picBackground);
            _navigationController.NavigationRequested += NavigationController_NavigationRequested;
            Load += UcPriceItems_Load;
            btnSelect.Click += btnSelect_Click;
            btnAdd.Click += btnAdd_Click;
            btnEdit.Click += btnEdit_Click;
            InitializePriceItemGrid();
            tbPriceItemName.KeyDown += SearchTextBox_KeyDown;
            cbCategory.SelectedIndexChanged += cbCategory_SelectedIndexChanged;
            PriceItemRepository.DedicatedPriceItemSaved += PriceItemRepository_DedicatedPriceItemSaved;
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;

        private async void UcPriceItems_Load(object sender, EventArgs e)
        {
            await LoadComboBoxesAsync();
            await LoadPriceItemsAsync();
        }

        private async void btnSelect_Click(object sender, EventArgs e)
        {
            await LoadPriceItemsAsync();
        }

        private async void btnAdd_Click(object sender, EventArgs e)
        {
            using (DialogPriceItem dialog = new DialogPriceItem())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    await LoadComboBoxesAsync();
                    await LoadPriceItemsAsync();
                }
            }
        }

        private async void btnEdit_Click(object sender, EventArgs e)
        {
            await OpenEditDialogAsync();
        }

        private async void PriceItemGrid_CellDoubleClicked(object sender, PagedGridCellClickEventArgs e)
        {
            SelectPriceItem(e);
            await OpenEditDialogAsync();
        }

        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            await LoadPriceItemsAsync();
        }

        private async void cbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInitializingCombo)
            {
                return;
            }

            await LoadCategoryNameComboAsync();
        }

        private void NavigationController_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigationRequested?.Invoke(this, e);
        }

        private async Task LoadComboBoxesAsync()
        {
            try
            {
                string selectedCategory = cbCategory.Text;
                string selectedCategoryName = cbCategoryName.Text;
                string selectedCustCode = GetSelectedCustomerCode();

                _isInitializingCombo = true;
                await LoadCustomerComboAsync(selectedCustCode);
                await LoadCategoryComboAsync(selectedCategory);
                await LoadCategoryNameComboAsync(selectedCategoryName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("조회조건을 불러오지 못했습니다.\r\n" + ex.Message, "상품관리", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _isInitializingCombo = false;
            }
        }

        private async void PriceItemRepository_DedicatedPriceItemSaved(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                // 아직 표시되지 않은 화면은 최초 Load에서 최신 목록을 조회합니다.
                return;
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => PriceItemRepository_DedicatedPriceItemSaved(sender, e)));
                    return;
                }

                await LoadCustomerComboAsync(GetSelectedCustomerCode());
            }
            catch (Exception ex)
            {
                MessageBox.Show("거래처 조회조건을 갱신하지 못했습니다.\r\n" + ex.Message, "상품관리", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task LoadCustomerComboAsync(string selectedCustCode)
        {
            int loadVersion = ++_customerComboLoadVersion;
            DataTable customers = await Task.Run(() => PriceItemRepository.GetPriceItemCustomers());
            if (IsDisposed || Disposing || loadVersion != _customerComboLoadVersion)
            {
                return;
            }

            BindCustomerCombo(customers, selectedCustCode);
        }

        private void BindCustomerCombo(DataTable customers, string selectedCustCode)
        {
            DataTable items = customers.Copy();
            if (!items.Columns.Contains("CustCode"))
            {
                items.Columns.Add("CustCode", typeof(string));
            }
            if (!items.Columns.Contains("CustName"))
            {
                items.Columns.Add("CustName", typeof(string));
            }

            DataRow defaultItem = items.NewRow();
            defaultItem["CustCode"] = string.Empty;
            defaultItem["CustName"] = NoCustomerText;
            items.Rows.InsertAt(defaultItem, 0);
            _customerFilterItems = items;
            cbCustName.TranslateValue = true;
            cbCustName.ItemsDataSource = items;
            int selectedIndex = 0;
            for (int index = 1; index < items.Rows.Count; index++)
            {
                if (string.Equals(Convert.ToString(items.Rows[index]["CustCode"]), selectedCustCode, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                    break;
                }
            }
            cbCustName.SelectedIndex = selectedIndex;
        }

        private string GetSelectedCustomerCode()
        {
            int index = cbCustName.SelectedIndex;
            return _customerFilterItems != null && index >= 0 && index < _customerFilterItems.Rows.Count
                ? Convert.ToString(_customerFilterItems.Rows[index]["CustCode"])
                : string.Empty;
        }

        private async Task LoadCategoryComboAsync(string selectedText)
        {
            cbCategory.Items.Clear();
            cbCategory.Items.Add(AllText);

            DataTable categories = await Task.Run(() => PriceItemRepository.GetPriceCategories());
            if (IsDisposed)
            {
                return;
            }

            foreach (DataRow row in categories.Rows)
            {
                cbCategory.Items.Add(row["PriceCategory"].ToString());
            }

            SelectComboItem(cbCategory, selectedText);
        }

        private async Task LoadCategoryNameComboAsync(string selectedText)
        {
            cbCategoryName.Items.Clear();
            cbCategoryName.Items.Add(AllText);

            string priceCategory = GetSelectedValue(cbCategory);
            DataTable categoryNames = await Task.Run(() => PriceItemRepository.GetCategoryNames(priceCategory));
            if (IsDisposed)
            {
                return;
            }

            foreach (DataRow row in categoryNames.Rows)
            {
                cbCategoryName.Items.Add(row["CategoryName"].ToString());
            }

            SelectComboItem(cbCategoryName, selectedText);
        }

        private Task LoadCategoryNameComboAsync()
        {
            return LoadCategoryNameComboAsync(AllText);
        }

        private async Task LoadPriceItemsAsync()
        {
            try
            {
                string priceCategory = GetSelectedValue(cbCategory);
                string categoryName = GetSelectedValue(cbCategoryName);
                string priceItemName = tbPriceItemName.Text;
                string custCode = GetSelectedCustomerCode();

                DataTable priceItems = await Task.Run(() => PriceItemRepository.GetPriceItemsForCustomer(
                    priceCategory,
                    categoryName,
                    priceItemName,
                    custCode));

                if (IsDisposed)
                {
                    return;
                }

                BindPriceItems(priceItems);
            }
            catch (Exception ex)
            {
                MessageBox.Show("상품정보를 조회하지 못했습니다.\r\n" + ex.Message, "조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializePriceItemGrid()
        {
            priceItemGrid.PageSize = Math.Max(1, (priceItemGrid.Height - 36 - 52) / 41);
            priceItemGrid.Columns.Add(new PagedGridColumn("No", "번호", 40, ContentAlignment.MiddleCenter));
            priceItemGrid.Columns.Add(new PagedGridColumn("PriceCategory", "카테고리", 80, ContentAlignment.MiddleCenter));
            priceItemGrid.Columns.Add(new PagedGridColumn("CategoryName", "품목분류", 120, ContentAlignment.MiddleCenter));
            // 기존 자동 너비 326px에서 40px 축소하고 숨긴 열의 여백이 재분배되지 않도록 고정합니다.
            priceItemGrid.Columns.Add(new PagedGridColumn("PriceItemName", "품목명", 286, ContentAlignment.MiddleLeft));
            priceItemGrid.Columns.Add(new PagedGridColumn("ItemCode", "품목코드", 95, ContentAlignment.MiddleCenter));
            priceItemGrid.Columns.Add(new PagedGridColumn("SpecText", "규격", 170, ContentAlignment.MiddleLeft));
            priceItemGrid.Columns.Add(new PagedGridColumn("UnitName", "단위", 60, ContentAlignment.MiddleCenter));
            priceItemGrid.Columns.Add(new PagedGridColumn("BillingBasis", "과금기준", 75, ContentAlignment.MiddleCenter));
            priceItemGrid.Columns.Add(new PagedGridColumn("CurrentUnitPrice", "현재판매가", 95, ContentAlignment.MiddleRight));
            priceItemGrid.Columns.Add(new PagedGridColumn("PriceRemarkGap", string.Empty, 16, ContentAlignment.MiddleLeft));
            priceItemGrid.Columns.Add(new PagedGridColumn("Remark", "비고", 150, ContentAlignment.MiddleLeft));
            priceItemGrid.Columns.Add(new PagedGridColumn("CustCode", "전용 거래처코드", 100, ContentAlignment.MiddleCenter));
            priceItemGrid.Columns.Add(new PagedGridColumn("SpecialCustName", "전용 거래처", 110, ContentAlignment.MiddleLeft));
            priceItemGrid.PageChanged += delegate { BindPriceItemPage(); };
            priceItemGrid.CellClicked += delegate(object sender, PagedGridCellClickEventArgs e) { SelectPriceItem(e); };
            priceItemGrid.SelectionChanged += PriceItemGrid_SelectionChanged;
            priceItemGrid.CellDoubleClicked += PriceItemGrid_CellDoubleClicked;
            priceItemGrid.KeyDown += PriceItemGrid_KeyDown;
            priceItemGrid.Resize += delegate
            {
                priceItemGrid.PageSize = Math.Max(1, (priceItemGrid.Height - 36 - 52) / 41);
                priceItemGrid.PageIndex = priceItemGrid.PageIndex;
                BindPriceItemPage();
            };
        }

        private async void PriceItemGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || e.Modifiers != Keys.None || priceItemGrid.SelectedRowIndex < 0)
            {
                return;
            }

            e.SuppressKeyPress = true;
            await OpenEditDialogAsync();
        }

        private void BindPriceItems(DataTable priceItems)
        {
            _priceItems = priceItems;
            priceItemGrid.TotalCount = priceItems.Rows.Count;
            priceItemGrid.PageIndex = 1;
            priceItemGrid.TotalText = string.Format("전체 {0:#,##0}건", priceItems.Rows.Count);
            BindPriceItemPage();
        }

        private void BindPriceItemPage()
        {
            _selectedPriceItemSeqNo = null;
            List<Dictionary<string, PagedGridCell>> rows = new List<Dictionary<string, PagedGridCell>>();
            if (_priceItems != null)
            {
                int startIndex = (priceItemGrid.PageIndex - 1) * priceItemGrid.PageSize;
                int endIndex = Math.Min(startIndex + priceItemGrid.PageSize, _priceItems.Rows.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    DataRow priceItem = _priceItems.Rows[index];
                    Dictionary<string, PagedGridCell> row = new Dictionary<string, PagedGridCell>
                    {
                        { "SeqNo", new PagedGridCell(Convert.ToString(priceItem["SeqNo"])) },
                        { "No", new PagedGridCell((index + 1).ToString()) }
                    };
                    foreach (PagedGridColumn column in priceItemGrid.Columns)
                    {
                        if (column.Key == "No")
                        {
                            continue;
                        }

                        if (column.Key == "PriceRemarkGap")
                        {
                            row.Add(column.Key, new PagedGridCell(string.Empty));
                            continue;
                        }

                        object value = priceItem[column.Key];
                        string text = value is DateTime
                            ? ((DateTime)value).ToString("yyyy-MM-dd")
                            : column.Key == "CurrentUnitPrice" && value != DBNull.Value
                                ? Convert.ToDecimal(value).ToString("#,##0.##")
                                : Convert.ToString(value);
                        row.Add(column.Key, new PagedGridCell(text));
                    }
                    rows.Add(row);
                }
            }
            priceItemGrid.SetRows(rows);
        }

        private void SelectPriceItem(PagedGridCellClickEventArgs e)
        {
            PagedGridCell seqNoCell;
            long seqNo;
            if (e.Row.TryGetValue("SeqNo", out seqNoCell) && long.TryParse(seqNoCell.Text, out seqNo))
            {
                _selectedPriceItemSeqNo = seqNo;
                priceItemGrid.SelectedRowIndex = e.RowIndex;
            }
        }

        private void PriceItemGrid_SelectionChanged(object sender, EventArgs e)
        {
            _selectedPriceItemSeqNo = null;
            int selectedIndex = priceItemGrid.SelectedRowIndex;
            if (_priceItems == null || selectedIndex < 0)
            {
                return;
            }

            int index = (priceItemGrid.PageIndex - 1) * priceItemGrid.PageSize + selectedIndex;
            long seqNo;
            if (index >= 0 && index < _priceItems.Rows.Count && long.TryParse(Convert.ToString(_priceItems.Rows[index]["SeqNo"]), out seqNo))
            {
                _selectedPriceItemSeqNo = seqNo;
            }
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

        private async Task OpenEditDialogAsync()
        {
            long seqNo;
            if (!TryGetCurrentSeqNo(out seqNo))
            {
                MessageBox.Show("수정할 상품을 선택해주세요.", "상품관리", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (DialogPriceItem dialog = new DialogPriceItem(seqNo))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    await LoadComboBoxesAsync();
                    await LoadPriceItemsAsync();
                }
            }
        }

        private bool TryGetCurrentSeqNo(out long seqNo)
        {
            seqNo = _selectedPriceItemSeqNo.GetValueOrDefault();
            return _selectedPriceItemSeqNo.HasValue;
        }
    }
}

