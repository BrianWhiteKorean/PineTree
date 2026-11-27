using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;
using PineTree.Properties;

namespace PineTree.UserControls
{
    internal static class OrderProductionStatusCell
    {
        internal const string ColumnKey = "Status";
        private static readonly Image NewImage = Resources.미작업;
        private static readonly Image ReceivedImage = Resources.접수;
        private static readonly Image PrintingImage = Resources.출력중;
        private static readonly Image PostProcessImage = Resources.후가공;
        private static readonly Image PackingWaitingImage = Resources.포장대기;
        private static readonly Image PackingCompleteImage = Resources.포장완료;

        internal static PagedGridCell Create(string value)
        {
            if (!OrderProductionStatusHelper.TryParse(value, out OrderProductionStatus status) && !string.IsNullOrWhiteSpace(value))
                return new PagedGridCell(value.Trim()) { CellType = PagedGridCellType.Badge };

            switch (status)
            {
                case OrderProductionStatus.New:
                    return CreateImage("미작업", NewImage, PagedGridCellType.ImageButton);
                case OrderProductionStatus.Design:
                    return CreateImage("접수", ReceivedImage);
                case OrderProductionStatus.Printing:
                    return CreateImage("출력중", PrintingImage);
                case OrderProductionStatus.PostProcess:
                    return CreateImage("후가공", PostProcessImage);
                case OrderProductionStatus.PackingWaiting:
                    return CreateImage("포장대기", PackingWaitingImage);
                case OrderProductionStatus.PackingComplete:
                    return CreateImage("포장완료", PackingCompleteImage);
                default:
                    return new PagedGridCell(value.Trim()) { CellType = PagedGridCellType.Badge };
            }
        }

        private static PagedGridCell CreateImage(string text, Image image, PagedGridCellType type = PagedGridCellType.Image)
        {
            return new PagedGridCell(text) { CellType = type, Image = image };
        }

        internal static async Task<bool> FeedAsync(Control owner, long orderId, PagedGridCell cell)
        {
            if (cell.CellType != PagedGridCellType.ImageButton || !owner.Enabled || orderId <= 0)
                return false;

            OrderProductionStatus status;
            using (var dialog = new DialogFeeding())
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK || !dialog.SelectedStatus.HasValue)
                    return false;

                status = dialog.SelectedStatus.Value;
            }

            owner.Enabled = false;
            try
            {
                bool updated = await Task.Run(() => OrderRepository.TryStartProduction(orderId, status));
                if (owner.IsDisposed) return false;
                if (!updated)
                    MessageBox.Show(owner, "주문의 진행상태가 이미 변경되었거나 주문이 삭제되었습니다.",
                        "작업 이송", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return true;
            }
            catch (Exception ex)
            {
                if (!owner.IsDisposed)
                    MessageBox.Show(owner, "진행상태를 저장하지 못했습니다.\r\n" + ex.Message,
                        "작업 이송", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            finally
            {
                if (!owner.IsDisposed) owner.Enabled = true;
            }
        }
    }
}
