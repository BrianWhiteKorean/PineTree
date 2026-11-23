using System;
using System.Configuration;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1Input;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogAddressSearch : Form
    {
        private readonly AddressSearchService _searchService;
        private readonly TextBox txtKeyword = new TextBox { Name = "txtKeyword", Dock = DockStyle.Fill, AccessibleName = "검색어", MaxLength = 256 };
        private readonly TextBox txtDetailAddress = new TextBox { Name = "txtDetailAddress", Dock = DockStyle.Fill };
        private readonly TextBox _selectedRoadAddress = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly TextBox _selectedJibunAddress = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly C1Button btnSearch = new C1Button { Name = "btnSearch", Text = "검색", Dock = DockStyle.Fill };
        private readonly C1Button _previousButton = new C1Button { Text = "이전", Dock = DockStyle.Fill };
        private readonly C1Button _nextButton = new C1Button { Text = "다음", Dock = DockStyle.Fill };
        private readonly Label _status = new Label { Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        private readonly Label _pageInfo = new Label { Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter };
        private readonly DataGridView _results = new DataGridView();
        private CancellationTokenSource _searchCancellation;
        private AddressResult _currentAddress;
        private int _page;
        private int _pageCount;

        public AddressResult SelectedAddress { get; private set; }

        public DialogAddressSearch() : this(null) { }

        internal DialogAddressSearch(AddressSearchService searchService)
        {
            _searchService = searchService;
            InitializeComponent();
            InitializeSearchControls();
            SetSearchButtons();
            ActiveControl = txtKeyword;
        }

        private void InitializeSearchControls()
        {
            var searchBar = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 5, 8, 3), ColumnCount = 2 };
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            searchBar.Controls.Add(txtKeyword, 0, 0);
            searchBar.Controls.Add(btnSearch, 1, 0);
            panel2.Controls.Add(searchBar);

            var paging = new TableLayoutPanel { Dock = DockStyle.Top, Height = 33, ColumnCount = 4, TabIndex = 0 };
            paging.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            paging.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 63));
            paging.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            paging.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 63));
            paging.Controls.Add(_status, 0, 0);
            paging.Controls.Add(_previousButton, 1, 0);
            paging.Controls.Add(_pageInfo, 2, 0);
            paging.Controls.Add(_nextButton, 3, 0);

            var selection = new TableLayoutPanel { Dock = DockStyle.Top, Height = 90, ColumnCount = 2, RowCount = 3, TabIndex = 1 };
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddAddressField(selection, 0, "도로명 / 우편번호", _selectedRoadAddress);
            AddAddressField(selection, 1, "지번주소", _selectedJibunAddress);
            AddAddressField(selection, 2, "상세주소", txtDetailAddress);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 123, TabIndex = 1 };
            footer.Controls.Add(selection);
            footer.Controls.Add(paging);

            _results.Dock = DockStyle.Fill;
            _results.AutoGenerateColumns = false;
            _results.AllowUserToAddRows = false;
            _results.AllowUserToDeleteRows = false;
            _results.BackgroundColor = SystemColors.Window;
            _results.MultiSelect = false;
            _results.ReadOnly = true;
            _results.RowHeadersVisible = false;
            _results.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _results.AccessibleName = "주소 검색 결과";
            AddResultColumn("우편번호", nameof(AddressResult.ZipCode), 72, 0);
            AddResultColumn("도로명주소", nameof(AddressResult.RoadAddress), 150, 52);
            AddResultColumn("지번주소", nameof(AddressResult.JibunAddress), 130, 34);
            AddResultColumn("건물명", nameof(AddressResult.BuildingName), 80, 14);
            panel3.Padding = new Padding(8, 0, 8, 0);
            panel3.Controls.Add(_results);
            panel3.Controls.Add(footer);

            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;
            btnOK.Click += delegate { ConfirmAddress(); };
            btnSearch.Click += async delegate { await SearchAsync(1); };
            _previousButton.Click += async delegate { await SearchAsync(_page - 1); };
            _nextButton.Click += async delegate { await SearchAsync(_page + 1); };
            txtKeyword.TextChanged += delegate { ResetResults(); };
            _results.SelectionChanged += delegate { UpdateAddressSelection(); };
            _results.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !txtDetailAddress.Enabled) return;
                if (!string.IsNullOrWhiteSpace(txtDetailAddress.Text))
                {
                    ConfirmAddress();
                    return;
                }
                txtDetailAddress.Focus();
            };
        }

        private static void AddAddressField(TableLayoutPanel layout, int row, string title, TextBox field)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
            layout.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            }, 0, row);
            field.AccessibleName = title;
            layout.Controls.Add(field, 1, row);
        }

        private void AddResultColumn(string title, string property, int width, float fillWeight)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = title,
                DataPropertyName = property,
                MinimumWidth = width,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (fillWeight > 0)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                column.FillWeight = fillWeight;
            }
            _results.Columns.Add(column);
        }

        private void ResetResults()
        {
            _page = 0;
            _pageCount = 0;
            _status.Text = string.Empty;
            _results.DataSource = null;
            UpdateAddressSelection();
            SetSearchButtons();
        }

        private async Task SearchAsync(int page)
        {
            if (_searchCancellation != null) return;

            string keyword = txtKeyword.Text.Trim();
            if (keyword.Length == 0)
            {
                _status.Text = "검색어를 입력해 주세요.";
                txtKeyword.Focus();
                return;
            }

            using (var cancellation = new CancellationTokenSource())
            {
                _searchCancellation = cancellation;
                SetSearchButtons();
                UpdateAddressSelection();
                _status.Text = "주소를 검색하는 중입니다.";
                try
                {
                    var service = _searchService ?? await AddressSearchService.FromSettingsAsync(cancellation.Token);
                    AddressSearchPage result = await service.SearchAsync(keyword, page, cancellation.Token);
                    if (cancellation.IsCancellationRequested || IsDisposed) return;

                    _page = page;
                    _pageCount = (int)Math.Ceiling(result.TotalCount / (double)AddressSearchService.PageSize);
                    _results.DataSource = result.Addresses;
                    _results.ClearSelection();
                    _status.Text = result.Addresses.Count == 0 ? "검색 결과가 없습니다." : $"총 {result.TotalCount:N0}건";
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (AddressSearchException ex)
                {
                    if (!cancellation.IsCancellationRequested && !IsDisposed) _status.Text = ex.Message;
                }
                catch (ConfigurationErrorsException)
                {
                    if (!cancellation.IsCancellationRequested && !IsDisposed)
                        _status.Text = "프로그램의 주소 검색 설정을 확인해 주세요.";
                }
                finally
                {
                    _searchCancellation = null;
                    if (!cancellation.IsCancellationRequested && !IsDisposed)
                    {
                        SetSearchButtons();
                        UpdateAddressSelection();
                    }
                }
            }
        }

        private void SetSearchButtons()
        {
            bool idle = _searchCancellation == null;
            txtKeyword.ReadOnly = !idle;
            btnSearch.Enabled = idle;
            _previousButton.Enabled = idle && _page > 1;
            _nextButton.Enabled = idle && _page > 0 && _page < _pageCount;
            _pageInfo.Text = _pageCount > 0 ? $"{_page} / {_pageCount}" : string.Empty;
        }

        private void UpdateAddressSelection()
        {
            var address = _results.SelectedRows.Count == 0 ? null : _results.SelectedRows[0].DataBoundItem as AddressResult;
            if (_currentAddress != address) txtDetailAddress.Clear();
            _currentAddress = address;
            _selectedRoadAddress.Text = address == null ? string.Empty : $"[{address.ZipCode}] {address.RoadAddress}";
            _selectedJibunAddress.Text = address?.JibunAddress ?? string.Empty;
            txtDetailAddress.Enabled = address != null && _searchCancellation == null;
        }

        private void ConfirmAddress()
        {
            if (_searchCancellation != null) return;
            if (_currentAddress == null)
            {
                _status.Text = "검색 결과에서 주소를 선택해 주세요.";
                return;
            }

            _currentAddress.DetailAddress = txtDetailAddress.Text.Trim();
            SelectedAddress = _currentAddress;
            DialogResult = DialogResult.OK;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                if (txtKeyword.Focused)
                {
                    ((IButtonControl)btnSearch).PerformClick();
                    return true;
                }
                if (txtDetailAddress.Focused)
                {
                    ConfirmAddress();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            _searchCancellation?.Cancel();
            if (DialogResult != DialogResult.OK)
            {
                SelectedAddress = null;
                DialogResult = DialogResult.Cancel;
            }
        }
    }
}
