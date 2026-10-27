using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace PineTree.Classes
{
    public class OrderSaveData
    {
        public long? OrderId { get; set; }
        public long? OrderItemId { get; set; }
        public DateTime OrderDate { get; set; }
        public string CustCode { get; set; }
        public string OrderChannel { get; set; }
        public string DeliveryType { get; set; }
        public decimal DeliveryFee { get; set; }
        public DateTime? RequestedDueAt { get; set; }
        public string OrderTitle { get; set; }
        public string OrderDescription { get; set; }
        public string ProductionStatus { get; set; }
        public string SourceChannel { get; set; }
        public long PriceItemSeqNo { get; set; }
        public long? PriceItemPriceId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string CustomName { get; set; }
        public string SpecText { get; set; }
        public string BillingBasis { get; set; }
        public decimal? WidthMm { get; set; }
        public decimal? HeightMm { get; set; }
        public decimal? AreaPerQty { get; set; }
        public decimal? Qty { get; set; }
        public decimal? AreaQty { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal SupplyAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string ItemDescription { get; set; }
        public string RequestFilePath { get; set; }
        public string OutputFilePath { get; set; }
        public List<OrderProcessRuleData> ProcessRules { get; set; }
        public string CreatedBy { get; set; }
        public List<OrderItemSaveData> Items { get; set; }
    }

    public class OrderItemSaveData
    {
        public long? OrderItemId { get; set; }
        public int LineNo { get; set; }
        public long PriceItemSeqNo { get; set; }
        public long? PriceItemPriceId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string CustomName { get; set; }
        public string SpecText { get; set; }
        public string BillingBasis { get; set; }
        public decimal? WidthMm { get; set; }
        public decimal? HeightMm { get; set; }
        public decimal? AreaPerQty { get; set; }
        public decimal? Qty { get; set; }
        public decimal? AreaQty { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal SupplyAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string ItemDescription { get; set; }
        public string ProductionStatus { get; set; }
        public string EditorStateJson { get; set; }
        public string RequestFilePath { get; set; }
        public string OutputFilePath { get; set; }
        public List<OrderProcessRuleData> ProcessRules { get; set; }
    }

    public static class OrderRepository
    {
        public static DataTable GetOrdersByOrderDate(DateTime orderDate)
        {
            return GetOrders(null, null, orderDate, orderDate, null, false);
        }

        public static DataTable GetOrders(string custName, string custCode, DateTime orderDateFrom, DateTime orderDateTo, string orderChannel = null)
        {
            return GetOrders(custName, custCode, orderDateFrom, orderDateTo, orderChannel, true);
        }

        private static DataTable GetOrders(string custName, string custCode, DateTime orderDateFrom, DateTime orderDateTo, string orderChannel, bool includeAllItems)
        {
            string sql = @"
SELECT
    oi.OrderId,
    item.OrderItemId,
    oi.OrderDate,
    oi.ReceivedAt,
    oi.OrderNo,
    oi.CustCode,
    c.CustName,
    oi.OrderTitle,
    oi.ProductionStatus,
    oi.RequestedDueAt,
    oi.SourceChannel,
    item.ItemName,
    item.CustomName,
    item.SpecText,
    item.WidthMm,
    item.HeightMm,
    item.Qty,
    item.AreaQty,
    item.BillingBasis,
    priceItem.UnitName
FROM OrderInfo oi
LEFT JOIN Customers c
    ON c.CustCode = oi.CustCode
LEFT JOIN OrderItems item
    ON item.OrderId = oi.OrderId
    AND (@IncludeAllItems = 1 OR item.LineNo = 1)
LEFT JOIN PriceItems priceItem
    ON priceItem.SeqNo = item.PriceItemSeqNo
WHERE oi.OrderDate >= @OrderDateFrom
    AND oi.OrderDate < @OrderDateToExclusive
    AND (@CustName = '' OR c.CustName LIKE @CustName)
    AND (@CustCode = '' OR CAST(oi.CustCode AS CHAR) LIKE @CustCode)
    AND (@OrderChannel = '' OR oi.OrderChannel = @OrderChannel)
ORDER BY oi.ReceivedAt DESC, oi.OrderId DESC, item.LineNo, item.OrderItemId";

            return DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@OrderDateFrom", orderDateFrom.Date),
                new ParamValuePair("@OrderDateToExclusive", orderDateTo.Date.AddDays(1)),
                new ParamValuePair("@CustName", string.IsNullOrWhiteSpace(custName) ? string.Empty : "%" + custName.Trim() + "%"),
                new ParamValuePair("@CustCode", string.IsNullOrWhiteSpace(custCode) ? string.Empty : "%" + custCode.Trim() + "%"),
                new ParamValuePair("@OrderChannel", string.IsNullOrWhiteSpace(orderChannel) ? string.Empty : orderChannel.Trim()),
                new ParamValuePair("@IncludeAllItems", includeAllItems ? 1 : 0)
            });
        }

        public static Dictionary<OrderProductionStatus, int> GetStatusCountsByOrderDate(DateTime orderDate)
        {
            string sql = @"
SELECT
    ProductionStatus,
    COUNT(*) AS StatusCount
FROM OrderInfo
WHERE OrderDate = @OrderDate
GROUP BY ProductionStatus";

            DataTable dt = DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@OrderDate", orderDate.Date)
            });

            Dictionary<OrderProductionStatus, int> counts = CreateEmptyStatusCounts();
            foreach (DataRow row in dt.Rows)
            {
                OrderProductionStatus status;
                if (!OrderProductionStatusHelper.TryParse(row["ProductionStatus"].ToString(), out status))
                {
                    continue;
                }

                int count;
                if (!int.TryParse(row["StatusCount"].ToString(), out count))
                {
                    count = 0;
                }

                counts[status] = count;
            }

            return counts;
        }

        private const string OrderForEditSql = @"
