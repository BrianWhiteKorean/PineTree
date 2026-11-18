using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
class DesignPadSpecificationRegression {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;static Assembly app;static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}static object Get(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}static void Set(object o,string n,object v){o.GetType().GetProperty(n,F).SetValue(o,v,null);}static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Put(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}static void Spec(object pad,string expected){Check((string)Get(Field(pad,"tbSpec"),"Text")==expected,"Spec expected "+expected+" got "+Get(Field(pad,"tbSpec"),"Text"));Check((bool)Get(Field(pad,"tbSpec"),"ReadOnly"),"Spec is automatic/read-only");}
 static Control Bare(string name){var type=app.GetType(name,true);var o=FormatterServices.GetUninitializedObject(type);(type.BaseType==typeof(Form)?typeof(Form):typeof(UserControl)).GetConstructor(Type.EmptyTypes).Invoke(o,new object[0]);Call(o,"InitializeComponent");Call(o,"InitializeOrderPricing");Call(o,"InitOrderItemTable");Call(o,"InitOrderEditor");Call(o,"InitPreviewEvent");return (Control)o;}
 static void Select(object pad,string name,string value){var table=new DataTable();string column=name=="cbItem"?"PriceItemName":"CategoryName";table.Columns.Add(column);table.Rows.Add(value);Call(pad,"BindProductCombo",Field(pad,name),table,column);Put(pad,"m_loadingProductSelection",true);try{Call(pad,"SelectComboText",Field(pad,name),value);}finally{Put(pad,"m_loadingProductSelection",false);}}
 static async Task<string> Verify(string type,string folder){using(var pad=Bare(type)){
  Spec(pad,"");Select(pad,"cbCategoryName","현수막");Select(pad,"cbItem","현수막 기본");Call(pad,"UpdateSelectedOrderEditor");var editor=Field(pad,"m_orderEditor");var source=Field(pad,"m_sourceDocument");await(Task)Call(source,"LoadAsync",Path.Combine(folder,"source.pdf"));Spec(pad,"180*120_1장");
  Set(Field(pad,"tbQty"),"Value",2m);Spec(pad,"180*120_2장");Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,"inputOption7"));Spec(pad,"1800*1200_2장");Call(editor,"RotateSource",90);Spec(pad,"1200*1800_2장");Call(editor,"FlipSourceHorizontally");Spec(pad,"1200*1800_2장");
  var save=Call(pad,"CreateOrderSaveData");Check((string)Get(save,"OrderTitle")=="현수막 기본","Product name stays separate from spec");Check((string)Get(save,"SpecText")=="1200*1800_2장","Save data includes automatic spec");string json=(string)Get(save,"DesignPadStateJson");
  using(var restored=Bare(type)){Call(restored,"ApplyDesignPadState",json);Spec(restored,"1200*1800_2장");Set(Field(restored,"tbQty"),"Value",3m);Spec(restored,"1200*1800_3장");Check((string)Get(Call(restored,"CreateOrderSaveData"),"OrderTitle")=="현수막 기본","Restore uses product name");}
  return json;
 }}
 static void Lists(string json){var rows=new DataTable();foreach(string n in new[]{"OrderId","OrderItemId"})rows.Columns.Add(n,typeof(long));foreach(string n in new[]{"OrderTitle","ItemName","CustomName","SpecText","BillingBasis","EditorStateJson","UnitName","DesignPadStateJson"})rows.Columns.Add(n);foreach(string n in new[]{"WidthMm","HeightMm","Qty","AreaQty"})rows.Columns.Add(n,typeof(decimal));
  var r=rows.NewRow();r["OrderId"]=1L;r["OrderItemId"]=10L;r["OrderTitle"]="Old title";r["ItemName"]="후가공:각목";r["SpecText"]="2ea 1set 1000 이하 기본";r["DesignPadStateJson"]=json;rows.Rows.Add(r);
  r=rows.NewRow();r["OrderId"]=2L;r["DesignPadStateJson"]=json;rows.Rows.Add(r);
  r=rows.NewRow();r["OrderId"]=3L;r["OrderItemId"]=30L;r["ItemName"]="기존 품목";r["SpecText"]="기존 규격";rows.Rows.Add(r);
  var result=(DataTable)app.GetType("PineTree.Classes.OrderRepository").GetMethod("AddDesignPadProductRows",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{rows});Check(result.Rows.Count==4,"Product plus optional accessories; no duplicate empty item");Check((string)result.Rows[0]["ItemName"]=="현수막 기본" && (string)result.Rows[0]["SpecText"]=="1200*1800_2장","List product and spec");Check((string)result.Rows[1]["SpecText"]=="2ea 1set 1000 이하 기본","Accessory spec preserved");Check((string)result.Rows[3]["SpecText"]=="기존 규격","Legacy spec preserved");
  foreach(var name in new[]{"PineTree.UserControls.UcDashboardHome","PineTree.UserControls.UcOrderInfo"}){var type=app.GetType(name);Check((string)type.GetMethod("CreateSizeText",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{result.Rows[0]})=="1200*1800_2장","Order list UI shows automatic spec");Check((string)type.GetMethod("GetOrderItemDisplayName",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{result.Rows[0],true})=="현수막 기본","List product name separate");}
 }
 [STAThread]static int Main(string[]args){int exit=0;try{Application.EnableVisualStyles();app=Assembly.LoadFrom(Path.GetFullPath(args[0]));using(var host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-4000,Top=-4000}){host.Shown+=async delegate{try{string json=await Verify("PineTree.UserControls.UcOrderDesignPad",args[1]);await Verify("PineTree.Dialogs.DialogOrderDesignPad",args[1]);Lists(json);Console.WriteLine("PASS: "+checks+" specification checks");}catch(Exception e){Console.Error.WriteLine(e);exit=1;}finally{host.Close();}};Application.Run(host);}return exit;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
