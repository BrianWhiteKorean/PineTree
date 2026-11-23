using System;
using System.Collections;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
internal static class OrderListItemsRegression
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void FieldSet(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 static object Cell(object grid,int row,string name){return ((IDictionary)((IList)Field(grid,"_rows"))[row])[name];}
 static string Text(object grid,int row,string name){return (string)Get(Cell(grid,row,name),"Text");}
 static object Bare(Type type,Type baseType){var o=FormatterServices.GetUninitializedObject(type);baseType.GetConstructor(Type.EmptyTypes).Invoke(o,new object[0]);return o;}
 static DataTable Items(){
  var t=new DataTable();foreach(var n in new[]{"OrderId","OrderItemId","OrderNo","CustName","CustomName","ItemName","SpecText","WidthMm","HeightMm","Qty","AreaQty","UnitName","ProductionStatus","OrderDate","OrderTitle","EditorStateJson"})t.Columns.Add(n);
  t.Rows.Add("101","11","20261105-002","거래처 A","첫 주문명","현수막","", "600","1800","1","1.08","회배","신규","2026-11-05","주문 A","");
  t.Rows.Add("101","12","20261105-002","거래처 A","두 번째 주문명","페트","", "700","2000","2","2.8","회배","신규","2026-11-05","주문 A","");
  t.Rows.Add("101","13","20261105-002","거래처 A","","배너","900*1500", "900","1500","3","4.05","개","신규","2026-11-05","주문 A","");
  t.Rows.Add("102","21","20261105-001","거래처 B","별도 주문","현수막","", "500","1000","1","0.5","회배","신규","2026-11-05","주문 B","");return t;
 }
 [STAThread]static int Main(string[] args){try{
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var data=Items();var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
  data.Columns.Add("StoredOrderTitle");
  var header=new DataTable();
  foreach(var name in new[]{"OrderNo","OrderDate","CustName","CustCode","OrderChannel","DeliveryType","DeliveryFee","ProductionStatus","OrderDescription","RequestedDueAt","OrderTitle"})header.Columns.Add(name);
  var order=header.Rows.Add("20261105-007","2026-11-05","거래처 A","1","온라인","택배","0","신규","비고","2026-11-06","다시 여는 주문");
  string[] orderEditors={"PineTree.UserControls.UcOrderDesignPad","PineTree.Dialogs.DialogOrderDesignPad"};
  for(int i=0;i<orderEditors.Length;i++){
   var baseType=i==0 ? typeof(UserControl) : typeof(Form);
   using(var editor=(Control)Bare(app.GetType(orderEditors[i],true),baseType)){
    Call(editor,"InitializeComponent");
    Call(editor,"InitOrderItemTable");
    Call(editor,"InitOrderEditor");
    Call(editor,"ApplyLoadedOrder",order);
    Check((string)Get(Field(editor,"tbOrderNo"),"Text")=="20261105-007","edit screen displays stored order number");
    Check((bool)Get(Field(editor,"tbOrderNo"),"ReadOnly"),"order number remains read-only");
    var title=Field(editor,"tbOrderName");
    Check((string)Get(title,"Text")=="다시 여는 주문","edit screen restores the stored order title");
    Set(title,"Text",i==0 ? "  저장된 주문 A  " : "  수정된 주문 B  ");
    var savedTitle=(string)Get(Call(editor,"CreateOrderSaveData"),"OrderTitle");
    data.Rows[i==0 ? 0 : 3]["StoredOrderTitle"]=savedTitle;
    Set(title,"Text","   ");
    Check((string)Get(Call(editor,"CreateOrderSaveData"),"OrderTitle")=="","clearing the title leaves it empty");
    Set(title,"Text","새 주문에 복사되면 안 되는 주문명");
    editor.GetType().GetMethod("ResetNewOrder").Invoke(editor,null);
    Check((string)Get(title,"Text")=="","new order clears the previous title");
   }
  }
  using(var dash=(UserControl)Bare(app.GetType("PineTree.UserControls.UcDashboardHome",true),typeof(UserControl))){
   var grid=(Control)Call(dash,"CreateTodayOrderGrid");FieldSet(dash,"_todayOrderGrid",grid);FieldSet(dash,"_todayOrders",data);FieldSet(dash,"_selectedDate",DateTime.Today);Set(grid,"TotalText",Call(dash,"CreateTotalText",2));grid.BackColor=Color.FromArgb(30,34,59);Set(grid,"PageSize",10);Set(grid,"TotalCount",data.Rows.Count);Call(dash,"BindTodayOrderPage");
   Check(((IList)Field(grid,"_rows")).Count==4,"dashboard includes all items");
   Check(Text(grid,0,"OrderNo")=="20261105-002"&&Text(grid,1,"OrderNo")==""&&Text(grid,2,"OrderNo")==""&&Text(grid,3,"OrderNo")=="20261105-001","one order number per order");
   Check(Text(grid,0,"Manage")=="상세 >"&&Text(grid,1,"Manage")==""&&Text(grid,2,"Manage")==""&&Text(grid,3,"Manage")=="상세 >","one detail link per order");
   Check(Text(grid,0,"Item")=="현수막"&&Text(grid,1,"Item")=="페트"&&Text(grid,2,"Item")=="배너","item column uses item names");
   Check(Text(grid,0,"OrderTitle")=="저장된 주문 A"&&Text(grid,1,"OrderTitle")==""&&Text(grid,3,"OrderTitle")=="수정된 주문 B","dashboard displays the title sent by each editor once per order");
   data.Rows[3]["StoredOrderTitle"]=DBNull.Value;
   Call(dash,"BindTodayOrderPage");
   Check(Text(grid,3,"OrderTitle")=="","missing title stays blank");
   var columns=(IList)Get(grid,"Columns");
   var expectedKeys=new[]{"OrderNo","Customer","Item","Size","Quantity","OrderTitle","Status","Manage"};
   var expectedWidths=new[]{160,190,210,165,100,350,120,0};
   Check(columns.Count==expectedKeys.Length,"dashboard has eight columns");
   for(int i=0;i<expectedKeys.Length;i++){
    Check((string)Get(columns[i],"Key")==expectedKeys[i],"dashboard column order");
    Check((int)Get(columns[i],"Width")==expectedWidths[i],"dashboard column width");
   }
   foreach(int gridWidth in new[]{1320,1550,1630}){
    grid.Width=gridWidth;
    int previousManageWidth=Math.Max(80,gridWidth-44-1245);
    Check((int)Call(grid,"GetColumnWidth",columns[7])==previousManageWidth-50,"management width is 50 px smaller at each window size");
   }
   Check(Text(grid,1,"Size")=="700 x 2,000"&&Text(grid,1,"Quantity")=="2.8","each item's dimensions and area");
   Check((int)dash.GetType().GetMethod("CountOrders",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{data})==2,"dashboard count uses orders");
   var eventType=app.GetType("PineTree.UserControls.PagedGridCellClickEventArgs",true);var rows=(IList)Field(grid,"_rows");
   var hidden=Activator.CreateInstance(eventType,new object[]{1,"Manage",rows[1]});Call(dash,"TodayOrderGrid_CellClicked",grid,hidden);Check((int)Get(grid,"SelectedRowIndex")==1,"hidden link only selects item; does not open dialog");
   grid.Size=new Size(1550,320);using(var bitmap=new Bitmap(grid.Width,grid.Height)){grid.DrawToBitmap(bitmap,grid.ClientRectangle);bitmap.Save(Path.Combine(output,"dashboard.png"));}
   Set(grid,"PageSize",2);Set(grid,"PageIndex",2);Call(dash,"BindTodayOrderPage");Check(Text(grid,0,"OrderNo")==""&&Text(grid,0,"Manage")==""&&Text(grid,1,"OrderNo")=="20261105-001","page continuation keeps identifiers unique");
   FieldSet(dash,"_todayOrders",data.Clone());Set(grid,"PageIndex",1);Call(dash,"BindTodayOrderPage");Check(((IList)Field(grid,"_rows")).Count==0,"empty dashboard works");grid.Dispose();
  }
  using(var info=(UserControl)Bare(app.GetType("PineTree.UserControls.UcOrderInfo",true),typeof(UserControl))){
   Call(info,"InitializeComponent");Call(info,"InitializeOrderGrid");var grid=(Control)Field(info,"orderGrid");Set(grid,"PageSize",10);Call(info,"BindOrders",data);
   Check(((IList)Field(grid,"_rows")).Count==4,"management includes all items");
   Check(Text(grid,0,"WorkSheet")=="보기"&&Text(grid,1,"WorkSheet")==""&&Text(grid,2,"WorkSheet")==""&&Text(grid,3,"WorkSheet")=="보기","one view button per order");
   Check(Get(Cell(grid,1,"WorkSheet"),"CellType").ToString()=="Text","hidden button does not render button frame");
   Check(((string)Get(grid,"TotalText")).Contains("2건")&&((string)Get(grid,"TotalText")).Contains("4개"),"order and item counts distinguished");
   Set(grid,"SelectedRowIndex",1);Check((long)Field(info,"_selectedOrderId")==101,"second item selects correct order");
   var et=app.GetType("PineTree.UserControls.PagedGridCellClickEventArgs",true);var rows=(IList)Field(grid,"_rows");var ev=Activator.CreateInstance(et,new object[]{1,"WorkSheet",rows[1]});Call(info,"OrderGrid_CellClicked",grid,ev);Check((long)Field(info,"_selectedOrderId")==101,"hidden view area cannot open print dialog");
   grid.Size=new Size(1550,320);using(var bitmap=new Bitmap(grid.Width,grid.Height)){grid.DrawToBitmap(bitmap,grid.ClientRectangle);bitmap.Save(Path.Combine(output,"management.png"));}
   Set(grid,"PageSize",2);Set(grid,"PageIndex",2);Call(info,"BindOrderPage");Check(Text(grid,0,"WorkSheet")==""&&Text(grid,1,"WorkSheet")=="보기","view button remains unique across pages");Set(grid,"SelectedRowIndex",1);Check((long)Field(info,"_selectedOrderId")==102,"page two order selection");
  }
  Console.WriteLine("PASS "+checks+" multi-item order list checks");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
