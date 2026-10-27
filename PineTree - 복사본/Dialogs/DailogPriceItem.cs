using C1.Win.C1InputPanel;
using PineTree.Classes;
using System;
using System.Data;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DailogPriceItem : Form
    {
        private DialogMode m_DialogMode = DialogMode.New;
        private long m_seqNo = -1;
        private bool m_isDuplicateChecked;
        private string m_checkedItemCode = string.Empty;
        private DateTime m_applyFrom = DateTime.Today;
        private bool m_isInitializing;

        public DailogPriceItem()
        {
            InitializeComponent();
            InitEvent();
            InitCombo();
        }

        public DailogPriceItem(long seqNo)
        {
            InitializeComponent();
            InitEvent();

            m_seqNo = seqNo;
            inputGroupHeader1.Text = "상품정보 수정";
            m_DialogMode = DialogMode.Edit;

            InitCombo();
            LoadPriceItem();
        }

        private void InitEvent()
        {
            c1Button1.Click += btnSave_Click;
            c1Button2.Click += btnCancel_Click;
            btnDuplCheck.Click += btnDuplCheck_Click;
            tbItemCode.TextChanged += tbItemCode_TextChanged;
            cbCategory.SelectedIndexChanged += cbCategory_SelectedIndexChanged;
            cbApplyPrice.SelectedIndexChanged += cbApplyPrice_SelectedIndexChanged;
        }

        private void InitCombo()
        {
            m_isInitializing = true;

            LoadTextCombo(cbCategory, PriceItemRepository.GetPriceCategories(), "PriceCategory", false);
            LoadCategoryNameCombo();
            LoadTextCombo(cbApplyPrice, PriceItemRepository.GetPriceNames(), "PriceName", false);
            LoadTextCombo(cbUnit, CommonCodeRepository.GetItems("단위"), "ItemName", false);
            LoadTextCombo(cbBillingBasis, CommonCodeRepository.GetItems("단가적용기준"), "ItemName", false);

            cbIsActive.SelectedIndex = 0;

            m_isInitializing = false;
            tbPrice.Value = 0M;
            m_applyFrom = DateTime.Today;
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
            LoadTextCombo(cbCategoryName, PriceItemRepository.GetCategoryNames(cbCategory.Text), "CategoryName", false);
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
                long priceItemSeqNo = m_DialogMode == DialogMode.New
                    ? PriceItemRepository.InsertPriceItem(
                        cbCategory.Text,
                        cbCategoryName.Text,
                        tbItemName.Text,
                        tbItemCode.Text,
                        tbSpecText.Text,
                        cbUnit.Text,
                        cbBillingBasis.Text,
                        GetNullableDecimal(tbDefaultWidth.Value),
                        GetNullableDecimal(tbDefaultHeight.Value),
                        cbIsActive.SelectedIndex == 0)
                    : m_seqNo;

                if (m_DialogMode == DialogMode.Edit)
                {
                    PriceItemRepository.UpdatePriceItem(
                        m_seqNo,
                        cbCategory.Text,
                        cbCategoryName.Text,
                        tbItemName.Text,
                        tbItemCode.Text,
                        tbSpecText.Text,
                        cbUnit.Text,
                        cbBillingBasis.Text,
                        GetNullableDecimal(tbDefaultWidth.Value),
                        GetNullableDecimal(tbDefaultHeight.Value),
                        cbIsActive.SelectedIndex == 0);
                }

                PriceItemRepository.InsertPriceItemPrice(
                    priceItemSeqNo,
                    cbApplyPrice.Text,
                    GetDecimal(tbPrice.Value),
                    m_applyFrom,
                    tbRemark.Text);

                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류가 발생했습니다.\r\n" + ex.Message, "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void btnDuplCheck_Click(object sender, EventArgs e)
        {
            string itemCode = tbItemCode.Text.Trim();
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                MessageBox.Show("품목코드를 입력해주세요.", "중복체크", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long? exceptSeqNo = m_DialogMode == DialogMode.Edit ? (long?)m_seqNo : null;
            if (PriceItemRepository.ExistsItemCode(itemCode, exceptSeqNo))
            {
                m_isDuplicateChecked = false;
                m_checkedItemCode = string.Empty;
                MessageBox.Show("동일한 품목코드가 존재합니다.", "중복체크", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            m_isDuplicateChecked = true;
            m_checkedItemCode = itemCode;
            MessageBox.Show("사용 가능한 품목코드입니다.", "중복체크", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void tbItemCode_TextChanged(object sender, EventArgs e)
        {
            m_isDuplicateChecked = string.Equals(m_checkedItemCode, tbItemCode.Text.Trim(), StringComparison.Ordinal);
        }

        private bool CheckValidation()
        {
            string itemCode = tbItemCode.Text.Trim();
            string itemName = tbItemName.Text.Trim();

            if (string.IsNullOrWhiteSpace(itemName))
            {
                MessageBox.Show("품목명은 필수항목입니다.", "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                MessageBox.Show("품목코드는 필수항목입니다.", "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (m_DialogMode == DialogMode.New
                && (!m_isDuplicateChecked || !string.Equals(m_checkedItemCode, itemCode, StringComparison.Ordinal)))
            {
                MessageBox.Show("품목코드 중복체크를 진행해주세요.", "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(cbApplyPrice.Text))
            {
                MessageBox.Show("적용단가표는 필수항목입니다.", "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (GetDecimal(tbPrice.Value) <= 0)
            {
                MessageBox.Show("단가는 0보다 커야 합니다.", "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            DataRow row = PriceItemRepository.GetPriceItem(m_seqNo);
            if (row == null)
            {
                MessageBox.Show("상품정보를 찾을 수 없습니다.", "상품정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                return;
            }

            m_isInitializing = true;

            SelectComboText(cbCategory, row["PriceCategory"].ToString());
            LoadCategoryNameCombo();
            SelectComboText(cbCategoryName, row["CategoryName"].ToString());
            tbItemName.Text = row["PriceItemName"].ToString();
            tbItemCode.Text = row["ItemCode"].ToString();
            tbSpecText.Text = row["SpecText"].ToString();
            SelectComboText(cbUnit, row["UnitName"].ToString());
            SelectComboText(cbBillingBasis, row["BillingBasis"].ToString());
            tbDefaultWidth.Value = GetDecimal(row["DefaultWidth"]);
            tbDefaultHeight.Value = GetDecimal(row["DefaultHeight"]);
            SelectComboText(cbApplyPrice, row["PriceName"].ToString());
            tbPrice.Value = GetDecimal(row["UnitPrice"]);
            tbRemark.Text = row["Remark"].ToString();
            cbIsActive.SelectedIndex = IsActive(row["IsActive"]) ? 0 : 1;

            DateTime applyFrom;
            m_applyFrom = DateTime.TryParse(row["ApplyFrom"].ToString(), out applyFrom)
                ? applyFrom.Date
                : DateTime.Today;

            m_checkedItemCode = tbItemCode.Text.Trim();
            m_isDuplicateChecked = true;
            m_isInitializing = false;
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
