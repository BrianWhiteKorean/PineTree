using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;
using PineTree.Classes;
using PineTree.Dialogs;

internal static class OrderProcessRuleRegression
{
    private static int checks;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static object Field(object value, string name) { return value.GetType().GetField(name, PrivateInstance).GetValue(value); }
    private static object Invoke(Type type, object target, string name, params object[] args)
    {
        try { return type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Sql(MySqlConnection conn, MySqlTransaction tx, string sql)
    {
        using (var cmd = new MySqlCommand(sql, conn, tx)) return cmd.ExecuteScalar();
    }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; }
        throw new Exception(message);
    }
    private static List<OrderProcessRuleData> Rules(ProcessRuleData step, int count)
    {
        var result = new List<OrderProcessRuleData>();
        for (int i = 1; i <= count; i++) result.Add(new OrderProcessRuleData {
            RuleID = step.RuleID, SortOrder = i, ProcessName = step.ProcessName,
            Summary = step.BuildSummary(), Steps = new List<ProcessRuleData> { step.Copy() }
        });
        return result;
    }
    private static void Save(MySqlConnection conn, MySqlTransaction tx, long order, long item, OrderItemSaveData data)
    {
        Invoke(typeof(OrderRepository), null, "SaveFilesAndRules", conn, tx, order, item, data, "regression");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var sample = new ProcessRuleData { RuleID = 1, ProcessName = "Punch", ActionCode = "ADD_PUNCH_MARK",
                ParamsJSON = "{\"punchMode\":\"FOUR_SIDES\",\"punchCount\":4,\"diameterMm\":35,\"unit\":\"mm\"}" };
            var selected = Rules(sample, 7);
            using (var add = new DialogOrderItem())
            using (var edit = new DialogOrderInfoEdit())
            {
                foreach (Form form in new Form[] { add, edit })
                {
                    var editor = Field(form, "_processRuleEditor");
                    Invoke(editor.GetType(), editor, "LoadRules", selected);
                    var loaded = (List<OrderProcessRuleData>)Invoke(editor.GetType(), editor, "GetRules");
                    Check(loaded.Count == 7 && loaded[6].SortOrder == 7, "UI supports more than three rules");
                    loaded[0].Steps[0].ParamsJSON = "{}";
                    Check(selected[0].Steps[0].ParamsJSON == sample.ParamsJSON, "Edit does not mutate original snapshot");
                }
                var item = new DialogPriceItemOrderItem { PriceItemSeqNo = 10, PriceItemName = "Original", UnitName = "EA", Qty = 1,
                    RequestFilePath = @"C:\Regression\request.pdf", OutputFilePath = @"C:\Regression\output.pdf", ProcessRules = selected };
                Invoke(edit.GetType(), edit, "ApplyOrderItemToInputs", item);
                var edited = (DialogPriceItemOrderItem)Invoke(edit.GetType(), edit, "CreateOrderItem");
                Check(edited.ProcessRules.Count == 7 && edited.RequestFilePath == item.RequestFilePath && edited.OutputFilePath == item.OutputFilePath,
                    "Edit round trip keeps all rules and paths");
                var save = (OrderItemSaveData)Invoke(edit.GetType(), edit, "CreateOrderItemSaveData", edited);
                Check(save.ProcessRules.Count == 7 && save.RequestFilePath == item.RequestFilePath, "Edit save mapping");
                // Exercise the parent row mapping without loading a real order.
                var parent = (DialogOrder)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(DialogOrder));
                typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(parent, new object[0]);
                try
                {
                    Invoke(typeof(DialogOrder), parent, "InitializeComponent");
                    Invoke(typeof(DialogOrder), parent, "InitOrderItemTable");
                    Invoke(typeof(DialogOrder), parent, "AddOrderItem", edited, (long?)null);
                    var rows = (DataTable)Field(parent, "m_orderItems");
                    var parentResult = (DialogPriceItemOrderItem)Invoke(typeof(DialogOrder), parent, "CreateDialogOrderItem", rows.Rows[0]);
                    Check(parentResult.ProcessRules.Count == 7 && parentResult.OutputFilePath == item.OutputFilePath, "Parent row round trip");
                    var saveItems = (List<OrderItemSaveData>)Invoke(typeof(DialogOrder), parent, "CreateOrderItemSaveDataList");
                    Check(saveItems[0].ProcessRules.Count == 7 && saveItems[0].RequestFilePath == item.RequestFilePath, "Parent full-order save mapping");
                }
                finally { parent.Dispose(); }
            }
            if (Array.IndexOf(args, "--database") >= 0) Database();
            Console.WriteLine("PASS: " + checks + " order process-rule checks");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
    private static void Database()
    {
        var masters = ProcessRuleRepository.GetRuleList("", "");
        ProcessRuleData step = null;
        foreach (DataRow row in masters.Rows)
        {
            var steps = ProcessRuleRepository.GetRuleSteps(Convert.ToInt64(row["RuleID"]));
            if (steps.Count == 0) continue;
            try { steps[0].BuildSummary(); step = steps[0]; break; } catch (ArgumentException) { }
        }
        Check(step != null, "At least one supported active rule");
        using (var conn = DB.CreateConnection())
        {
            conn.Open();
            long fixtureOrder = 0;
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    var customer = Convert.ToString(Sql(conn, tx, "SELECT CustCode FROM Customers LIMIT 1"));
                    using (var cmd = new MySqlCommand("INSERT INTO OrderInfo (CustCode,OrderDate,OrderTitle) VALUES (@Cust,CURDATE(),'OrderProcessRuleRegression'); SELECT LAST_INSERT_ID()",conn,tx))
                    { cmd.Parameters.AddWithValue("@Cust",customer); fixtureOrder = Convert.ToInt64(cmd.ExecuteScalar()); }
                    long price = Convert.ToInt64(Sql(conn,tx,"SELECT SeqNo FROM PriceItems WHERE IsActive = 1 LIMIT 1"));
                    var data = new OrderItemSaveData { LineNo = 1, PriceItemSeqNo = price, ItemName = "Rule regression", BillingBasis = "QTY", Qty = 1,
                        ProductionStatus = "RECEIVED", ProcessRules = Rules(step,7) };
                    long item = Convert.ToInt64(Invoke(typeof(OrderRepository), null, "UpsertOrderItem", conn,tx,fixtureOrder,data));
                    Save(conn,tx,fixtureOrder,item,data);
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item))==7,"DB N rules without file");
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item+" AND OrderFileId IS NOT NULL"))==0,"No dummy files required");
                    data.RequestFilePath = @"C:\Regression\request.pdf";
                    data.OutputFilePath = @"C:\Regression\output.pdf";
                    Save(conn,tx,fixtureOrder,item,data);
                    long request = Convert.ToInt64(Sql(conn,tx,"SELECT OrderFileId FROM OrderFiles WHERE OrderItemId="+item+" AND FileRole='REQUEST'"));
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item+" AND OrderFileId="+request))==7,"Rules linked to request file");
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderFiles WHERE OrderItemId="+item))==2,"Existing order/file relations preserved");
                    string json = Convert.ToString(Sql(conn,tx,"SELECT StepsJSON FROM OrderProcessRules WHERE OrderItemId="+item+" AND SortOrder=1"));
                    var stored = new JavaScriptSerializer().Deserialize<List<ProcessRuleData>>(json);
                    Check(stored[0].ParamsJSON==step.ParamsJSON,"JSON snapshot is lossless");
                    var reloaded = (List<OrderProcessRuleData>)Invoke(typeof(OrderProcessRuleRepository),null,"LoadRulesCore",conn,tx,fixtureOrder,item);
                    Check(reloaded.Count == 7 && reloaded[6].SortOrder == 7 && reloaded[0].Steps[0].ParamsJSON == step.ParamsJSON,
                        "Saved rule list reloads with ordering and snapshots");
                    using (var editor = new DialogOrderInfoEdit())
                    {
                        Invoke(typeof(DialogOrderInfoEdit),editor,"ApplyOrderItemToInputs",new DialogPriceItemOrderItem {
                            PriceItemSeqNo = price, PriceItemName = "Reloaded", UnitName = "EA", Qty = 1,
                            ProcessRules = reloaded, RequestFilePath = data.RequestFilePath, OutputFilePath = data.OutputFilePath });
                        var roundTrip = (DialogPriceItemOrderItem)Invoke(typeof(DialogOrderInfoEdit),editor,"CreateOrderItem");
                        Check(roundTrip.ProcessRules.Count == 7 && roundTrip.ProcessRules[6].Summary == reloaded[6].Summary,
                            "DB snapshots reload into N-row edit UI");
                    }
                    var untouched = new OrderItemSaveData();
                    Save(conn,tx,fixtureOrder,item,untouched);
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item))==7,"Null list preserves rules");
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderFiles WHERE OrderItemId="+item+" AND IsActive=1"))==2,"Null paths preserve files");
                    data.ProcessRules.RemoveAt(0);
                    data.ProcessRules[0].SortOrder = 1;
                    Save(conn,tx,fixtureOrder,item,data);
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item))==6,"Remove and reorder persisted");
                    data.RequestFilePath = "";
                    Save(conn,tx,fixtureOrder,item,data);
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item+" AND OrderFileId IS NULL"))==6,"Removing file keeps item rules");
                    data.ProcessRules[1].SortOrder = 1;
                    Reject(()=>Save(conn,tx,fixtureOrder,item,data),"Duplicate sequence allowed");
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item))==6,"Bad list does not delete existing rules");
                    data.ProcessRules = Rules(step,1);
                    Reject(()=>Save(conn,tx,fixtureOrder+100000,item,data),"Cross-order ownership allowed");
                    Reject(()=>Invoke(typeof(OrderProcessRuleRepository),null,"Save",conn,tx,fixtureOrder,item,(long?)request,data.ProcessRules),"Inactive file linked");
                    data.ProcessRules = new List<OrderProcessRuleData>();
                    Save(conn,tx,fixtureOrder,item,data);
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item))==0,"Empty list removes all rules");
                    data.ProcessRules = Rules(step,7);
                    Save(conn,tx,fixtureOrder,item,data);
                    var retained = new OrderItemSaveData { LineNo = 2, PriceItemSeqNo = price, ItemName = "Retained fixture",
                        BillingBasis = "QTY", Qty = 1, ProductionStatus = "RECEIVED" };
                    long retainedId = Convert.ToInt64(Invoke(typeof(OrderRepository), null, "UpsertOrderItem",conn,tx,fixtureOrder,retained));
                    Invoke(typeof(OrderRepository),null,"DeleteRemovedOrderItems",conn,tx,fixtureOrder,new List<long> { retainedId });
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderItems WHERE OrderItemId="+item))==0,"Existing item deletion flow works");
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderItemId="+item))==0,"Item deletion cascades rule links");
                    Check(Convert.ToInt32(Sql(conn,tx,"SELECT COUNT(*) FROM OrderFiles WHERE OrderItemId="+item))==0,"Item deletion removes files without FK conflict");
                    // All fixture writes and any sequence allocation are rolled back together.
                }
                finally { tx.Rollback(); }
            }
            Check(Convert.ToInt32(Sql(conn,null,"SELECT COUNT(*) FROM OrderInfo WHERE OrderId="+fixtureOrder))==0,"Fixture order rolled back");
            Check(Convert.ToInt32(Sql(conn,null,"SELECT COUNT(*) FROM OrderProcessRules WHERE OrderId="+fixtureOrder))==0,"Fixture rules rolled back");
            Check(Convert.ToInt32(Sql(conn,null,"SELECT COUNT(*) FROM OrderFiles WHERE OrderId="+fixtureOrder))==0,"Fixture files rolled back");
        }
    }
}
