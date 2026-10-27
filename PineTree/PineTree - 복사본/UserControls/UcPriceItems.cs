using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;

namespace PineTree.UserControls
{
    public partial class UcPriceItems : UserControl, INavigationView
    {
        private const string AllText = "[전체]";
        private readonly NavigationController _navigationController;
        private bool _isInitializingCombo;

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

            _navigationController = new NavigationController(picBackground);
            _navigationController.NavigationRequested += NavigationController_NavigationRequested;
            Load += UcPriceItems_Load;
            btnSelect.Click += btnSelect_Click;
            cbCategory.SelectedIndexChanged += cbCategory_SelectedIndexChanged;
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
                _isInitializingCombo = true;
                await LoadCategoryComboAsync();
                await LoadCategoryNameComboAsync();
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

        private async Task LoadCategoryComboAsync()
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

            cbCategory.SelectedIndex = 0;
        }

        private async Task LoadCategoryNameComboAsync()
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

            cbCategoryName.SelectedIndex = 0;
        }

        private async Task LoadPriceItemsAsync()
        {
            try
            {
                string priceCategory = GetSelectedValue(cbCategory);
                string categoryName = GetSelectedValue(cbCategoryName);
                string priceItemName = tbPriceItemName.Text;

                DataTable priceItems = await Task.Run(() => PriceItemRepository.GetPriceItems(
                    priceCategory,
                    categoryName,
                    priceItemName));

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

        private void BindPriceItems(DataTable priceItems)
        {
            PrepareGridData(priceItems);

            FGrid1.DataSource = priceItems;
            InitGrid();

            labelCount.Text = string.Format("조회건수 : {0}건", priceItems.Rows.Count);
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

            FGrid1.Cols["PriceCategory"].Caption = "카테고리";
            FGrid1.Cols["PriceCategory"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["PriceCategory"].AllowEditing = false;
            FGrid1.Cols["PriceCategory"].Width = 130;

            FGrid1.Cols["CategoryName"].Caption = "품목분류";
            FGrid1.Cols["CategoryName"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["CategoryName"].AllowEditing = false;
            FGrid1.Cols["CategoryName"].Width = 160;

            FGrid1.Cols["PriceItemName"].Caption = "품목명";
            FGrid1.Cols["PriceItemName"].TextAlign = TextAlignEnum.LeftCenter;
            FGrid1.Cols["PriceItemName"].AllowEditing = false;
            FGrid1.Cols["PriceItemName"].Width = 220;

            FGrid1.Cols["MatchedItemCode"].Caption = "품목코드";
            FGrid1.Cols["MatchedItemCode"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["MatchedItemCode"].AllowEditing = false;
            FGrid1.Cols["MatchedItemCode"].Width = 130;

            FGrid1.Cols["SpecText"].Caption = "규격";
            FGrid1.Cols["SpecText"].TextAlign = TextAlignEnum.LeftCenter;
            FGrid1.Cols["SpecText"].AllowEditing = false;
            FGrid1.Cols["SpecText"].Width = 130;

            FGrid1.Cols["CurrentUnitPrice"].Caption = "현재판매가";
            FGrid1.Cols["CurrentUnitPrice"].TextAlign = TextAlignEnum.RightCenter;
            FGrid1.Cols["CurrentUnitPrice"].AllowEditing = false;
            FGrid1.Cols["CurrentUnitPrice"].Width = 130;
            FGrid1.Cols["CurrentUnitPrice"].Format = "#,##0.##";

            FGrid1.Cols["ApplyFrom"].Caption = "단가적용일";
            FGrid1.Cols["ApplyFrom"].TextAlign = TextAlignEnum.CenterCenter;
            FGrid1.Cols["ApplyFrom"].AllowEditing = false;
            FGrid1.Cols["ApplyFrom"].Width = 120;
            FGrid1.Cols["ApplyFrom"].Format = "yyyy-MM-dd";

            FGrid1.Cols["Remark"].Caption = "비고";
            FGrid1.Cols["Remark"].TextAlign = TextAlignEnum.LeftCenter;
            FGrid1.Cols["Remark"].AllowEditing = false;
            FGrid1.Cols["Remark"].Width = 180;

            FGrid1.Rows[0].TextAlign = TextAlignEnum.CenterCenter;
        }

        private void PrepareGridData(DataTable priceItems)
        {
            if (!priceItems.Columns.Contains("IsSelected"))
            {
                priceItems.Columns.Add("IsSelected", typeof(bool)).SetOrdinal(0);
            }

            if (!priceItems.Columns.Contains("No"))
            {
                priceItems.Columns.Add("No", typeof(int)).SetOrdinal(1);
            }

            for (int i = 0; i < priceItems.Rows.Count; i++)
            {
                priceItems.Rows[i]["IsSelected"] = false;
                priceItems.Rows[i]["No"] = i + 1;
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
    }
}

