using PineTree.Classes;
using System;
using System.Data;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DailogCustInfo : Form
    {
        private DialogMode m_DialogMode = DialogMode.New;
        private long m_seqNo = -1;
        private bool m_isDuplicateChecked;
        private string m_checkedCustomerCode = string.Empty;

        public DailogCustInfo()
        {
            InitializeComponent();
            InitEvent();
        }

        public DailogCustInfo(long seqNo)
        {
            InitializeComponent();
            InitEvent();

            m_seqNo = seqNo;
            inputGroupHeader1.Text = "고객정보 수정";
            m_DialogMode = DialogMode.Edit;
            ReadDB();
        }

        private void InitEvent()
        {
            c1Button1.Click += btnSave_Click;
            c1Button2.Click += btnCancel_Click;
            btnDuplCheck.Click += btnDuplCheck_Click;
            tbCustCode.TextChanged += tbCustCode_TextChanged;
        }

        private void ReadDB()
        {
            DataRow row = CustomerRepository.GetCustomer(m_seqNo);
            if (row == null)
            {
                MessageBox.Show("고객정보를 찾을 수 없습니다.", "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                return;
            }

            tbCustCode.Text = row["CustCode"].ToString();
            tbECountCustCode.Text = row["ECountCustomerCode"].ToString();
            tbCustName.Text = row["CustomerName"].ToString();
            tbRepName.Text = row["RepresentativeName"].ToString();
            tbPhone.Text = row["Phone"].ToString();
            tbBusinessNum.Text = row["BusinessNum"].ToString();

            m_isDuplicateChecked = true;
            m_checkedCustomerCode = tbCustCode.Text.Trim();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!CheckValidation())
            {
                return;
            }

            try
            {
                if (m_DialogMode == DialogMode.New)
                {
                    CustomerRepository.InsertCustomer(
                        GetCustCode(),
                        tbECountCustCode.Text,
                        tbCustName.Text,
                        tbRepName.Text,
                        tbPhone.Text,
                        tbBusinessNum.Text);
                }
                else
                {
                    CustomerRepository.UpdateCustomer(
                        m_seqNo,
                        GetCustCode(),
                        tbECountCustCode.Text,
                        tbCustName.Text,
                        tbRepName.Text,
                        tbPhone.Text,
                        tbBusinessNum.Text);
                }

                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류가 발생했습니다.\r\n" + ex.Message, "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void btnDuplCheck_Click(object sender, EventArgs e)
        {
            string customerCode = tbCustCode.Text.Trim();
            if (string.IsNullOrWhiteSpace(customerCode))
            {
                MessageBox.Show("거래처코드를 입력해주세요.", "중복체크", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long custCode;
            if (!long.TryParse(customerCode, out custCode))
            {
                MessageBox.Show("거래처코드는 숫자만 입력해주세요.", "중복체크", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long? exceptSeqNo = m_DialogMode == DialogMode.Edit ? (long?)m_seqNo : null;
            if (CustomerRepository.ExistsCustCode(custCode, exceptSeqNo))
            {
                m_isDuplicateChecked = false;
                m_checkedCustomerCode = string.Empty;
                MessageBox.Show("동일한 거래처코드가 존재합니다.", "중복체크", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            m_isDuplicateChecked = true;
            m_checkedCustomerCode = customerCode;
            MessageBox.Show("사용 가능한 거래처코드입니다.", "중복체크", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void tbCustCode_TextChanged(object sender, EventArgs e)
        {
            m_isDuplicateChecked = string.Equals(m_checkedCustomerCode, tbCustCode.Text.Trim(), StringComparison.Ordinal);
        }

        private bool CheckValidation()
        {
            string customerCode = tbCustCode.Text.Trim();
            string customerName = tbCustName.Text.Trim();

            if (string.IsNullOrWhiteSpace(customerCode))
            {
                MessageBox.Show("거래처코드는 필수항목입니다.", "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            long custCode;
            if (!long.TryParse(customerCode, out custCode))
            {
                MessageBox.Show("거래처코드는 숫자만 입력해주세요.", "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(customerName))
            {
                MessageBox.Show("거래처명은 필수항목입니다.", "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!m_isDuplicateChecked || !string.Equals(m_checkedCustomerCode, customerCode, StringComparison.Ordinal))
            {
                MessageBox.Show("거래처코드 중복체크를 진행해주세요.", "고객정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private long GetCustCode()
        {
            return long.Parse(tbCustCode.Text.Trim());
        }
    }
}
