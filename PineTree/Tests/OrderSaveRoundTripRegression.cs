using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// Writes only new test orders inside a transaction that is always rolled back.
internal static class OrderSaveRoundTripRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static Assembly app;
    private static Type repository;
    private static object Finish(object editor,string code) { return editor.GetType().Assembly.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null,new object[]{code}); }
    private static int checks;
    private static long createdOrderId;
    private static object Field(object target, string name) { return target.GetType().GetField(name, PrivateInstance).GetValue(target); }
    private static void SetField(object target, string name, object value) { target.GetType().GetField(name, PrivateInstance).SetValue(target, value); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args); }
    private static object Get(object target, string name) { return target.GetType().GetProperty(name).GetValue(target, null); }
    private static void Set(object target, string name, object value) { target.GetType().GetProperty(name).SetValue(target, value, null); }
    private static void Number(object control, string name, decimal value) { Set(Field(control, name), "Value", value); }
    private static decimal NumberValue(object control, string name) { return Convert.ToDecimal(Get(Field(control, name), "Value")); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static DataTable Query(DbConnection connection, DbTransaction transaction, string sql, long orderId)
    {
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction; command.CommandText = sql;
            var p = command.CreateParameter(); p.ParameterName = "@OrderId"; p.Value = orderId; command.Parameters.Add(p);
            var adapterType = connection.GetType().Assembly.GetType("MySql.Data.MySqlClient.MySqlDataAdapter", true);
            using (var adapter = (DbDataAdapter)Activator.CreateInstance(adapterType, new object[] { command }))
            {
                var table = new DataTable(); adapter.Fill(table); return table;
            }
        }
    }
    private static DataTable ReadOrder(DbConnection c, DbTransaction t, string queryName, long id)
    {
        return Query(c, t, (string)repository.GetField(queryName, PrivateStatic).GetRawConstantValue(), id);
    }
    private static long Save(DbConnection c, DbTransaction t, object model)
    {
        long id = Convert.ToInt64(repository.GetMethod("SaveOrderCore", PrivateStatic).Invoke(null, new object[] { c, t, model }));
        if (createdOrderId == 0) createdOrderId = id;
        return id;
    }
    private static Form EmptyDialog()
    {
        var type = app.GetType("PineTree.Dialogs.DialogOrder", true);
        var form = (Form)FormatterServices.GetUninitializedObject(type);
        typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(form, new object[0]);
        Call(form, "InitializeComponent"); Call(form, "InitOrderItemTable"); Call(form, "InitOrderEditor"); Call(form, "InitPreviewEvent");
        SetField(form, "m_productionStatus", "신규");
        var mode = type.GetField("m_DialogMode", PrivateInstance); mode.SetValue(form, Enum.Parse(mode.FieldType, "New"));
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-4000, -4000); form.ShowInTaskbar = false;
        return form;
    }
    private static Form Restore(DataRow header, DataTable rows, long orderId)
    {
        var form = EmptyDialog();
        SetField(form, "m_orderId", orderId);
        var mode = form.GetType().GetField("m_DialogMode", PrivateInstance); mode.SetValue(form, Enum.Parse(mode.FieldType, "Edit"));
        Call(form, "ApplyLoadedOrder", header); Call(form, "ApplyLoadedOrderItems", rows); SetField(form, "m_isInitializing", false);
        return form;
    }
    private static object Editor(Form form) { return Field(form, "m_orderEditor"); }
    private static string CaptureJson(object editor)
    {
        var state = editor.GetType().GetMethod("CaptureEditorState").Invoke(editor, null);
        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        return serializer.Serialize(SortJson(serializer.DeserializeObject((string)state.GetType().GetMethod("ToJson").Invoke(state, null))));
    }
    private static object SortJson(object value)
    {
        var map = value as System.Collections.Generic.Dictionary<string,object>;
        if(map != null) { var sorted = new System.Collections.Generic.SortedDictionary<string,object>(); foreach(var pair in map) sorted.Add(pair.Key, SortJson(pair.Value)); return sorted; }
        var array = value as object[]; return array == null ? value : Array.ConvertAll(array, SortJson);
    }
    private static void Run(DbConnection connection, DbTransaction transaction, string output)
    {
        var engines = Query(connection, transaction, "SELECT TABLE_NAME,ENGINE FROM information_schema.tables WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('OrderInfo','OrderItems','OrderFiles','OrderItemEditorStates')", 0);
        Check(engines.Rows.Count == 4, "all persistence tables exist");
        foreach (DataRow engine in engines.Rows) Check(engine["ENGINE"].ToString() == "InnoDB", "rollback-safe storage: " + engine["TABLE_NAME"]);
        var customers = Query(connection, transaction, "SELECT CustCode,CustName FROM Customers WHERE CustCode='141' LIMIT 1", 0);
        var prices = Query(connection, transaction, "SELECT SeqNo,ItemCode,PriceItemName,SpecText,UnitName,BillingBasis,PriceCategory,CategoryName FROM PriceItems WHERE ItemCode='2026_0001' LIMIT 1", 0);
        Check(customers.Rows.Count == 1 && prices.Rows.Count == 1, "requested customer and item fixtures exist");
        var price = prices.Rows[0];
        using (var form = EmptyDialog())
        {
            Call(form, "ApplySelectedCustomer", customers.Rows[0]);
            Set(Field(form, "dtmOrder"), "Value", new DateTime(2026,10,25));
            Set(Field(form, "dtmDelivery"), "Value", new DateTime(2026,10,25));
            Call(form, "SelectComboText", Field(form, "cbChannel"), "소나무그래픽");
            Call(form, "SelectComboText", Field(form, "cbDeliveryType"), "택배");
            var item = Activator.CreateInstance(app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem", true));
            Set(item, "PriceItemSeqNo", Convert.ToInt64(price["SeqNo"])); Set(item, "ItemCode", price["ItemCode"].ToString());
            Set(item, "PriceItemName", price["PriceItemName"].ToString()); Set(item, "CustomName", price["PriceItemName"].ToString());
            Set(item, "SpecText", price["SpecText"].ToString()); Set(item, "UnitName", price["UnitName"].ToString());
            Set(item, "BillingBasis", price["BillingBasis"].ToString()); Set(item, "UnitPrice", 4000m);
            Call(form, "AddOrderItem", item, null);
            var table = (DataTable)Field(form, "m_orderItems"); table.Rows[0]["PriceCategory"] = price["PriceCategory"]; table.Rows[0]["CategoryName"] = price["CategoryName"];
            var grid = Field(form, "FGrid1"); Set(grid, "Row", (int)Get(Get(grid,"Rows"),"Fixed")); Call(form, "UpdateSelectedOrderEditor");
            Check(Editor(form).GetType().Name == "UcOrder현수막", "banner editor selected");
            Check((bool)Call(form, "CheckValidation"), "file-less zero quantity order passes validation");
            string initial = CaptureJson(Editor(form));
            var model = Call(form, "CreateOrderSaveData");
            long id = Save(connection, transaction, model);
            Console.WriteLine("Saved file-less zero quantity order in rollback-only transaction");
            var header = ReadOrder(connection, transaction, "OrderForEditSql", id);
            var rows = ReadOrder(connection, transaction, "OrderItemsForEditSql", id);
            Check(rows.Rows.Count == 1 && rows.Rows[0]["RequestFilePath"] == DBNull.Value, "file path is optional");
            Check(rows.Rows[0]["Qty"] != DBNull.Value && Convert.ToDecimal(rows.Rows[0]["Qty"]) == 0, "zero quantity stored as zero");
            Check(Convert.ToDecimal(rows.Rows[0]["UnitPrice"]) == 4000 && Convert.ToDecimal(rows.Rows[0]["TotalAmount"]) == 0, "price and zero amount preserved");
            Check(Query(connection,transaction,"SELECT COUNT(*) AS N FROM OrderFiles WHERE OrderId=@OrderId",id).Rows[0][0].ToString()=="0", "no placeholder file rows created");
            using (var restored = Restore(header.Rows[0], rows, id))
            {
                Check(CaptureJson(Editor(restored)) == initial, "all screenshot default editor settings round-trip");
                Check(Convert.ToDateTime(Get(Field(restored,"dtmOrder"),"Value")).Date == new DateTime(2026,10,25), "order date restored");
                Check(Get(Field(restored,"tbCustCode"),"Text").ToString()=="141" && Get(Field(restored,"cbChannel"),"Text").ToString()=="소나무그래픽" && Get(Field(restored,"cbDeliveryType"),"Text").ToString()=="택배", "customer and combo fields restored");
                var editor = Editor(restored);
                Number(editor,"tbTargetWidth",3300); Number(editor,"tbTargetHeight",1000); Number(editor,"inputNumericBox4",2);
                Number(editor,"tbEdgeTop",3);Number(editor,"tbEdgeBottom",4);Number(editor,"tbEdgeLeft",5);Number(editor,"tbEdgeRight",6);
                Set(Field(editor,"cbDeploy"),"SelectedOption",Field(editor,"inputOption5"));
                var cut=Finish(editor,"CUT");Set(Get(cut,"Parameters"),"LineWidthMm",.25m);Set(Get(cut,"Parameters"),"SpotColorName","custom-cut");Call(editor,"CommitPostProcess",-1,cut);
                var hole=Finish(editor,"HOLE");var hp=Get(hole,"Parameters");Set(hp,"TypeCode","CUSTOM_SPACING");Set(hp,"MarkLeftRightMm",25m);Set(hp,"MarkTopBottomMm",30m);Set(hp,"HoleSpacingMm",600m);Call(editor,"CommitPostProcess",-1,hole);
                var restoredRows = (DataTable)Field(restored,"m_orderItems"); restoredRows.Rows[0]["OrderName"]="저장복원 검증";
                Set(Field(restored,"tbDeliveryFee"),"Value",3500m);Set(Field(restored,"tbRemark"),"Text","rollback-only regression");
                Set(Field(restored,"dtmDelivery"),"Value",new DateTime(2026,10,28));
                Check((bool)Call(restored,"CheckValidation"), "custom file-less settings pass validation");
                string changed = CaptureJson(editor);
                var secondItem = Activator.CreateInstance(app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem",true));
                Set(secondItem,"PriceItemSeqNo",Convert.ToInt64(price["SeqNo"]));Set(secondItem,"ItemCode",price["ItemCode"].ToString());
                Set(secondItem,"PriceItemName",price["PriceItemName"].ToString());Set(secondItem,"UnitName",price["UnitName"].ToString());
                Set(secondItem,"BillingBasis",price["BillingBasis"].ToString());Set(secondItem,"UnitPrice",5000m);
                Call(restored,"AddOrderItem",secondItem,null);
                restoredRows.Rows[1]["CategoryName"]=price["CategoryName"];restoredRows.Rows[1]["PriceCategory"]=price["PriceCategory"];
                var restoredGrid=Field(restored,"FGrid1");Set(restoredGrid,"Row",(int)Get(Get(restoredGrid,"Rows"),"Fixed")+1);Call(restored,"UpdateSelectedOrderEditor");
                Number(Editor(restored),"tbEdgeTop",7);
                string secondState=CaptureJson(Editor(restored));
                var update = Call(restored,"CreateOrderSaveData");
                Check(Save(connection,transaction,update)==id,"edit updates existing order");
                header = ReadOrder(connection,transaction,"OrderForEditSql",id);rows=ReadOrder(connection,transaction,"OrderItemsForEditSql",id);
                Check(rows.Rows.Count==2 && rows.Rows[0]["CustomName"].ToString()=="저장복원 검증","order name stored in CustomName");
                Check(Convert.ToDecimal(rows.Rows[0]["Qty"])==2 && Convert.ToDecimal(rows.Rows[0]["TotalAmount"])==29040,"quantity, area billing and VAT stored");
                using(var again=Restore(header.Rows[0],rows,id))
                {
                    Check(CaptureJson(Editor(again))==changed,"all edited settings restored exactly without source file");
                    Check(((DataTable)Field(again,"m_orderItems")).Rows[0]["OrderName"].ToString()=="저장복원 검증","order name displayed after reload");
                    Check(Convert.ToDecimal(Get(Field(again,"tbDeliveryFee"),"Value"))==3500 && Get(Field(again,"tbRemark"),"Text").ToString()=="rollback-only regression","delivery fee and remark restored");
                    Check(Convert.ToDateTime(Get(Field(again,"dtmDelivery"),"Value")).Date==new DateTime(2026,10,28),"due date restored");
                    var againGrid=Field(again,"FGrid1");Set(againGrid,"Row",(int)Get(Get(againGrid,"Rows"),"Fixed")+1);Call(again,"UpdateSelectedOrderEditor");
                    Check(CaptureJson(Editor(again))==secondState,"second item finishing settings restored independently");
                    Set(againGrid,"Row",(int)Get(Get(againGrid,"Rows"),"Fixed"));Call(again,"UpdateSelectedOrderEditor");
                    Check(CaptureJson(Editor(again))==changed,"selecting first item preserves its own settings");
                    again.Show();Application.DoEvents();
                    using(var bitmap=new Bitmap(again.Width,again.Height)){again.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"order-restored.png"));}
                }
                // A previously saved order without a state row must still restore its numeric item values.
                using(var command=connection.CreateCommand()) {command.Transaction=transaction;command.CommandText="DELETE state FROM OrderItemEditorStates state JOIN OrderItems item ON item.OrderItemId=state.OrderItemId WHERE item.OrderId=@OrderId";var p=command.CreateParameter();p.ParameterName="@OrderId";p.Value=id;command.Parameters.Add(p);command.ExecuteNonQuery();}
                rows=ReadOrder(connection,transaction,"OrderItemsForEditSql",id);
                using(var legacy=Restore(header.Rows[0],rows,id)) Check(NumberValue(Editor(legacy),"tbTargetWidth")==3300 && NumberValue(Editor(legacy),"inputNumericBox4")==2,"legacy order restored from existing item columns");
            }
        }
    }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            string path=Path.GetFullPath(args[0]);string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"../../dll/MySql.Data.dll")));
            app=Assembly.LoadFrom(path);repository=app.GetType("PineTree.Classes.OrderRepository",true);
            using(var connection=(DbConnection)app.GetType("PineTree.Classes.DB",true).GetMethod("CreateConnection").Invoke(null,null))
            {
                connection.Open();using(var transaction=connection.BeginTransaction())
                {
                    try {Run(connection,transaction,output);}
                    finally {transaction.Rollback();Console.WriteLine("Rolled back all test orders and item data");}
                }
                Check(Query(connection,null,"SELECT COUNT(*) AS N FROM OrderInfo WHERE OrderId=@OrderId",createdOrderId).Rows[0][0].ToString()=="0","test order absent after rollback");
                Check(Query(connection,null,"SELECT COUNT(*) AS N FROM OrderItems WHERE OrderId=@OrderId",createdOrderId).Rows[0][0].ToString()=="0","test items absent after rollback");
            }
            Console.WriteLine("PASS: database save/reload checks: "+checks);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