SELECT
    oi.OrderId,
    oi.CustCode,
    c.CustName,
    oi.OrderChannel,
    oi.DeliveryType,
    oi.DeliveryFee,
    oi.OrderNo,
    oi.OrderDate,
    oi.ReceivedAt,
    oi.RequestedDueAt,
    oi.OrderTitle,
    oi.OrderDescription,
    oi.ProductionStatus,
    oi.SourceChannel,
    item.OrderItemId,
    item.PriceItemSeqNo,
    item.PriceItemPriceId,
    item.ItemCode,
    item.ItemName,
    item.CustomName,
    item.SpecText,
    item.BillingBasis,
    item.WidthMm,
    item.HeightMm,
    item.AreaPerQty,
    item.Qty,
    item.AreaQty,
    item.UnitPrice,
    item.SupplyAmount,
    item.VatAmount,
    item.TotalAmount,
    price.PriceName,
    requestFile.StoredPath AS RequestFilePath,
    outputFile.StoredPath AS OutputFilePath
FROM OrderInfo oi
LEFT JOIN Customers c
    ON c.CustCode = oi.CustCode
LEFT JOIN OrderItems item
    ON item.OrderId = oi.OrderId
    AND item.LineNo = 1
LEFT JOIN PriceItemPrices price
    ON price.PriceItemPriceId = item.PriceItemPriceId
LEFT JOIN OrderFiles requestFile
    ON requestFile.OrderId = oi.OrderId
    AND requestFile.OrderItemId = item.OrderItemId
    AND requestFile.FileRole = 'REQUEST'
    AND requestFile.IsActive = 1
LEFT JOIN OrderFiles outputFile
    ON outputFile.OrderId = oi.OrderId
    AND outputFile.OrderItemId = item.OrderItemId
    AND outputFile.FileRole = 'OUTPUT'
    AND outputFile.IsActive = 1
WHERE oi.OrderId = @OrderId
LIMIT 1";

        public static DataRow GetOrderForEdit(long orderId)
        {
            string sql = OrderForEditSql;

            DataTable dt = DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@OrderId", orderId)
            });

            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        private const string OrderItemsForEditSql = @"
