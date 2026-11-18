using System;
using System.Data;
using PineTree.Classes;
using PineTree.UserControlsOrder;

namespace PineTree.Dialogs
{
    public partial class DialogOrderDesignPad
    {
        private OrderPreviewSnapshot m_savedSourcePreview;
        private bool m_hasDesignPadState;

        private void UpdateAutomaticSpecification()
        {
            var editor = m_orderEditor as IOrderItemEditor;
            tbSpec.ReadOnly = true;
            tbSpec.Enabled = true;
            tbSpec.Text = editor == null ? string.Empty : OrderDesignPadState.FormatSpecification(editor.WidthMm, editor.HeightMm, GetDecimal(tbQty.Value));
        }

        private string CaptureDesignPadState()
        {
            if (m_DialogMode == DialogMode.Edit && !m_hasDesignPadState) return null;
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
            m_hasDesignPadState = true;
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
