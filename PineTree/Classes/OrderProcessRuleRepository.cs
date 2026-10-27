using System;
using System.Collections.Generic;
using System.Data;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    public sealed class OrderProcessRuleData
    {
        public long RuleID { get; set; }
        public int SortOrder { get; set; }
        public string ProcessName { get; set; }
        public string Summary { get; set; }
        public List<ProcessRuleData> Steps { get; set; }
    }

    public static class OrderProcessRuleRepository
    {
        public static List<OrderProcessRuleData> GetRules(long orderId, long orderItemId)
        {
            using (var connection = DB.CreateConnection())
            {
                connection.Open();
                return LoadRulesCore(connection, null, orderId, orderItemId);
            }
        }

        internal static List<OrderProcessRuleData> LoadRulesCore(MySqlConnection connection, MySqlTransaction transaction, long orderId, long orderItemId)
        {
            var table = new DataTable();
            using (var command = new MySqlCommand(@"
SELECT RuleID, SortOrder, ProcessName, Summary, StepsJSON
FROM OrderProcessRules WHERE OrderId = @Order AND OrderItemId = @Item
ORDER BY SortOrder, OrderProcessRuleId", connection, transaction))
            {
                command.Parameters.AddWithValue("@Order", orderId);
                command.Parameters.AddWithValue("@Item", orderItemId);
                using (var adapter = new MySqlDataAdapter(command)) adapter.Fill(table);
            }
            var result = new List<OrderProcessRuleData>();
            foreach (DataRow row in table.Rows)
                result.Add(new OrderProcessRuleData {
                    RuleID = Convert.ToInt64(row["RuleID"]), SortOrder = Convert.ToInt32(row["SortOrder"]),
                    ProcessName = Convert.ToString(row["ProcessName"]), Summary = Convert.ToString(row["Summary"]),
                    Steps = new JavaScriptSerializer().Deserialize<List<ProcessRuleData>>(Convert.ToString(row["StepsJSON"]))
                });
            return result;
        }

        internal static void Save(MySqlConnection connection, MySqlTransaction transaction,
            long orderId, long itemId, long? fileId, List<OrderProcessRuleData> rules)
        {
            // null means this caller did not edit the rule list; empty means remove all.
            if (rules == null) return;
            using (var check = new MySqlCommand("SELECT OrderId FROM OrderItems WHERE OrderItemId = @Item FOR UPDATE", connection, transaction))
            {
                check.Parameters.AddWithValue("@Item", itemId);
                if (Convert.ToString(check.ExecuteScalar()) != orderId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("주문과 주문품목의 연결을 확인할 수 없습니다.");
            }
            if (fileId.HasValue)
            {
                using (var check = new MySqlCommand("SELECT COUNT(*) FROM OrderFiles WHERE OrderFileId = @File AND OrderId = @Order AND OrderItemId = @Item AND IsActive = 1", connection, transaction))
                {
                    check.Parameters.AddWithValue("@File", fileId.Value);
                    check.Parameters.AddWithValue("@Order", orderId);
                    check.Parameters.AddWithValue("@Item", itemId);
                    if (Convert.ToInt32(check.ExecuteScalar()) != 1) throw new InvalidOperationException("다른 주문 또는 품목의 파일에 후가공을 연결할 수 없습니다.");
                }
            }
            var slots = new HashSet<int>();
            foreach (var rule in rules)
            {
                if (rule == null || rule.RuleID <= 0 || rule.SortOrder <= 0 || !slots.Add(rule.SortOrder)
                    || string.IsNullOrWhiteSpace(rule.ProcessName) || rule.ProcessName.Length > 100
                    || string.IsNullOrWhiteSpace(rule.Summary) || rule.Steps == null || rule.Steps.Count == 0)
                    throw new ArgumentException("후가공 규칙의 순서와 선택 내용을 확인해주세요.");
                foreach (var step in rule.Steps)
                {
                    if (step == null || step.RuleID != rule.RuleID) throw new ArgumentException("후가공 상세 명령의 규칙이 일치하지 않습니다.");
                    step.BuildSummary(); // Reject unreadable commands/JSON without changing the master.
                }
            }
            using (var delete = new MySqlCommand("DELETE FROM OrderProcessRules WHERE OrderId = @Order AND OrderItemId = @Item", connection, transaction))
            {
                delete.Parameters.AddWithValue("@Order", orderId);
                delete.Parameters.AddWithValue("@Item", itemId);
                delete.ExecuteNonQuery();
            }
            foreach (var rule in rules)
            {
                using (var insert = new MySqlCommand(@"
INSERT INTO OrderProcessRules (OrderId, OrderItemId, OrderFileId, RuleID, SortOrder, ProcessName, Summary, StepsJSON)
VALUES (@Order, @Item, @File, @Rule, @Sort, @Name, @Summary, @Steps)", connection, transaction))
                {
                    insert.Parameters.AddWithValue("@Order", orderId);
                    insert.Parameters.AddWithValue("@Item", itemId);
                    insert.Parameters.AddWithValue("@File", (object)fileId ?? DBNull.Value);
                    insert.Parameters.AddWithValue("@Rule", rule.RuleID);
                    insert.Parameters.AddWithValue("@Sort", rule.SortOrder);
                    insert.Parameters.AddWithValue("@Name", rule.ProcessName);
                    insert.Parameters.AddWithValue("@Summary", rule.Summary);
                    insert.Parameters.AddWithValue("@Steps", new JavaScriptSerializer().Serialize(rule.Steps));
                    insert.ExecuteNonQuery();
                }
            }
        }
    }
}
