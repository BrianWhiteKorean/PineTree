using System;
using System.Data;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.IO;
internal static class DesignPadOrderNameRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 [STAThread] static int Main(string[] args){try{
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=app.GetType("PineTree.UserControls.UcOrderDesignPad",true);
  var pad=(UserControl)FormatterServices.GetUninitializedObject(type);typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(pad,new object[0]);
  using(pad){Call(pad,"InitializeComponent");Call(pad,"InitOrderItemTable");Call(pad,"InitOrderEditor");
   var itemType=app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem",true);
   var main=Activator.CreateInstance(itemType);Set(main,"PriceItemSeqNo",101L);Set(main,"PriceItemName","현수막 기본");Set(main,"CustomName","현수막 기본");Set(main,"BillingBasis","면적");Set(main,"UnitName","회배");Set(main,"UnitPrice",4000m);Set(main,"Qty",1m);
   Call(pad,"AddOrderItem",main,null);
   var accessory=Activator.CreateInstance(itemType);Set(accessory,"PriceItemSeqNo",102L);Set(accessory,"PriceItemName","부속품");Set(accessory,"CustomName","고리");Set(accessory,"BillingBasis","수량");Set(accessory,"UnitPrice",1000m);Set(accessory,"Qty",2m);Set(accessory,"SupplyAmount",2000m);Set(accessory,"VatAmount",200m);Set(accessory,"TotalAmount",2200m);
   Call(pad,"AddOrderItem",accessory,null);var table=(DataTable)Field(pad,"m_orderItems");
   using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder.UcOrder수성_그룹A",true))){
    Set(Field(editor,"tbTargetWidth"),"Value",1800m);Set(Field(editor,"tbTargetHeight"),"Value",1200m);editor.GetType().GetMethod("SetOrderQuantity").Invoke(editor,new object[]{1m});
    Call(pad,"UpdateBannerOrderValues",table.Rows[0],editor);
    Check((decimal)table.Rows[0]["AreaQty"]==2.16m,"area is 2.16 square metres");
    Check((decimal)table.Rows[0]["SupplyAmount"]==8640m && (decimal)table.Rows[0]["TotalAmount"]==9504m,"area price with existing VAT");
    Set(Field(pad,"tbOrderName"),"Text","  아부다비 투자 포럼  ");
    var model=Call(pad,"CreateOrderSaveData");var items=(IList)Get(model,"Items");
    Check((string)Get(model,"OrderTitle")=="아부다비 투자 포럼","order header uses entered name without item count suffix");
    Check((string)Get(items[0],"CustomName")=="현수막 기본","accessory line keeps its own name");
    Check((string)Get(items[0],"ItemName")=="현수막 기본" && (long)Get(items[0],"PriceItemSeqNo")==101L,"pricing identity remains intact");
    Check((string)Get(items[1],"CustomName")=="고리" && (decimal)Get(items[1],"TotalAmount")==2200m,"accessory name and amount preserved");
    Check((decimal)Get(items[0],"TotalAmount")+(decimal)Get(items[1],"TotalAmount")==11704m,"product plus accessory totals include both lines once");
    editor.GetType().GetMethod("SetOrderQuantity").Invoke(editor,new object[]{2m});Call(pad,"UpdateBannerOrderValues",table.Rows[0],editor);
    Check((decimal)table.Rows[0]["TotalAmount"]==19008m && (decimal)table.Rows[1]["TotalAmount"]==2200m,"quantity recalculates product area without changing accessory");
   }
   var rowTable=new DataTable();foreach(var n in new[]{"OrderNo","OrderTitle","OrderDate","CustName","CustCode","OrderChannel","DeliveryType","DeliveryFee","ProductionStatus","OrderDescription","RequestedDueAt"})rowTable.Columns.Add(n);
   var row=rowTable.Rows.Add("TEST","다시 여는 주문","2026-11-13","고객","1","온라인","택배","0","신규","비고",DBNull.Value);
   Call(pad,"ApplyLoadedOrder",row);Check((string)Get(Field(pad,"tbOrderName"),"Text")=="다시 여는 주문","loading restores order title into text box");
  }
  var display=new DataTable();display.Columns.Add("OrderTitle");display.Columns.Add("CustomName");display.Columns.Add("ItemName");var r=display.Rows.Add("입력한 주문명","부속품 이름","가격표 품명");
  foreach(var name in new[]{"UcOrderInfo","UcDashboardHome"}){
   var m=app.GetType("PineTree.UserControls."+name).GetMethod("GetOrderItemDisplayName",BindingFlags.NonPublic|BindingFlags.Static);
   Check((string)m.Invoke(null,new object[]{r,true})=="입력한 주문명",name+" primary displays order title");
   Check((string)m.Invoke(null,new object[]{r,false})=="부속품 이름",name+" accessory remains distinct");
   r["OrderTitle"]=DBNull.Value;Check((string)m.Invoke(null,new object[]{r,true})=="부속품 이름",name+" legacy title fallback");r["OrderTitle"]="입력한 주문명";
  }
  Console.WriteLine("PASS: design pad order name / list display / area / accessory totals: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
