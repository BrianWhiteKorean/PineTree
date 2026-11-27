using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    public static partial class OrderRepository
    {
        private const string OrderWriteLock = "PineTree.OrderWrite";
        // FileId fields in the saved editor, design-pad and preview JSON use this storage format.
        private static readonly Regex ServerFileId = new Regex(
            @"""FileId""\s*:\s*""([a-f0-9]{64}\.(?:pdf|eps|ai|png|jpg|jpeg))""", RegexOptions.CultureInvariant);

        internal static void DeleteOrders(IEnumerable<long> orderIds)
        {
            var ids = new HashSet<long>(orderIds);
            if (ids.Count == 0 || ids.Any(id => id <= 0))
                throw new ArgumentException("삭제할 주문번호가 올바르지 않습니다.", nameof(orderIds));
            string idList = string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));

            using (var conn = DB.CreateConnection())
            {
                conn.Open();
                using (AcquireOrderWriteLock(conn))
                using (var transaction = conn.BeginTransaction())
                {
                    foreach (string fileId in ReadServerFileIds(conn, transaction, idList))
                        Execute(conn, transaction, @"INSERT INTO OrderFileDeletionQueue (FileId) VALUES (@FileId)
ON DUPLICATE KEY UPDATE FileId = VALUES(FileId)", new[] { new ParamValuePair("@FileId", fileId) });

                    PreserveLinkedOrders(conn, transaction, ids, idList);
                    Execute(conn, transaction, @"DELETE relation FROM OrderFileRelations relation
INNER JOIN OrderFiles file ON file.OrderFileId = relation.ParentOrderFileId
OR file.OrderFileId = relation.ChildOrderFileId WHERE file.OrderId IN (" + idList + ")", null);
                    Execute(conn, transaction, "DELETE FROM OrderProcessRules WHERE OrderId IN (" + idList + ")", null);
                    Execute(conn, transaction, "DELETE FROM OrderFiles WHERE OrderId IN (" + idList + ")", null);
                    // Some older databases have the restricted self-reference on order items.
                    Execute(conn, transaction, "UPDATE OrderItems SET ParentOrderItemId = NULL WHERE OrderId IN (" + idList
                        + ") AND ParentOrderItemId IS NOT NULL", null);
                    Execute(conn, transaction, "DELETE FROM OrderItems WHERE OrderId IN (" + idList + ")", null);
                    Execute(conn, transaction, "DELETE FROM OrderInfo WHERE OrderId IN (" + idList + ")", null);
                    transaction.Commit();
                }
            }
        }

        private static void PreserveLinkedOrders(MySqlConnection conn, MySqlTransaction transaction, HashSet<long> deleting, string idList)
        {
            using (var command = new MySqlCommand(@"SELECT OrderId, OrderGroupId FROM OrderInfo
WHERE OrderGroupId IN (" + idList + ") ORDER BY OrderGroupSequence, OrderId FOR UPDATE", conn, transaction))
            using (var children = new DataTable())
            {
                using (var reader = command.ExecuteReader())
                    children.Load(reader);

                foreach (var group in children.AsEnumerable().GroupBy(row => Convert.ToInt64(row["OrderGroupId"])))
                {
                    long? newRoot = group.Select(row => Convert.ToInt64(row["OrderId"]))
                        .Where(id => !deleting.Contains(id)).Select(id => (long?)id).FirstOrDefault();
                    // The old root cannot be removed while any child still references it.
                    Execute(conn, transaction, @"UPDATE OrderInfo SET OrderGroupId = @NewRoot
WHERE OrderGroupId = @Root AND OrderId NOT IN (" + idList + ") AND OrderId <> @NewRoot",
                        new[] { new ParamValuePair("@Root", group.Key), new ParamValuePair("@NewRoot", newRoot) });
                    Execute(conn, transaction, @"UPDATE OrderInfo SET OrderGroupId = NULL
WHERE OrderGroupId = @Root AND (OrderId IN (" + idList + ") OR OrderId = @NewRoot)",
                        new[] { new ParamValuePair("@Root", group.Key), new ParamValuePair("@NewRoot", newRoot) });
                }
            }
        }

        internal static void CleanUpDeletedOrderFiles()
        {
            try
            {
                using (var conn = DB.CreateConnection())
                {
                    conn.Open();
                    using (AcquireOrderWriteLock(conn))
                    {
                        var pending = new List<string>();
                        using (var command = new MySqlCommand("SELECT FileId FROM OrderFileDeletionQueue", conn))
                        using (var reader = command.ExecuteReader())
                            while (reader.Read()) pending.Add(reader.GetString(0));
                        if (pending.Count == 0) return;

                        var referenced = ReadServerFileIds(conn, null, null);
                        OrderFileStorageService.DeleteFiles(pending.Where(id => !referenced.Contains(id)));
                        Execute(conn, null, "DELETE FROM OrderFileDeletionQueue", null);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("대기 중인 서버 파일을 삭제하지 못했습니다. 주문관리 화면을 다시 열면 재시도합니다.\r\n"
                    + OrderFileStorageService.GetTransferFailureMessage(ex), ex);
            }
        }

        private static HashSet<string> ReadServerFileIds(MySqlConnection conn, MySqlTransaction transaction, string idList)
        {
            string filter = idList == null ? "" : " WHERE OrderId IN (" + idList + ")";
            var files = new HashSet<string>(StringComparer.Ordinal);
            using (var command = new MySqlCommand(@"
SELECT StateJSON FROM OrderDesignPadStates" + filter + @"
UNION ALL SELECT PreviewJSON FROM OrderPreviewSnapshots" + filter + @"
UNION ALL SELECT editor.StateJSON FROM OrderItemEditorStates editor
INNER JOIN OrderItems item ON item.OrderItemId = editor.OrderItemId" + filter, conn, transaction))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.IsDBNull(0)) continue;
                    foreach (Match match in ServerFileId.Matches(reader.GetString(0)))
                        files.Add(match.Groups[1].Value);
                }
            }
            return files;
        }

        private static IDisposable AcquireOrderWriteLock(MySqlConnection conn)
        {
            object acquired;
            using (var command = new MySqlCommand("SELECT GET_LOCK(@Name, 25)", conn))
            {
                command.Parameters.AddWithValue("@Name", OrderWriteLock);
                acquired = command.ExecuteScalar();
            }
            if (acquired == null || acquired == DBNull.Value || Convert.ToInt32(acquired) != 1)
                throw new InvalidOperationException("다른 주문을 저장하거나 삭제하는 중입니다. 잠시 후 다시 시도해주세요.");
            return new OrderWriteLease(conn);
        }

        private sealed class OrderWriteLease : IDisposable
        {
            private readonly MySqlConnection _connection;

            internal OrderWriteLease(MySqlConnection connection)
            {
                _connection = connection;
            }

            public void Dispose()
            {
                try
                {
                    Scalar(_connection, null, "SELECT RELEASE_LOCK(@Name)",
                        new[] { new ParamValuePair("@Name", OrderWriteLock) });
                }
                catch (MySqlException)
                {
                    MySqlConnection.ClearPool(_connection);
                }
            }
        }
    }
}