SELECT
    item.OrderItemId,
    item.LineNo,
    item.PriceItemSeqNo,
    item.PriceItemPriceId,
    item.ItemCode,
    item.ItemName,
    item.CustomName,
    item.SpecText,
    item.BillingBasis,
    priceItem.UnitName,
    priceItem.PriceCategory,
    priceItem.CategoryName,
    price.ApplyFrom,
    item.WidthMm,
    item.HeightMm,
    item.Qty,
    item.AreaQty,
    item.UnitPrice,
    item.SupplyAmount,
    item.VatAmount,
    item.TotalAmount,
    item.ItemDescription,
    item.Remark,
    editorState.StateJSON AS EditorStateJson,
    price.PriceName,
    (SELECT f.StoredPath FROM OrderFiles f WHERE f.OrderId = item.OrderId AND f.OrderItemId = item.OrderItemId
        AND f.FileRole = 'REQUEST' AND f.IsActive = 1 ORDER BY f.OrderFileId LIMIT 1) AS RequestFilePath,
    (SELECT f.StoredPath FROM OrderFiles f WHERE f.OrderId = item.OrderId AND f.OrderItemId = item.OrderItemId
        AND f.FileRole = 'OUTPUT' AND f.IsActive = 1 ORDER BY f.OrderFileId LIMIT 1) AS OutputFilePath
FROM OrderItems item
LEFT JOIN PriceItems priceItem
    ON priceItem.SeqNo = item.PriceItemSeqNo
LEFT JOIN PriceItemPrices price
    ON price.PriceItemPriceId = item.PriceItemPriceId
LEFT JOIN OrderItemEditorStates editorState
    ON editorState.OrderItemId = item.OrderItemId
