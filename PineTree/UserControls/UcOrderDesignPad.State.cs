using System;
using System.Data;
using System.Linq;
using PineTree.Classes;
using PineTree.UserControlsOrder;

namespace PineTree.UserControls
{
    public partial class UcOrderDesignPad
    {
        private OrderPreviewSnapshot m_savedSourcePreview;
        private object[] m_initialDraftInputs;

        internal bool HasDraftChanges =>
            m_orderItems.Rows.Count > 0 ||
            !string.IsNullOrEmpty(m_sourceDocument.ReferencePath) ||
            m_sourceDocument.ServerFile != null ||
            !m_initialDraftInputs.SequenceEqual(CaptureDraftInputs());

        private object[] CaptureDraftInputs()
        {
            var editor = m_orderEditor as IOrderItemEditor;
            return new object[]
            {
                m_orderId,
                m_linkedOrderId,
                tbCustName.Text,
                tbCustCode.Text,
                GetDateValue(dtmOrder.Value),
                GetDateValue(dtmDelivery.Value),
                cbChannel.Text,
                cbDeliveryType.Text,
                GetDecimal(tbDeliveryFee.Value),
                m_shippingDetails?.ConsigneeName ?? string.Empty,
                m_shippingDetails?.ConsigneePhone ?? string.Empty,
                m_shippingDetails?.ConsigneeZipCode ?? string.Empty,
                m_shippingDetails?.ConsigneeAddress ?? string.Empty,
                m_shippingDetails?.ConsigneeDetailAddress ?? string.Empty,
                m_shippingDetails?.ShipperName ?? string.Empty,
                m_shippingDetails?.ShipperPhone ?? string.Empty,
                m_shippingDetails?.ShipperZipCode ?? string.Empty,
                m_shippingDetails?.ShipperAddress ?? string.Empty,
                m_shippingDetails?.ShipperDetailAddress ?? string.Empty,
                tbRemark.Text,
                tbOrderName.Text,
                GetSelectedEquipmentName(),
                GetSelectedPriceCategory(),
                cbCategoryName.Text,
                cbItem.Text,
                GetDecimal(tbQty.Value),
                editor?.CaptureEditorState().ToJson()
            };
        }

        private void UpdateAutomaticSpecification()
        {
            var editor = m_orderEditor as IOrderItemEditor;
            tbSpec.ReadOnly = true;
            tbSpec.Enabled = true;
            tbSpec.Text = editor == null ? string.Empty : OrderDesignPadState.FormatSpecification(editor.WidthMm, editor.HeightMm, GetDecimal(tbQty.Value));
        }

        private string CaptureDesignPadState()
        {
            
            var editor = m_orderEditor as IOrderItemEditor;
            return new OrderDesignPadState {
                EquipmentName = GetSelectedEquipmentName(),
                PriceCategory = GetSelectedPriceCategory(),
                CategoryName = cbCategoryName.Text,
                ProductName = cbItem.Text,
                SpecText = tbSpec.Text,
                UnitPrice = m_selectedProductPrice == null ? (decimal?)null : GetDecimal(m_selectedProductPrice["CurrentUnitPrice"]),
                Editor = editor == null ? null : editor.CaptureEditorState()
            }.ToJson();
        }

        private void ApplyDesignPadState(string json)
        {
            var state = OrderDesignPadState.FromJson(json);
            if (state == null) return;
            m_loadingProductSelection = true;
            try {
                foreach (var radio in new[] { rbEquipment1, rbEquipment2 })
                    radio.Checked = string.Equals(radio.Text.Replace("&&", "&").Trim(), state.EquipmentName, StringComparison.Ordinal);
                SelectComboText(cbCategory, state.PriceCategory);
                SelectComboText(cbCategoryName, state.CategoryName);
                SelectComboText(cbItem, state.ProductName);
                cbCategory.Enabled = cbCategoryName.Enabled = cbItem.Enabled = false;
                rbEquipment1.Enabled = rbEquipment2.Enabled = false;
                tbSpec.Enabled = true;
                m_productPriceStatus = "저장 시점의 제품 단가 적용 · 부자재 목록 합계 반영";
                m_selectedProductPrice = null;
                if (state.UnitPrice.HasValue) {
                    var prices = new DataTable();
                    prices.Columns.Add("CurrentUnitPrice", typeof(decimal)); prices.Columns.Add("CustCode");
                    m_selectedProductPrice = prices.Rows.Add(state.UnitPrice.Value, m_sSelectedCustCode);
                }
            }
            finally { m_loadingProductSelection = false; }
            UpdateSelectedOrderEditor();
            var editor = m_orderEditor as IOrderItemEditor;
            if (editor != null && state.Editor != null) editor.RestoreEditorState(state.Editor);
            if (m_sourceDocument.UseSavedPreviewsOnly) m_sourceDocument.SetSavedPreview(m_savedSourcePreview);
            m_sourcePreview.Bind(m_sourceDocument);
            UpdateSourceTransformPreview();
            UpdateOrderPriceSummary(true);
        }
    }
}
