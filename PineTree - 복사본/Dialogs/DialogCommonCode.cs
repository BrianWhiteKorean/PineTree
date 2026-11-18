using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogCommonCode : Form
    {
        private readonly long? _seqNo;
        private bool _isSaving;
        public long SavedSeqNo { get; private set; }
        DialogMode m_DialogMode = DialogMode.New;

        public DialogCommonCode() : this((string)null)
        {
        }

        public DialogCommonCode(string groupName)
        {
            InitializeComponent();
            tbGroupName.Text = groupName ?? string.Empty;
            tbGroupName.ReadOnly = true;
            tbItemName.ReadOnly = false;
            tbItemName.MaxLength = 50;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AcceptButton = btnOK;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnOK.Click += BtnOK_Click;
            Load += DialogCommonCode_Load;
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (_isSaving) e.Cancel = true; };
            Text = "공통코드 추가";
        }

        public DialogCommonCode(long seqNo) : this((string)null)
        {
            _seqNo = seqNo;
            Text = "공통코드 수정";

            m_DialogMode = DialogMode.Edit;
        }

        private async void DialogCommonCode_Load(object sender, EventArgs e)
        {
            if (!_seqNo.HasValue)
            {
                tbItemName.Select();
                return;
            }
            btnOK.Enabled = false;
            try
            {
                DataRow item = await Task.Run(() => CommonCodeRepository.GetCommonCode(_seqNo.Value));
                if (IsDisposed) return;
                if (item == null)
                    throw new InvalidOperationException("수정할 항목을 찾을 수 없습니다. 다시 조회해주세요.");
                tbGroupName.Text = Convert.ToString(item["GroupName"]);
                tbItemName.Text = Convert.ToString(item["ItemName"]);
                btnOK.Enabled = true;
                tbItemName.Select();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    MessageBox.Show(this, "공통코드를 불러오지 못했습니다.\r\n" + ex.Message,
                        "공통코드", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            }
        }

        private async void BtnOK_Click(object sender, EventArgs e)
        {
            if (_isSaving) return;
            try
            {
                string groupName = tbGroupName.Text;
                string itemName = tbItemName.Text;
                CommonCodeRepository.ValidateItem(groupName, itemName);
                _isSaving = true;
                btnOK.Enabled = false;
                btnCancel.Enabled = false;
                tbItemName.Enabled = false;
                SavedSeqNo = await Task.Run(() => CommonCodeRepository.SaveItem(_seqNo, groupName, itemName));
                _isSaving = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show(this, ex.Message, "공통코드 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _isSaving = false;
                if (!IsDisposed)
                {
                    btnOK.Enabled = true;
                    btnCancel.Enabled = true;
                    tbItemName.Enabled = true;
                }
            }
        }

        private void DialogCommonCode_KeyDown(object sender, KeyEventArgs e)
        {
            if( e.KeyCode == Keys.Escape )
            {
                if( m_DialogMode == DialogMode.Edit )
                {
                    DialogResult = DialogResult.Cancel;
                }
            }
        }
    }
}