WHERE item.OrderId = @OrderId
ORDER BY item.LineNo, item.OrderItemId";

        public static DataTable GetOrderItemsForEdit(long orderId)
        {
            string sql = OrderItemsForEditSql;

            return DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@OrderId", orderId)
            });
        }

        public static long UpsertOrder(OrderSaveData order)
        {
            using (MySqlConnection conn = DB.CreateConnection())
            {
                conn.Open();
                using (MySqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        long orderId = SaveOrderCore(conn, transaction, order);
                        transaction.Commit();
                        return orderId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        internal static long SaveOrderCore(MySqlConnection conn, MySqlTransaction transaction, OrderSaveData order)
        {
            long orderId = UpsertOrderInfo(conn, transaction, order);
            List<long> savedOrderItemIds = new List<long>();
            List<OrderItemSaveData> items = GetOrderItems(order);

            for (int i = 0; i < items.Count; i++)
            {
                OrderItemSaveData item = items[i];
                item.LineNo = i + 1;

                long orderItemId = UpsertOrderItem(conn, transaction, orderId, item);
                savedOrderItemIds.Add(orderItemId);

                SaveFilesAndRules(conn, transaction, orderId, orderItemId, item, order.CreatedBy);
            }

            DeleteRemovedOrderItems(conn, transaction, orderId, savedOrderItemIds);
            return orderId;
        }

        public static void UpdateOrderItem(long orderId, OrderItemSaveData item)
        {
            if (orderId <= 0 || item == null || !item.OrderItemId.HasValue || item.OrderItemId.Value <= 0)
            {
                throw new ArgumentException("수정할 주문품목 정보가 올바르지 않습니다.");
            }

            using (MySqlConnection conn = DB.CreateConnection())
            {
                conn.Open();
                using (MySqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string updateSql = @"
UPDATE OrderItems
SET
    PriceItemSeqNo = @PriceItemSeqNo,
    PriceItemPriceId = @PriceItemPriceId,
    ItemCode = @ItemCode,
    ItemName = @ItemName,
    CustomName = @CustomName,
    SpecText = @SpecText,
    BillingBasis = @BillingBasis,
    WidthMm = @WidthMm,
    HeightMm = @HeightMm,
    AreaPerQty = @AreaPerQty,
    Qty = @Qty,
    AreaQty = @AreaQty,
    UnitPrice = @UnitPrice,
    SupplyAmount = @SupplyAmount,
    VatAmount = @VatAmount,
    TotalAmount = @TotalAmount,
    ItemDescription = @ItemDescription
WHERE OrderId = @OrderId
AND OrderItemId = @OrderItemId";

                        EnsureItemOwner(conn, transaction, orderId, item.OrderItemId.Value);
                        Execute(conn, transaction, updateSql, CreateOrderItemParameters(orderId, item.OrderItemId.Value, item));
                        SaveFilesAndRules(conn, transaction, orderId, item.OrderItemId.Value, item, null);
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static long UpsertOrderInfo(MySqlConnection conn, MySqlTransaction transaction, OrderSaveData order)
        {
            if (order.OrderId.HasValue && order.OrderId.Value > 0)
            {
                string updateSql = @"
UPDATE OrderInfo
SET
    CustCode = @CustCode,
    OrderChannel = @OrderChannel,
    DeliveryType = @DeliveryType,
    DeliveryFee = @DeliveryFee,
    OrderDate = @OrderDate,
    RequestedDueAt = @RequestedDueAt,
    OrderTitle = @OrderTitle,
    OrderDescription = @OrderDescription,
    ProductionStatus = @ProductionStatus,
    SourceChannel = @SourceChannel
WHERE OrderId = @OrderId";

                Execute(conn, transaction, updateSql, new[]
                {
                    new ParamValuePair("@OrderId", order.OrderId.Value),
                    new ParamValuePair("@CustCode", order.CustCode),
                    new ParamValuePair("@OrderChannel", EmptyToNull(order.OrderChannel)),
                    new ParamValuePair("@DeliveryType", EmptyToNull(order.DeliveryType)),
                    new ParamValuePair("@DeliveryFee", order.DeliveryFee),
                    new ParamValuePair("@OrderDate", order.OrderDate.Date),
                    new ParamValuePair("@RequestedDueAt", order.RequestedDueAt),
                    new ParamValuePair("@OrderTitle", EmptyToNull(order.OrderTitle)),
                    new ParamValuePair("@OrderDescription", EmptyToNull(order.OrderDescription)),
                    new ParamValuePair("@ProductionStatus", order.ProductionStatus),
                    new ParamValuePair("@SourceChannel", EmptyToNull(order.SourceChannel))
                });

                return order.OrderId.Value;
            }

            string orderNo = CreateOrderNo(conn, transaction, order.OrderDate);
            string insertSql = @"
INSERT INTO OrderInfo
(
    CustCode,
    OrderChannel,
    DeliveryType,
    DeliveryFee,
    OrderNo,
    OrderDate,
    ReceivedAt,
    RequestedDueAt,
    OrderTitle,
    OrderDescription,
    ProductionStatus,
    SourceChannel
)
VALUES
(
    @CustCode,
    @OrderChannel,
    @DeliveryType,
    @DeliveryFee,
    @OrderNo,
    @OrderDate,
    NOW(),
    @RequestedDueAt,
    @OrderTitle,
    @OrderDescription,
    @ProductionStatus,
    @SourceChannel
);
SELECT LAST_INSERT_ID();";

            object value = Scalar(conn, transaction, insertSql, new[]
            {
                new ParamValuePair("@CustCode", order.CustCode),
                new ParamValuePair("@OrderChannel", EmptyToNull(order.OrderChannel)),
                new ParamValuePair("@DeliveryType", EmptyToNull(order.DeliveryType)),
                new ParamValuePair("@DeliveryFee", order.DeliveryFee),
                new ParamValuePair("@OrderNo", orderNo),
                new ParamValuePair("@OrderDate", order.OrderDate.Date),
                new ParamValuePair("@RequestedDueAt", order.RequestedDueAt),
                new ParamValuePair("@OrderTitle", EmptyToNull(order.OrderTitle)),
                new ParamValuePair("@OrderDescription", EmptyToNull(order.OrderDescription)),
                new ParamValuePair("@ProductionStatus", order.ProductionStatus),
                new ParamValuePair("@SourceChannel", EmptyToNull(order.SourceChannel))
            });

            return Convert.ToInt64(value);
        }

        private static long UpsertOrderItem(MySqlConnection conn, MySqlTransaction transaction, long orderId, OrderItemSaveData item)
        {
            long? orderItemId = item.OrderItemId;

            if (orderItemId.HasValue && orderItemId.Value > 0)
            {
                EnsureItemOwner(conn, transaction, orderId, orderItemId.Value);
                string updateSql = @"
UPDATE OrderItems
SET
    LineNo = @LineNo,
    PriceItemSeqNo = @PriceItemSeqNo,
    PriceItemPriceId = @PriceItemPriceId,
    ItemCode = @ItemCode,
    ItemName = @ItemName,
    CustomName = @CustomName,
    SpecText = @SpecText,
    BillingBasis = @BillingBasis,
    WidthMm = @WidthMm,
    HeightMm = @HeightMm,
    AreaPerQty = @AreaPerQty,
    Qty = @Qty,
    AreaQty = @AreaQty,
    UnitPrice = @UnitPrice,
    SupplyAmount = @SupplyAmount,
    VatAmount = @VatAmount,
    TotalAmount = @TotalAmount,
    ItemDescription = @ItemDescription,
    ProductionStatus = @ProductionStatus
WHERE OrderItemId = @OrderItemId";

                Execute(conn, transaction, updateSql, CreateOrderItemParameters(orderId, orderItemId.Value, item));
                return orderItemId.Value;
            }

            string insertSql = @"
INSERT INTO OrderItems
(
    OrderId,
    LineNo,
    PriceItemSeqNo,
    PriceItemPriceId,
    ItemCode,
    ItemName,
    CustomName,
    SpecText,
    BillingBasis,
    WidthMm,
    HeightMm,
    AreaPerQty,
    Qty,
    AreaQty,
    UnitPrice,
    SupplyAmount,
    VatAmount,
    TotalAmount,
    ItemDescription,
    ProductionStatus
)
VALUES
(
    @OrderId,
    @LineNo,
    @PriceItemSeqNo,
    @PriceItemPriceId,
    @ItemCode,
    @ItemName,
    @CustomName,
    @SpecText,
    @BillingBasis,
    @WidthMm,
    @HeightMm,
    @AreaPerQty,
    @Qty,
    @AreaQty,
    @UnitPrice,
    @SupplyAmount,
    @VatAmount,
    @TotalAmount,
    @ItemDescription,
    @ProductionStatus
);
SELECT LAST_INSERT_ID();";

            object value = Scalar(conn, transaction, insertSql, CreateOrderItemParameters(orderId, 0, item));
            return Convert.ToInt64(value);
        }

        private static void EnsureItemOwner(MySqlConnection conn, MySqlTransaction transaction, long orderId, long itemId)
        {
            object owner = Scalar(conn, transaction, "SELECT OrderId FROM OrderItems WHERE OrderItemId = @Item FOR UPDATE",
                new[] { new ParamValuePair("@Item", itemId) });
            if (owner == null || owner == DBNull.Value || Convert.ToInt64(owner) != orderId)
                throw new InvalidOperationException("다른 주문의 품목은 수정할 수 없습니다.");
        }

        private static void SaveFilesAndRules(MySqlConnection conn, MySqlTransaction transaction, long orderId,
            long itemId, OrderItemSaveData item, string createdBy)
        {
            EnsureItemOwner(conn, transaction, orderId, itemId);
            OrderItemEditorStateRepository.Save(conn, transaction, itemId, item.EditorStateJson);
            // A null path means preserve; an empty path explicitly clears that file role.
            if (item.RequestFilePath != null) UpsertOrderFile(conn, transaction, orderId, itemId, "REQUEST", item.RequestFilePath, createdBy);
            if (item.OutputFilePath != null) UpsertOrderFile(conn, transaction, orderId, itemId, "OUTPUT", item.OutputFilePath, createdBy);
            long? requestId = GetActiveFileId(conn, transaction, orderId, itemId, "REQUEST");
            long? outputId = GetActiveFileId(conn, transaction, orderId, itemId, "OUTPUT");
            if (requestId.HasValue && outputId.HasValue)
                UpsertOrderFileRelation(conn, transaction, requestId.Value, outputId.Value);
            OrderProcessRuleRepository.Save(conn, transaction, orderId, itemId, requestId, item.ProcessRules);
        }

        private static long? GetActiveFileId(MySqlConnection conn, MySqlTransaction transaction, long orderId, long itemId, string role)
        {
            object value = Scalar(conn, transaction, @"
SELECT OrderFileId FROM OrderFiles WHERE OrderId = @Order AND OrderItemId = @Item
AND FileRole = @Role AND IsActive = 1 ORDER BY OrderFileId LIMIT 1",
                new[] { new ParamValuePair("@Order", orderId), new ParamValuePair("@Item", itemId), new ParamValuePair("@Role", role) });
            return value == null || value == DBNull.Value ? null : (long?)Convert.ToInt64(value);
        }

        private static long? UpsertOrderFile(MySqlConnection conn, MySqlTransaction transaction, long orderId, long orderItemId, string fileRole, string filePath, string createdBy)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Execute(conn, transaction, @"
UPDATE OrderFiles
SET IsActive = 0
WHERE OrderId = @OrderId
AND OrderItemId = @OrderItemId
AND FileRole = @FileRole", new[]
                {
                    new ParamValuePair("@OrderId", orderId),
                    new ParamValuePair("@OrderItemId", orderItemId),
                    new ParamValuePair("@FileRole", fileRole)
                });
                return null;
            }

            FileInfo file = new FileInfo(filePath);
            string originalFileName = file.Name;
            string extension = file.Extension;
            long? fileSize = file.Exists ? (long?)file.Length : null;

            long? existingFileId = GetOrderFileId(conn, transaction, orderId, orderItemId, fileRole);
            if (existingFileId.HasValue)
            {
                string updateSql = @"
UPDATE OrderFiles
SET
    OriginalFileName = @OriginalFileName,
    StoredFileName = @StoredFileName,
    StoredPath = @StoredPath,
    FileExt = @FileExt,
    FileSizeBytes = @FileSizeBytes,
    IsActive = 1,
    CreatedBy = @CreatedBy
WHERE OrderFileId = @OrderFileId";

                Execute(conn, transaction, updateSql, new[]
                {
                    new ParamValuePair("@OrderFileId", existingFileId.Value),
                    new ParamValuePair("@OriginalFileName", originalFileName),
                    new ParamValuePair("@StoredFileName", originalFileName),
                    new ParamValuePair("@StoredPath", filePath.Trim()),
                    new ParamValuePair("@FileExt", EmptyToNull(extension)),
                    new ParamValuePair("@FileSizeBytes", fileSize),
                    new ParamValuePair("@CreatedBy", EmptyToNull(createdBy))
                });

                return existingFileId.Value;
            }

            string insertSql = @"
INSERT INTO OrderFiles
(
    OrderId,
    OrderItemId,
    FileRole,
    OriginalFileName,
    StoredFileName,
    StoredPath,
    FileExt,
    FileSizeBytes,
    IsActive,
    CreatedBy
)
VALUES
(
    @OrderId,
    @OrderItemId,
    @FileRole,
    @OriginalFileName,
    @StoredFileName,
    @StoredPath,
    @FileExt,
    @FileSizeBytes,
    1,
    @CreatedBy
);
SELECT LAST_INSERT_ID();";

            object value = Scalar(conn, transaction, insertSql, new[]
            {
                new ParamValuePair("@OrderId", orderId),
                new ParamValuePair("@OrderItemId", orderItemId),
                new ParamValuePair("@FileRole", fileRole),
                new ParamValuePair("@OriginalFileName", originalFileName),
                new ParamValuePair("@StoredFileName", originalFileName),
                new ParamValuePair("@StoredPath", filePath.Trim()),
                new ParamValuePair("@FileExt", EmptyToNull(extension)),
                new ParamValuePair("@FileSizeBytes", fileSize),
                new ParamValuePair("@CreatedBy", EmptyToNull(createdBy))
            });

            return Convert.ToInt64(value);
        }

        private static void UpsertOrderFileRelation(MySqlConnection conn, MySqlTransaction transaction, long parentFileId, long childFileId)
        {
            object existing = Scalar(conn, transaction, @"
SELECT RelationId
FROM OrderFileRelations
WHERE ParentOrderFileId = @ParentOrderFileId
AND ChildOrderFileId = @ChildOrderFileId
AND RelationType = 'GENERATED_FROM'
LIMIT 1", new[]
            {
                new ParamValuePair("@ParentOrderFileId", parentFileId),
                new ParamValuePair("@ChildOrderFileId", childFileId)
            });

            if (existing != null && existing != DBNull.Value)
            {
                return;
            }

            Execute(conn, transaction, @"
INSERT INTO OrderFileRelations
(
    ParentOrderFileId,
    ChildOrderFileId,
    RelationType
)
VALUES
(
    @ParentOrderFileId,
    @ChildOrderFileId,
    'GENERATED_FROM'
)", new[]
            {
                new ParamValuePair("@ParentOrderFileId", parentFileId),
                new ParamValuePair("@ChildOrderFileId", childFileId)
            });
        }

        private static string CreateOrderNo(MySqlConnection conn, MySqlTransaction transaction, DateTime orderDate)
        {
            string prefix = orderDate.ToString("yyyyMMdd");
            object value = Scalar(conn, transaction, @"
SELECT MAX(CAST(SUBSTRING(OrderNo, 10) AS UNSIGNED))
FROM OrderInfo
WHERE OrderNo LIKE @OrderNoPrefix
AND OrderNo REGEXP @OrderNoPattern", new[]
            {
                new ParamValuePair("@OrderNoPrefix", prefix + "-%"),
                new ParamValuePair("@OrderNoPattern", "^" + prefix + "-[0-9]{3}$")
            });

            int sequence;
            if (value == null || value == DBNull.Value || !int.TryParse(value.ToString(), out sequence))
            {
                sequence = 0;
            }

            return string.Format("{0}-{1:000}", prefix, sequence + 1);
        }

        private static long? GetFirstOrderItemId(MySqlConnection conn, MySqlTransaction transaction, long orderId)
        {
            object value = Scalar(conn, transaction, @"
SELECT OrderItemId
FROM OrderItems
WHERE OrderId = @OrderId
ORDER BY LineNo, OrderItemId
LIMIT 1", new[]
            {
                new ParamValuePair("@OrderId", orderId)
            });

            return value == null || value == DBNull.Value ? null : (long?)Convert.ToInt64(value);
        }

        private static long? GetOrderFileId(MySqlConnection conn, MySqlTransaction transaction, long orderId, long orderItemId, string fileRole)
        {
            object value = Scalar(conn, transaction, @"
SELECT OrderFileId
FROM OrderFiles
WHERE OrderId = @OrderId
AND OrderItemId = @OrderItemId
AND FileRole = @FileRole
ORDER BY OrderFileId
LIMIT 1", new[]
            {
                new ParamValuePair("@OrderId", orderId),
                new ParamValuePair("@OrderItemId", orderItemId),
                new ParamValuePair("@FileRole", fileRole)
            });

            return value == null || value == DBNull.Value ? null : (long?)Convert.ToInt64(value);
        }

        private static List<OrderItemSaveData> GetOrderItems(OrderSaveData order)
        {
            if (order.Items != null && order.Items.Count > 0)
            {
                return order.Items;
            }

            List<OrderItemSaveData> items = new List<OrderItemSaveData>();
            items.Add(new OrderItemSaveData
            {
                OrderItemId = order.OrderItemId,
                LineNo = 1,
                PriceItemSeqNo = order.PriceItemSeqNo,
                PriceItemPriceId = order.PriceItemPriceId,
                ItemCode = order.ItemCode,
                ItemName = order.ItemName,
                CustomName = order.CustomName,
                SpecText = order.SpecText,
                BillingBasis = order.BillingBasis,
                WidthMm = order.WidthMm,
                HeightMm = order.HeightMm,
                AreaPerQty = order.AreaPerQty,
                Qty = order.Qty,
                AreaQty = order.AreaQty,
                UnitPrice = order.UnitPrice,
                SupplyAmount = order.SupplyAmount,
                VatAmount = order.VatAmount,
                TotalAmount = order.TotalAmount,
                ItemDescription = order.ItemDescription,
                ProductionStatus = order.ProductionStatus,
                RequestFilePath = order.RequestFilePath,
                OutputFilePath = order.OutputFilePath,
                ProcessRules = order.ProcessRules
            });

            return items;
        }

        private static void DeleteRemovedOrderItems(MySqlConnection conn, MySqlTransaction transaction, long orderId, List<long> savedOrderItemIds)
        {
            if (savedOrderItemIds == null || savedOrderItemIds.Count == 0)
            {
                return;
            }

            string ids = string.Join(",", savedOrderItemIds);
            Execute(conn, transaction, @"
DELETE relation
FROM OrderFileRelations relation
INNER JOIN OrderFiles file
    ON file.OrderFileId = relation.ParentOrderFileId
    OR file.OrderFileId = relation.ChildOrderFileId
WHERE file.OrderId = @OrderId
AND file.OrderItemId NOT IN (" + ids + ")", new[]
            {
                new ParamValuePair("@OrderId", orderId)
            });

            Execute(conn, transaction, @"
DELETE FROM OrderFiles
WHERE OrderId = @OrderId
AND OrderItemId NOT IN (" + ids + ")", new[]
            {
                new ParamValuePair("@OrderId", orderId)
            });

            Execute(conn, transaction, @"
DELETE FROM OrderItems
WHERE OrderId = @OrderId
AND OrderItemId NOT IN (" + ids + ")", new[]
            {
                new ParamValuePair("@OrderId", orderId)
            });
        }

        private static ParamValuePair[] CreateOrderItemParameters(long orderId, long orderItemId, OrderItemSaveData item)
        {
            return new[]
            {
                new ParamValuePair("@OrderItemId", orderItemId),
                new ParamValuePair("@OrderId", orderId),
                new ParamValuePair("@LineNo", item.LineNo),
                new ParamValuePair("@PriceItemSeqNo", item.PriceItemSeqNo),
                new ParamValuePair("@PriceItemPriceId", item.PriceItemPriceId),
                new ParamValuePair("@ItemCode", EmptyToNull(item.ItemCode)),
                new ParamValuePair("@ItemName", item.ItemName),
                new ParamValuePair("@CustomName", EmptyToNull(item.CustomName)),
                new ParamValuePair("@SpecText", EmptyToNull(item.SpecText)),
                new ParamValuePair("@BillingBasis", EmptyToNull(item.BillingBasis) ?? "AREA"),
                new ParamValuePair("@WidthMm", item.WidthMm),
                new ParamValuePair("@HeightMm", item.HeightMm),
                new ParamValuePair("@AreaPerQty", item.AreaPerQty),
                new ParamValuePair("@Qty", item.Qty),
                new ParamValuePair("@AreaQty", item.AreaQty),
                new ParamValuePair("@UnitPrice", item.UnitPrice),
                new ParamValuePair("@SupplyAmount", item.SupplyAmount),
                new ParamValuePair("@VatAmount", item.VatAmount),
                new ParamValuePair("@TotalAmount", item.TotalAmount),
                new ParamValuePair("@ItemDescription", EmptyToNull(item.ItemDescription)),
                new ParamValuePair("@ProductionStatus", item.ProductionStatus)
            };
        }

        private static int Execute(MySqlConnection conn, MySqlTransaction transaction, string sql, ParamValuePair[] parameters)
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn, transaction))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        private static object Scalar(MySqlConnection conn, MySqlTransaction transaction, string sql, ParamValuePair[] parameters)
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn, transaction))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteScalar();
            }
        }

        private static void AddParameters(MySqlCommand cmd, ParamValuePair[] parameters)
        {
            foreach (ParamValuePair parameter in parameters)
            {
                cmd.Parameters.AddWithValue(parameter.ParameterName, parameter.Value ?? DBNull.Value);
            }
        }

        private static object EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static Dictionary<OrderProductionStatus, int> CreateEmptyStatusCounts()
        {
            Dictionary<OrderProductionStatus, int> counts = new Dictionary<OrderProductionStatus, int>();
            counts[OrderProductionStatus.New] = 0;
            counts[OrderProductionStatus.Design] = 0;
            counts[OrderProductionStatus.Printing] = 0;
            counts[OrderProductionStatus.PostProcess] = 0;
            counts[OrderProductionStatus.PackingWaiting] = 0;
            counts[OrderProductionStatus.PackingComplete] = 0;
            counts[OrderProductionStatus.DeliveryComplete] = 0;
            counts[OrderProductionStatus.SettlementComplete] = 0;

            return counts;
        }
    }
}
