using System;
using System.Collections.Generic;
using System.Data;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;
using PineTree.PdfEngine;

namespace PineTree.Classes
{
    public sealed class OrderPreviewPageSnapshot
    {
        public int PageIndex { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }
        public OrderServerFile ImageFile { get; set; }
        public OrderDocumentResult Analysis { get; set; }
    }

    public sealed class OrderPreviewSnapshot
    {
        public int Version { get; set; } = 1;
        public string SourceFileInfo { get; set; }
        public List<OrderPreviewPageSnapshot> Pages { get; set; } = new List<OrderPreviewPageSnapshot>();
        private static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }; }
        public string ToJson() { return Serializer().Serialize(this); }
        public static OrderPreviewSnapshot FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var snapshot = Serializer().Deserialize<OrderPreviewSnapshot>(json);
            if (snapshot == null || snapshot.Version != 1 || snapshot.Pages == null)
                throw new InvalidOperationException("저장된 주문 미리보기 형식이 올바르지 않습니다.");
            for (int i = 0; i < snapshot.Pages.Count; ++i)
            {
                var page = snapshot.Pages[i];
                if (page == null || page.PageIndex != i || page.ImageFile == null || page.Analysis == null
                    || page.PixelWidth <= 0 || page.PixelHeight <= 0 || string.IsNullOrEmpty(page.ImageFile.FileId) || !page.ImageFile.FileId.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("저장된 대지 미리보기 정보가 올바르지 않습니다.");
                page.ImageFile.Validate();
            }
            return snapshot;
        }
    }

    public static class OrderPreviewSnapshotRepository
    {
        public static OrderPreviewSnapshot Get(long orderId)
        {
            using (DataTable rows = DB.MySqlQueryRead(@"SELECT preview.PreviewJSON, preview.SourceFileInfo FROM OrderPreviewSnapshots preview
INNER JOIN OrderInfo info ON info.OrderNo = preview.OrderNo AND info.OrderId = preview.OrderId
WHERE info.OrderId = @OrderId", new[] { new ParamValuePair("@OrderId", orderId) }))
            {
                if (rows.Rows.Count == 0) return null;
                var snapshot = OrderPreviewSnapshot.FromJson(Convert.ToString(rows.Rows[0][0]));
                snapshot.SourceFileInfo = rows.Rows[0][1] == DBNull.Value ? null : Convert.ToString(rows.Rows[0][1]);
                return snapshot;
            }
        }

        internal static void Save(MySqlConnection conn, MySqlTransaction transaction, long orderId, string json)
        {
            // Null preserves a snapshot when an older editor only changes order fields.
            if (string.IsNullOrWhiteSpace(json)) return;
            var snapshot = OrderPreviewSnapshot.FromJson(json);
            using (var command = new MySqlCommand(@"INSERT INTO OrderPreviewSnapshots (OrderNo, OrderId, SourceFileInfo, PreviewJSON)
SELECT OrderNo, OrderId, @Info, @Preview FROM OrderInfo WHERE OrderId = @OrderId
ON DUPLICATE KEY UPDATE SourceFileInfo = VALUES(SourceFileInfo), PreviewJSON = VALUES(PreviewJSON)", conn, transaction))
            {
                command.Parameters.AddWithValue("@OrderId", orderId);
                command.Parameters.AddWithValue("@Info", (object)snapshot.SourceFileInfo ?? DBNull.Value);
                command.Parameters.AddWithValue("@Preview", json);
                command.ExecuteNonQuery();
            }
        }
    }
}
