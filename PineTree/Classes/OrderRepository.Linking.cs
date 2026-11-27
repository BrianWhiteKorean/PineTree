using System;
using System.Data;
using System.Globalization;
using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    public static partial class OrderRepository
    {
        private sealed class LinkedOrderNumber
        {
            public long RootOrderId;
            public int Sequence;
            public string BaseNo;
            public string OrderNo { get { return BaseNo + "_" + Sequence.ToString(CultureInfo.InvariantCulture); } }
        }

        public static DataTable GetOrderLinkCandidates(DateTime date, string customerName)
        {
            return DB.MySqlQueryRead(@"SELECT oi.OrderId, oi.OrderNo, oi.OrderDate, oi.RequestedDueAt,
oi.OrderTitle, oi.CustCode, c.CustName FROM OrderInfo oi
LEFT JOIN Customers c ON c.CustCode = oi.CustCode
WHERE oi.OrderDate = @Date AND (@Name = '' OR c.CustName LIKE @Name)
ORDER BY oi.ReceivedAt DESC, oi.OrderId DESC", new[] {
                new ParamValuePair("@Date", date.Date),
                new ParamValuePair("@Name", string.IsNullOrWhiteSpace(customerName) ? "" : "%" + customerName.Trim() + "%") });
        }

        public static DataRow GetOrderLinkSource(long selectedOrderId)
        {
            using (var conn = DB.CreateConnection())
            {
                conn.Open();
                long rootId = GetLinkRootId(conn, null, selectedOrderId);
                DataRow root = ReadLinkHeader(conn, null, rootId, false);
                if (root == null) throw new InvalidOperationException("연결할 원주문이 존재하지 않습니다.");
                string baseNo = GetLinkBaseNo(root);
                object max = Scalar(conn, null, "SELECT MAX(OrderGroupSequence) FROM OrderInfo WHERE OrderGroupId = @Root",
                    new[] { new ParamValuePair("@Root", rootId) });
                root.Table.Columns.Add("NextOrderNo");
                root["NextOrderNo"] = baseNo + "_" + GetNextLinkedSequence(root, max);
                return root;
            }
        }

        private static long GetLinkRootId(MySqlConnection conn, MySqlTransaction transaction, long selectedId)
        {
            object value = Scalar(conn, transaction, "SELECT COALESCE(OrderGroupId, OrderId) FROM OrderInfo WHERE OrderId = @Id",
                new[] { new ParamValuePair("@Id", selectedId) });
            if (value == null || value == DBNull.Value) throw new InvalidOperationException("연결할 주문이 존재하지 않습니다.");
            return Convert.ToInt64(value);
        }

        private static DataRow ReadLinkHeader(MySqlConnection conn, MySqlTransaction transaction, long id, bool forUpdate)
        {
            using (var command = new MySqlCommand(@"SELECT oi.OrderId, oi.OrderNo, oi.OrderBaseNo,
oi.OrderGroupId, oi.OrderGroupSequence, oi.OrderDate, oi.RequestedDueAt, oi.CustCode, c.CustName,
oi.OrderChannel, oi.DeliveryType, oi.DeliveryFee FROM OrderInfo oi
LEFT JOIN Customers c ON c.CustCode = oi.CustCode WHERE oi.OrderId = @Id" + (forUpdate ? " FOR UPDATE" : ""), conn, transaction))
            {
                command.Parameters.AddWithValue("@Id", id);
                using (var reader = command.ExecuteReader())
                {
                    var table = new DataTable(); table.Load(reader);
                    return table.Rows.Count == 0 ? null : table.Rows[0];
                }
            }
        }

        private static string GetLinkBaseNo(DataRow root)
        {
            string value = Convert.ToString(root["OrderBaseNo"]);
            if (string.IsNullOrWhiteSpace(value)) value = Convert.ToString(root["OrderNo"]);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("원주문의 주문번호를 확인해주세요.");
            return value;
        }

        private static void CopyLinkHeader(OrderSaveData order, DataRow root)
        {
            order.CustCode = Convert.ToString(root["CustCode"]);
            order.OrderDate = Convert.ToDateTime(root["OrderDate"]);
            order.RequestedDueAt = root["RequestedDueAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(root["RequestedDueAt"]);
            order.OrderChannel = Convert.ToString(root["OrderChannel"]);
            order.DeliveryType = Convert.ToString(root["DeliveryType"]);
            decimal fee;
            order.DeliveryFee = decimal.TryParse(Convert.ToString(root["DeliveryFee"]), NumberStyles.Number, CultureInfo.InvariantCulture, out fee) ? fee : 0m;
        }

        private static LinkedOrderNumber PrepareLinkedOrder(MySqlConnection conn, MySqlTransaction transaction, OrderSaveData order)
        {
            long rootId = GetLinkRootId(conn, transaction, order.LinkToOrderId.Value);
            DataRow root = ReadLinkHeader(conn, transaction, rootId, true);
            if (root == null) throw new InvalidOperationException("연결할 원주문이 존재하지 않습니다.");
            var shared = new OrderSaveData(); CopyLinkHeader(shared, root);
            if (order.CustCode != shared.CustCode || order.OrderDate.Date != shared.OrderDate.Date
                || order.RequestedDueAt != shared.RequestedDueAt || (order.OrderChannel ?? "") != shared.OrderChannel
                || (order.DeliveryType ?? "") != shared.DeliveryType || order.DeliveryFee != shared.DeliveryFee)
                throw new InvalidOperationException("원주문 정보가 변경되었습니다. 기존주문을 다시 선택해주세요.");
            CopyLinkHeader(order, root);
            string baseNo = GetLinkBaseNo(root);
            // The root row serializes all allocations. This locking read avoids stale snapshots.
            object last = Scalar(conn, transaction, @"SELECT OrderGroupSequence FROM OrderInfo
WHERE OrderGroupId = @Root ORDER BY OrderGroupSequence DESC LIMIT 1 FOR UPDATE",
                new[] { new ParamValuePair("@Root", rootId) });
            int next = GetNextLinkedSequence(root, last);
            if (root["OrderGroupSequence"] == DBNull.Value)
                Execute(conn, transaction, @"UPDATE OrderInfo SET OrderNo = @No, OrderBaseNo = @Base,
OrderGroupSequence = 1 WHERE OrderId = @Root", new[] {
                    new ParamValuePair("@No", baseNo + "_1"), new ParamValuePair("@Base", baseNo), new ParamValuePair("@Root", rootId) });
            return new LinkedOrderNumber { RootOrderId = rootId, Sequence = next, BaseNo = baseNo };
        }

        private static int GetNextLinkedSequence(DataRow root, object last)
        {
            int rootSequence = root.IsNull("OrderGroupSequence") ? 1 : Convert.ToInt32(root["OrderGroupSequence"]);
            int lastChildSequence = last == null || last == DBNull.Value ? 1 : Convert.ToInt32(last);
            return checked(Math.Max(rootSequence, lastChildSequence) + 1);
        }

        private static void PreserveLinkedOrderHeader(MySqlConnection conn, MySqlTransaction transaction, OrderSaveData order)
        {
            long rootId = GetLinkRootId(conn, transaction, order.OrderId.Value);
            DataRow root = ReadLinkHeader(conn, transaction, rootId, true);
            if (root != null && root["OrderGroupSequence"] != DBNull.Value) CopyLinkHeader(order, root);
        }
    }
}
