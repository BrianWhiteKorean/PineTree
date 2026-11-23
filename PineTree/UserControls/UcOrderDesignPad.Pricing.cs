using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.UserControlsOrder;

namespace PineTree.UserControls
{
    public partial class UcOrderDesignPad
    {
        private const decimal MinimumBillableArea = 1m;
        private bool m_pricingInitialized;
        private bool m_updatingPricing;
        private int m_productPriceRequestVersion;
        private DataRow m_selectedProductPrice;
        private string m_productPriceStatus = "제품을 선택해주세요";
        private ToolTip m_priceToolTip;

        private void InitializeOrderPricing()
        {
            if (m_pricingInitialized) return;
            m_pricingInitialized = true;
            tbSpec.ReadOnly = true;
            tbSpec.Enabled = true;
            m_updatingPricing = true;
            try
            {
                foreach (var input in new[] { tbArea, tbUnitPrice, tbAmount, tbWorkSum, tbAccessory, tbTotalAmount })
                    input.ReadOnly = true;
                foreach (var input in new[] { tbArea, tbUnitPrice, tbAmount, tbWorkSum, tbAccessory, tbTotalAmount, tbQty })
                {
                    input.Culture = 1042;
                    input.UserCultureOverride = false;
                }
                tbArea.FormatType = C1.Win.C1Input.FormatTypeEnum.CustomFormat;
                tbArea.CustomFormat = "#,##0.00";
                foreach (var input in new[] { tbUnitPrice, tbAmount, tbWorkSum, tbAccessory, tbTotalAmount, tbQty })
                {
                    input.FormatType = C1.Win.C1Input.FormatTypeEnum.CustomFormat;
                    input.CustomFormat = "#,##0";
                }
                tbQty.Value = 1m;
                tbQty.ValueChanged += FooterQuantityChanged;
                m_priceToolTip = new ToolTip();
                Disposed += delegate { ++m_productPriceRequestVersion; m_priceToolTip.Dispose(); };
            }
            finally { m_updatingPricing = false; }
            UpdateOrderPriceSummary(false);
        }

        private void ResetOrderPricing()
        {
            InvalidateSelectedProductPrice();
            m_updatingPricing = true;
            try { tbQty.Value = 1m; }
            finally { m_updatingPricing = false; }
            UpdateOrderPriceSummary(false);
        }

        private void InvalidateSelectedProductPrice()
        {
            ++m_productPriceRequestVersion;
            m_selectedProductPrice = null;
            m_productPriceStatus = "제품을 선택해주세요";
            UpdateOrderPriceSummary(false);
        }

        private async void RefreshSelectedProductPrice()
        {
            if (!IsDisposed && !Disposing && m_pricingInitialized)
                await ReloadSelectedProductPriceAsync();
        }

        private async Task ReloadSelectedProductPriceAsync()
        {
            int version = ++m_productPriceRequestVersion;
            m_selectedProductPrice = null;
            string priceCategory = GetSelectedPriceCategory();
            string category = cbCategoryName.SelectedIndex >= 0 ? cbCategoryName.Text.Trim() : null;
            string product = cbItem.Enabled && cbItem.SelectedIndex > 0 ? cbItem.Text.Trim() : null;
            string customer = m_sSelectedCustCode;
            int? equipmentId = m_selectedEquipmentId;
            m_productPriceStatus = string.IsNullOrEmpty(product) ? "제품을 선택해주세요" : "단가 조회 중";
            UpdateOrderPriceSummary(false);
            if (string.IsNullOrEmpty(priceCategory) || string.IsNullOrEmpty(category) || string.IsNullOrEmpty(product)) return;
            try
            {
                using (DataTable prices = await Task.Run(() => PriceItemRepository.GetDesignPadProductPrices(priceCategory, category, product, customer, equipmentId)))
                {
                    if (IsDisposed || Disposing || version != m_productPriceRequestVersion) return;
                    ApplyProductPriceCandidates(prices, customer);
                }
            }
            catch (Exception)
            {
                if (IsDisposed || Disposing || version != m_productPriceRequestVersion) return;
                m_selectedProductPrice = null;
                m_productPriceStatus = "단가를 조회하지 못했습니다. 거래처 또는 제품을 다시 선택해주세요.";
                UpdateOrderPriceSummary(false);
            }
        }

