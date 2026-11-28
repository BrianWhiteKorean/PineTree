using System;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
class DesignPadAccessoryRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static Assembly app;static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n,F).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 static decimal Number(object pad,string n){return Convert.ToDecimal(Get(Field(pad,n),"Value"));}
 static int GetColumnIndex(object grid,string name){var cols=Get(grid,"Cols");return Convert.ToInt32(Get(cols.GetType().GetProperty("Item",new[]{typeof(string)}).GetValue(cols,new object[]{name}),"Index"));} static int Count(object o){return Convert.ToInt32(Get(o,"Count"));}
 static object At(object o,int i){return o.GetType().GetProperty("Item",new[]{typeof(int)}).GetValue(o,new object[]{i});}
 static void Summary(object pad,decimal expected){var g=Field(pad,"FGrid1");var rows=Get(g,"Rows");var last=At(rows,Count(rows)-1);Check((bool)Get(last,"IsNode"),"Summary row exists");var value=g.GetType().GetProperty("Item",new[]{typeof(int),typeof(string)}).GetValue(g,new object[]{Count(rows)-1,"TotalAmount"});Check(Convert.ToDecimal(value)==expected,"Summary total "+value+" expected "+expected);Check(Number(pad,"tbAccessory")==expected,"Footer matches summary");}
 static DataRow Add(object pad,string name,decimal price,decimal qty){var item=Activator.CreateInstance(app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem"));Set(item,"PriceItemName",name);Set(item,"UnitPrice",price);Set(item,"Qty",qty);Call(pad,"AddOrderItem",item,null);var t=(DataTable)Field(pad,"m_orderItems");var row=t.Rows[t.Rows.Count-1];Call(pad,"CalculateAccessoryAmounts",row);Call(pad,"UpdateOrderItemTotals");return row;}
 static void QueryChecks(){var repo=app.GetType("PineTree.Classes.PriceItemRepository");var query=repo.GetMethod("GetBasicAccessoryPriceItems");
  using(var connection=(DbConnection)app.GetType("PineTree.Classes.DB").GetMethod("CreateConnection").Invoke(null,null))using(var command=connection.CreateCommand()){
   connection.Open();command.CommandText="SELECT pi.SeqNo,pi.PriceCategory FROM PriceItems pi WHERE pi.IsActive=1 AND TRIM(pi.Material) IN ('부품','구성품') AND (pi.CustCode IS NULL OR pi.CustCode='')";
   var all=new DataTable();using(var r=command.ExecuteReader())all.Load(r);var categories=new HashSet<string>();foreach(DataRow r in all.Rows)categories.Add(Convert.ToString(r["PriceCategory"]));Check(categories.Count>0,"Accessory fixtures exist in live catalog");
   foreach(var cat in categories){var expected=new HashSet<long>();foreach(DataRow r in all.Rows)if(Convert.ToString(r["PriceCategory"])==cat)expected.Add(Convert.ToInt64(r["SeqNo"]));var actual=new HashSet<long>();var result=(DataTable)query.Invoke(null,new object[]{cat,null,null});foreach(DataRow r in result.Rows){Check(Convert.ToString(r["PriceCategory"])==cat,"Category locked in SQL");actual.Add(Convert.ToInt64(r["SeqNo"]));}Check(actual.SetEquals(expected),"Only parts/components in selected category: "+cat);
    using(var dialog=(Form)Activator.CreateInstance(app.GetType("PineTree.Dialogs.DialogPriceItemList"),new object[]{"","","",cat})){var combo=Field(dialog,"cbCategory");Check((bool)Get(combo,"Enabled") && (int)Get(combo,"SelectedIndex")==0,"Category starts at all and can be changed");Call(dialog,"SelectComboItem",combo,cat);var grid=Field(dialog,"FGrid1");var data=(DataTable)Get(grid,"DataSource");Check(data.Rows.Count==result.Rows.Count,"Dialog uses accessory query");Call(dialog,"LoadPriceItems");Check(((DataTable)Get(grid,"DataSource")).Rows.Count==result.Rows.Count,"Search reload keeps filter");}
   }
  }
 }
 [STAThread]static int Main(string[]args){try{Application.EnableVisualStyles();app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=app.GetType("PineTree.UserControls.UcOrderDesignPad");var pad=(UserControl)FormatterServices.GetUninitializedObject(type);typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(pad,new object[0]);using(pad){Call(pad,"InitializeComponent");Call(pad,"InitializeOrderPricing");Call(pad,"InitOrderItemTable");Call(pad,"InitOrderEditor");Call(pad,"InitPreviewEvent");Summary(pad,0);
 var table=new DataTable();table.Columns.Add("CurrentUnitPrice",typeof(decimal));table.Columns.Add("CustCode");table.Rows.Add(4000m,DBNull.Value);Call(pad,"ApplyProductPriceCandidates",table,"");
 var cats=new DataTable();cats.Columns.Add("CategoryName");cats.Rows.Add("현수막");Call(pad,"BindProductCombo",Field(pad,"cbCategoryName"),cats,"CategoryName");Call(pad,"UpdateSelectedOrderEditor");var editor=Field(pad,"m_orderEditor");var pt=app.GetType("PineTree.Classes.OrderSourcePage");var page=Activator.CreateInstance(pt);pt.GetField("SourceWidthMm").SetValue(page,1000d);pt.GetField("SourceHeightMm").SetValue(page,1000d);Call(editor,"ApplySourceMeasurements",page); var a=Add(pad,"Accessory A",1000m,2m);Summary(pad,2200);var b=Add(pad,"Accessory B",500m,3m);Summary(pad,3850);
 var grid=Field(pad,"FGrid1");var rows=Get(grid,"Rows");int first=Convert.ToInt32(Get(rows,"Fixed"));
 Check(Object.ReferenceEquals(Call(pad,"GetAccessoryRow",first),a),"First data row mapped by binding");Check(Call(pad,"GetAccessoryRow",Count(rows)-1)==null,"Summary is not an accessory");
 Set(grid,"Row",Count(rows)-1);Call(pad,"btnRemove_Click",null,EventArgs.Empty);Summary(pad,3850);Check(((DataTable)Field(pad,"m_orderItems")).Rows.Count==2,"Remove ignores summary");
 Set(Field(pad,"tbQty"),"Value",4m);Check(Number(pad,"tbAccessory")==3850 && Number(pad,"tbTotalAmount")==19850,"Order quantity does not multiply accessory sum");
 b["Qty"]=4m;var col=At(Get(grid,"Cols"),GetColumnIndex(grid,"Qty"));var evt=Activator.CreateInstance(grid.GetType().Assembly.GetType("C1.Win.C1FlexGrid.RowColEventArgs"),new object[]{first+1,GetColumnIndex(grid,"Qty")});Call(grid,"OnAfterEdit",evt);Summary(pad,4400);Check(Number(pad,"tbTotalAmount")==20400,"Edited quantity refreshes final amount"); Set(grid,"Row",first);Call(pad,"btnRemove_Click",null,EventArgs.Empty);Summary(pad,2200);Check(Convert.ToInt32(b["LineNo"])==1,"Delete renumbers rows");
 Set(grid,"Row",first);Call(pad,"btnRemove_Click",null,EventArgs.Empty);Summary(pad,0);Check(((DataTable)Field(pad,"m_orderItems")).Rows.Count==0,"Last accessory can be removed");
 Set(grid,"Row",first);Call(pad,"btnRemove_Click",null,EventArgs.Empty);Summary(pad,0);
 }if(args.Length>1)QueryChecks();Console.WriteLine("PASS: "+checks+" accessory checks");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
