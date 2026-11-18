using System;
using System.IO;
using System.Data;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
internal static class DesignPadPricingRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static decimal Number(object o,string n){return Convert.ToDecimal(Get(Field(o,n),"Value"));}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 static DataTable Rates(){var t=new DataTable();t.Columns.Add("CustCode");t.Columns.Add("CurrentUnitPrice",typeof(decimal));return t;}
 [STAThread]static int Main(string[] args){try{
  Application.EnableVisualStyles();var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=app.GetType("PineTree.UserControls.UcOrderDesignPad",true);
  var pad=(UserControl)FormatterServices.GetUninitializedObject(type);typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(pad,new object[0]);
  using(pad){Call(pad,"InitializeComponent");Call(pad,"InitializeOrderPricing");Call(pad,"InitOrderItemTable");Call(pad,"InitOrderEditor");
   var categories=new DataTable();categories.Columns.Add("CategoryName");categories.Rows.Add("현수막");Call(pad,"BindProductCombo",Field(pad,"cbCategoryName"),categories,"CategoryName");Call(pad,"UpdateSelectedOrderEditor");
   var editor=(UserControl)Field(pad,"m_orderEditor");Check(editor!=null,"material editor activated");Check(editor.GetType().GetField("inputNumericBox4",F)==null,"removed quantity control is absent");
   var pageType=app.GetType("PineTree.Classes.OrderSourcePage",true);var page=Activator.CreateInstance(pageType,true);pageType.GetField("SourceWidthMm").SetValue(page,1800d);pageType.GetField("SourceHeightMm").SetValue(page,1200d);Call(editor,"ApplySourceMeasurements",page);
   Check(Number(pad,"tbArea")==2.16m && Number(editor,"inputNumericBox3")==2.16m,"both area displays show one item only");
   Check(Number(pad,"tbQty")==1,"quantity defaults to one");
   var rates=Rates();rates.Rows.Add(DBNull.Value,4000m);rates.Rows.Add("C1",5000m);rates.Rows.Add("OTHER",9999m);
   Call(pad,"ApplyProductPriceCandidates",rates,"C1");Check(Number(pad,"tbUnitPrice")==5000 && Number(pad,"tbAmount")==10800,"dedicated customer rate selected");
   Set(Field(pad,"tbQty"),"Value",3m);
   Check(Number(pad,"tbTotalAmount")==32400,"quantity applied exactly once in footer");
   Check(Number(editor,"inputNumericBox3")==2.16m && Number(pad,"tbArea")==2.16m,"quantity does not multiply visible area");
   Check((decimal)Get(editor,"Quantity")==3 && (decimal)Get(editor,"AreaQuantity")==6.48m,"stored total-area contract preserves quantity");
   Check(Number(pad,"tbWorkSum")==0 && Number(pad,"tbAccessory")==0,"undefined costs remain zero");
   Call(pad,"ApplyProductPriceCandidates",rates,"NO_DEDICATED");Check(Number(pad,"tbUnitPrice")==4000 && Number(pad,"tbTotalAmount")==25920,"basic price fallback with same quantity");
   rates.Rows.Add("C1",DBNull.Value);Call(pad,"ApplyProductPriceCandidates",rates,"C1");Check(Number(pad,"tbUnitPrice")==5000,"missing price does not shadow an active dedicated rate");
   rates.Rows.Add("C1",6000m);Call(pad,"ApplyProductPriceCandidates",rates,"C1");Check(Convert.IsDBNull(Get(Field(pad,"tbUnitPrice"),"Value"))&&Convert.IsDBNull(Get(Field(pad,"tbTotalAmount"),"Value")),"ambiguous rates are not silently chosen");
   Call(pad,"ApplyProductPriceCandidates",Rates(),"C1");Check(Convert.IsDBNull(Get(Field(pad,"tbAmount"),"Value")),"missing rate does not show zero as a confirmed price");
   var zero=Rates();zero.Rows.Add(DBNull.Value,0m);Call(pad,"ApplyProductPriceCandidates",zero,"C1");Check(Number(pad,"tbTotalAmount")==0,"explicit zero price is accepted");
   Call(pad,"ApplyProductPriceCandidates",rates,"NO_DEDICATED");Set(Field(editor,"tbTargetWidth"),"Value",1000m);Set(Field(editor,"tbTargetHeight"),"Value",500m);
   Check(Number(pad,"tbArea")==1m && Number(pad,"tbTotalAmount")==12000,"small area applies one-square-metre minimum and quantity");
   Set(Field(pad,"tbQty"),"Value",2.6m);Check(Number(pad,"tbQty")==3,"quantity normalized to a whole count");
   Set(Field(pad,"tbQty"),"Value",-1m);Check(Number(pad,"tbQty")==0 && Number(pad,"tbTotalAmount")==0,"negative quantity normalized to zero");
   Set(Field(pad,"tbQty"),"Value",2m);
   var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);Check((decimal)Get(state,"Quantity")==2,"footer quantity captured in editor state");
   using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())){restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{state});Check((decimal)Get(restored,"Quantity")==2 && Number(restored,"inputNumericBox3")==.5m,"quantity round trip keeps per-item area display");}
   foreach(var n in new[]{"tbArea","tbUnitPrice","tbAmount","tbWorkSum","tbAccessory","tbTotalAmount"})Check((bool)Get(Field(pad,n),"ReadOnly"),"derived footer value is read-only: "+n);
   Set(Field(pad,"tbQty"),"Value",1m);Set(Field(editor,"tbTargetWidth"),"Value",183m);Set(Field(editor,"tbTargetHeight"),"Value",123m);
   
   Check((decimal)Get(editor,"AreaQuantity")==.022509m,"actual small area is preserved for calculation");
   Check(Number(pad,"tbArea")==1m && Number(pad,"tbAmount")==4000m && Number(pad,"tbTotalAmount")==4000m,"customer screenshot case charges selected product one-area price");
   Check((string)Get(Field(pad,"tbArea"),"Text")=="1.00","billable area displays two decimal places");
   Check((string)Get(Field(pad,"tbUnitPrice"),"Text")=="4,000" && (string)Get(Field(pad,"tbAmount"),"Text")=="4,000","unit price and amount display whole currency");
   Set(Field(editor,"tbTargetWidth"),"Value",1000m);Set(Field(editor,"tbTargetHeight"),"Value",1000m);
   Check(Number(pad,"tbAmount")==4000m,"exact one-square-metre boundary uses base charge");
   Set(Field(editor,"tbTargetWidth"),"Value",1010m);Set(Field(pad,"tbQty"),"Value",2m);
   Check(Number(pad,"tbArea")==1.01m && Number(pad,"tbAmount")==4040m && Number(pad,"tbTotalAmount")==8080m,"above threshold uses actual area times selected rate and count");
   Check((string)Get(Field(pad,"tbArea"),"Text")=="1.01" && (string)Get(Field(pad,"tbTotalAmount"),"Text")=="8,080","area and total format after automatic recalculation");
   Set(Field(editor,"tbTargetWidth"),"Value",0m);
   Check(Number(pad,"tbArea")==0m && Number(pad,"tbTotalAmount")==0m,"missing dimensions do not create a minimum charge");
   Call(pad,"InvalidateSelectedProductPrice");Check(Convert.IsDBNull(Get(Field(pad,"tbUnitPrice"),"Value")),"selection reset clears previous product price");
  }
  Console.WriteLine("PASS: footer pricing / quantity / customer priority / missing rates / zero fees / state: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
