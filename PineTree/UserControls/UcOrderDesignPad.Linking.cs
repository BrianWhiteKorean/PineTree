using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcOrderDesignPad
    {
        private long? m_linkedOrderId;
        private DateTime? m_linkedDueAt;

        private async void LinkExistingOrder_Click(object sender, EventArgs e)
        {
            if (m_DialogMode != DialogMode.New || m_isSaving) return;
            using (var dialog = new DialogOrderList(tbCustName.Text))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.SelectedOrderId.HasValue) return;
                bool previousWait = UseWaitCursor;
                Cursor previousCursor = Cursor.Current;
                bool saveEnabled = btnSave.Enabled;
                inputButton1.Enabled = false;
                btnSave.Enabled = false;
                try
                {
                    UseWaitCursor = true; Cursor.Current = Cursors.WaitCursor;
                    DataRow root = await Task.Run(() => OrderRepository.GetOrderLinkSource(dialog.SelectedOrderId.Value));
                    if (!IsDisposed && !Disposing) ApplyLinkedOrder(root);
                }
                catch (Exception ex)
                {
                    if (!IsDisposed && !Disposing)
                        MessageBox.Show(this, "기존 주문에 연결하지 못했습니다.\r\n" + ex.Message, "주문 연결", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    if (!IsDisposed) { UseWaitCursor = previousWait; inputButton1.Enabled = m_DialogMode == DialogMode.New; btnSave.Enabled = saveEnabled; }
                    Cursor.Current = previousCursor;
                }
            }
        }

        private void ApplyLinkedOrder(DataRow root)
        {
            bool initializing = m_isInitializing;
            m_isInitializing = true;
            try
            {
                m_linkedOrderId = Convert.ToInt64(root["OrderId"]);
                tbOrderNo.Text = Convert.ToString(root["NextOrderNo"]) + " (예정)";
                tbOrderNo.ToolTipText = "연결 순번은 저장 시 확정됩니다. 원주문 " + Convert.ToString(root["OrderNo"]);
                dtmOrder.Value = Convert.ToDateTime(root["OrderDate"]);
                m_linkedDueAt = GetDateValue(root["RequestedDueAt"]);
                if (m_linkedDueAt.HasValue) dtmDelivery.Value = m_linkedDueAt.Value;
                else dtmDelivery.Text = string.Empty;
                m_sSelectedCustCode = Convert.ToString(root["CustCode"]);
                tbCustCode.Text = m_sSelectedCustCode;
                tbCustName.Text = Convert.ToString(root["CustName"]);
                SelectComboText(cbChannel, Convert.ToString(root["OrderChannel"]));
                SelectComboText(cbDeliveryType, Convert.ToString(root["DeliveryType"]));
                // Clear missing original values instead of retaining the previous selection.
                if (string.IsNullOrWhiteSpace(Convert.ToString(root["OrderChannel"]))) cbChannel.SelectedIndex = -1;
                if (string.IsNullOrWhiteSpace(Convert.ToString(root["DeliveryType"]))) cbDeliveryType.SelectedIndex = -1;
                tbDeliveryFee.Value = GetDecimal(root["DeliveryFee"]);
                SetLinkedHeaderLocked(true);
            }
            finally { m_isInitializing = initializing; }
            RefreshSelectedProductPrice();
        }

        private void SetLinkedHeaderLocked(bool locked)
        {
            dtmOrder.Enabled = dtmDelivery.Enabled = !locked;
            tbCustName.Enabled = tbCustCode.Enabled = btnCustList.Enabled = !locked;
            cbChannel.Enabled = cbDeliveryType.Enabled = tbDeliveryFee.Enabled = !locked;
            tbRemark.Enabled = true;
            inputButton1.Enabled = m_DialogMode == DialogMode.New;
        }
    }
}