        private static List<DataRow> PreferredProductPrices(DataTable prices, string customer)
        {
            var dedicated = new List<DataRow>();
            var basic = new List<DataRow>();
            customer = (customer ?? string.Empty).Trim();
            foreach (DataRow row in prices.Rows)
            {
                if (row.IsNull("CurrentUnitPrice")) continue;
                decimal price;
                if (!decimal.TryParse(Convert.ToString(row["CurrentUnitPrice"]), out price) || price < 0) continue;
                string rowCustomer = Convert.ToString(row["CustCode"]).Trim();
                if (rowCustomer.Length == 0) basic.Add(row);
                else if (customer.Length > 0 && string.Equals(rowCustomer, customer, StringComparison.Ordinal)) dedicated.Add(row);
            }
            return dedicated.Count > 0 ? dedicated : basic;
        }

        private void ApplyProductPriceCandidates(DataTable prices, string customer)
        {
            var candidates = PreferredProductPrices(prices, customer);
            m_selectedProductPrice = null;
            if (candidates.Count == 1)
            {
                m_selectedProductPrice = candidates[0].Table.Clone().NewRow();
                m_selectedProductPrice.ItemArray = (object[])candidates[0].ItemArray.Clone();
                m_productPriceStatus = string.IsNullOrWhiteSpace(Convert.ToString(candidates[0]["CustCode"]))
                    ? "기본 단가 적용 · 후가공비는 현재 0원 · 부자재 목록 합계 적용 · 부가세 별도"
                    : "거래처 전용 단가 적용 · 후가공비는 현재 0원 · 부자재 목록 합계 적용 · 부가세 별도";
            }
            else m_productPriceStatus = candidates.Count == 0 ? "적용 가능한 단가가 없습니다." : "단가 후보가 여러 개여서 자동 확정할 수 없습니다.";
            UpdateOrderPriceSummary(false);
        }

        private void FooterQuantityChanged(object sender, EventArgs e)
        {
            if (m_updatingPricing) return;
            decimal quantity = Math.Max(0m, Math.Round(GetDecimal(tbQty.Value), 0, MidpointRounding.AwayFromZero));
            m_updatingPricing = true;
            try
            {
                tbQty.Value = quantity;
                var editor = m_orderEditor as IOrderItemEditor;
                if (editor != null) editor.SetOrderQuantity(quantity);
            }
            finally { m_updatingPricing = false; }
            UpdateOrderPriceSummary(false);
        }

        private void UpdateOrderPriceSummary(bool useEditorQuantity)
        {
            if (!m_pricingInitialized || m_updatingPricing || IsDisposed || Disposing) return;
            m_updatingPricing = true;
            try
            {
                var editor = m_orderEditor as IOrderItemEditor;
                decimal area = editor == null ? 0m : editor.WidthMm * editor.HeightMm / 1000000m;
                decimal quantity = Math.Max(0m, Math.Round(useEditorQuantity && editor != null ? editor.Quantity : GetDecimal(tbQty.Value), 0, MidpointRounding.AwayFromZero));
                tbQty.Value = quantity;
                UpdateAutomaticSpecification();
                // Keep the material editor's actual area unchanged; the footer shows billable area per item.
                decimal billableArea = area > 0m ? Math.Max(MinimumBillableArea, area) : 0m;
                tbArea.Value = billableArea;
                tbWorkSum.Value = 0m;
                decimal accessoryTotal = 0m;
                if (m_orderItems != null)
                    foreach (DataRow row in m_orderItems.Rows)
                        if (row.RowState != DataRowState.Deleted) accessoryTotal += GetDecimal(row["TotalAmount"]);
                tbAccessory.Value = accessoryTotal;
                if (m_selectedProductPrice == null)
                {
                    tbUnitPrice.Value = tbAmount.Value = tbTotalAmount.Value = DBNull.Value;
                }
                else
                {
                    decimal unitPrice = GetDecimal(m_selectedProductPrice["CurrentUnitPrice"]);
                    decimal perItem = billableArea * unitPrice;
                    tbUnitPrice.Value = unitPrice;
                    tbAmount.Value = perItem;
                    tbTotalAmount.Value = Math.Round(perItem * quantity + accessoryTotal, 0, MidpointRounding.AwayFromZero);
                }
                if (m_priceToolTip != null)
                {
                    m_priceToolTip.SetToolTip(tbArea, string.Format("실제 면적 {0:0.######}㎡ / 1개당 최소 청구 면적 {1:0.00}㎡", area, MinimumBillableArea));
                    foreach (var input in new[] { tbUnitPrice, tbAmount, tbTotalAmount })
                        m_priceToolTip.SetToolTip(input, m_productPriceStatus + " / 1개당 최소 1.00㎡, 선택 제품의 단가 적용");
                }
            }
            finally { m_updatingPricing = false; }
        }
    }
}
