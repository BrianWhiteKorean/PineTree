using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;
class DesignModeExitRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static Assembly app;static int checks;static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Put(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 static Control Bare(string name,Type baseType){var o=FormatterServices.GetUninitializedObject(app.GetType(name,true));baseType.GetConstructor(Type.EmptyTypes).Invoke(o,new object[0]);return (Control)o;}
 [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
 [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr dialog,int itemId);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr buttonId,IntPtr button);
 static void ClickReset(Button reset,DialogResult? answer){
  bool seen=false;
  using(var timer=new Timer{Interval=30}){
   timer.Tick+=delegate{
    var dialog=GetActiveWindow();
    var buttonId=(int)(answer ?? DialogResult.No);
    var button=GetDlgItem(dialog,buttonId);
    if(button==IntPtr.Zero)return;
    seen=true;
    timer.Stop();
    SendMessage(dialog,0x0111,(IntPtr)buttonId,button);
   };
   timer.Start();
   Call(reset,"OnClick",EventArgs.Empty);
  }
  Check(seen==answer.HasValue,answer.HasValue ? "Modified draft asks for confirmation" : "Initial draft skips confirmation");
 }
 static void CheckModeButtons(Form main, Panel panel, Control design, Control editor, bool reopen, bool resetFromLeft, bool clean){
  var rightPanel=(Panel)Field(main,"_rightModeSwitchPanel");
  var preserve=(Button)rightPanel.Controls["preserveModeButton"];
  var reset=(Button)rightPanel.Controls["rightModeButton"];
  Check(preserve.Text=="관리모드로 변경(작업내용 보존)","Preserve button has the requested label");
  Check(preserve.Right<reset.Left && preserve.Top==reset.Top,"Design mode buttons are side by side");
  Check(TextRenderer.MeasureText(preserve.Text,preserve.Font).Width<preserve.Width,"Preserve label fits its button");
  Call(main,"SetManagementMode",false);
  main.ClientSize=new Size(main.ClientSize.Width+100,main.ClientSize.Height);
  Check(rightPanel.Right==panel.ClientSize.Width,"Both design mode buttons stay right anchored");
  var remark=Field(editor,"tbRemark");
  var text=remark.GetType().GetProperty("Text");
  text.SetValue(remark,"보존할 작업 메모",null);
  Call(preserve,"OnClick",EventArgs.Empty);
  Check((bool)Field(main,"_isManagementMode"),"Preserve returns to management mode");
  Call(Field(main,"_modeButton"),"OnClick",EventArgs.Empty);
  Check(!(bool)Field(main,"_isManagementMode")&&ReferenceEquals(design,Field(main,"_designModeContainer"))&&
   (string)text.GetValue(remark,null)=="보존할 작업 메모","Returning to design mode keeps the editor and draft");

  var resetButton=resetFromLeft ? (Button)Field(main,"_modeButton") : reset;
  if(clean){
   text.SetValue(remark,string.Empty,null);
   ClickReset(resetButton,null);
  }else{
   ClickReset(resetButton,DialogResult.No);
   Check(!(bool)Field(main,"_isManagementMode")&&!design.IsDisposed&&
    (string)text.GetValue(remark,null)=="보존할 작업 메모","Declining reset keeps design mode and the draft");
   ClickReset(resetButton,DialogResult.Yes);
  }
  Check((bool)Field(main,"_isManagementMode"),"Reset returns to management mode");
  Check(design.IsDisposed&&editor.IsDisposed&&Field(main,"_designModeContainer")==null,
   "Reset discards the draft and releases the old editor");
  if(reopen){
   Call(Field(main,"_modeButton"),"OnClick",EventArgs.Empty);
   var fresh=Field(Field(main,"_designModeContainer"),"_orderDesigner");
   Check(!(bool)Field(main,"_isManagementMode")&&!ReferenceEquals(fresh,editor),"Design mode starts with a fresh editor");
   Check((string)text.GetValue(Field(fresh,"tbRemark"),null)==string.Empty&&
    (string)text.GetValue(Field(fresh,"tbOrderName"),null)==string.Empty,"Fresh editor has no prior remarks or title");
   Check(((System.Data.DataTable)Field(fresh,"m_orderItems")).Rows.Count==0,"Fresh editor has no prior order items");
   Check(!(bool)fresh.GetType().GetProperty("HasDraftChanges",F).GetValue(fresh,null),"A newly opened editor starts clean with configured defaults");
  }
 }
 static void CheckDraftInputs(Control editor){
  Call(editor,"InitializeOrderPricing");
  Call(editor,"InitOrderItemTable");
  Call(editor,"InitOrderEditor");
  var delivery=new System.Data.DataTable();
  delivery.Columns.Add("ItemName");
  delivery.Columns.Add("ItemValue");
  delivery.Rows.Add("선불택배","3500");
  Call(editor,"LoadTextCombo",Field(editor,"cbDeliveryType"),delivery.Select());
  editor.GetType().GetMethod("ResetNewOrder").Invoke(editor,null);
  var dirty=editor.GetType().GetProperty("HasDraftChanges",F);
  Check(!(bool)dirty.GetValue(editor,null),"A reset editor starts without draft changes");
  foreach(var name in new[]{"tbOrderName","tbCustName","tbRemark"}){
   var input=Field(editor,name);
   var text=input.GetType().GetProperty("Text");
   text.SetValue(input,"작성 중",null);
   Check((bool)dirty.GetValue(editor,null),"Editing "+name+" changes the draft");
   text.SetValue(input,string.Empty,null);
  }
  var qty=Field(editor,"tbQty");
  var quantity=qty.GetType().GetProperty("Value");
  quantity.SetValue(qty,2m,null);
  Check((bool)dirty.GetValue(editor,null),"Changing quantity changes the draft");
  quantity.SetValue(qty,1m,null);
  var fee=Field(editor,"tbDeliveryFee");
  var feeValue=fee.GetType().GetProperty("Value");
  Check(Convert.ToDecimal(feeValue.GetValue(fee,null))==3500m,"Default delivery fee is part of the clean state");
  feeValue.SetValue(fee,5000m,null);
  Check((bool)dirty.GetValue(editor,null),"Changing only the delivery fee changes the draft");
  feeValue.SetValue(fee,3500m,null);
  var items=(System.Data.DataTable)Field(editor,"m_orderItems");
  items.Rows.Add(items.NewRow());
  Check((bool)dirty.GetValue(editor,null),"Adding an item changes the draft");
  items.Clear();
  var source=Field(editor,"m_sourceDocument");
  source.GetType().GetProperty("ReferencePath").SetValue(source,"draft.pdf",null);
  Check((bool)dirty.GetValue(editor,null),"Choosing a source changes the draft");
  editor.GetType().GetMethod("ResetNewOrder").Invoke(editor,null);
  Check(!(bool)dirty.GetValue(editor,null),"Resetting the draft clears the change check");
 }
 static FormClosingEventArgs Close(Form main,string action,CloseReason reason,bool preCancelled){
  bool seen=false;Exception failure=null;var e=new FormClosingEventArgs(reason,preCancelled);
  using(var timer=new Timer{Interval=30}){
   timer.Tick+=delegate{foreach(Form dialog in Application.OpenForms){if(dialog.GetType().Name!="DialogDesignModeExit1")continue;seen=true;timer.Stop();
    try{Check(((Control)Field(dialog,"label1")).Text=="프로그램을 종료하시겠습니까?","Exact prompt");Check(((Control)((Control)Field(dialog,"btnExit"))).Text=="예" && ((Control)((Control)Field(dialog,"btnStay"))).Text=="아니오" && ((Control)((Control)Field(dialog,"btnManagement"))).Text=="관리모드로 돌아가기","Three button labels");Check(Object.ReferenceEquals(dialog.AcceptButton,((Control)Field(dialog,"btnStay"))) && Object.ReferenceEquals(dialog.CancelButton,((Control)Field(dialog,"btnStay"))),"Safe Enter/Escape defaults");
     if(action=="close")dialog.Close();else if(action=="escape")Call(dialog,"ProcessDialogKey",Keys.Escape);else ((IButtonControl)Field(dialog,action)).PerformClick();
    }catch(Exception ex){failure=ex;dialog.Close();}break;}
   };
   timer.Start();Call(main,"OnFormClosing",e);timer.Stop();
  }
  if(failure!=null)throw failure;Check(seen==(action!=null),"Prompt shown only in design user-close path");return e;
 }
 [STAThread]static int Main(string[] args){try{Application.EnableVisualStyles();app=Assembly.LoadFrom(Path.GetFullPath(args[0]));using(var main=(Form)Bare("PineTree.Forms.FormMain",typeof(Form)))using(var management=Bare("PineTree.UserControls.UcContainer",typeof(UserControl)))using(var design=Bare("PineTree.UserControls.UcDesignModeContainer",typeof(UserControl)))using(var editor=Bare("PineTree.UserControls.UcOrderDesignPad",typeof(UserControl))){Call(main,"InitializeComponent");Call(editor,"InitializeComponent");Put(design,"_orderDesigner",editor);design.Controls.Add(editor);var panel=(Panel)Field(main,"panel1");panel.Controls.Add(management);panel.Controls.Add(design);Put(main,"_managementContainer",management);Put(main,"_designModeContainer",design);Call(main,"InitializeModeControls");var draft=new TextBox{Text="Keep draft"};design.Controls.Add(draft);
 Put(main,"_isManagementMode",false);Check(Close(main,"btnStay",CloseReason.UserClosing,false).Cancel,"No keeps program running");Check(!(bool)Field(main,"_isManagementMode"),"No keeps design mode");
 Check(Close(main,"close",CloseReason.UserClosing,false).Cancel,"Closing prompt keeps program running");Check(Close(main,"escape",CloseReason.UserClosing,false).Cancel,"Escape keeps program running");
 Check(!Close(main,"btnExit",CloseReason.UserClosing,false).Cancel,"Yes allows exit");
 Check(Close(main,"btnManagement",CloseReason.UserClosing,false).Cancel,"Management cancels program exit");Check((bool)Field(main,"_isManagementMode"),"Management mode activated");Check(Object.ReferenceEquals(design,Field(main,"_designModeContainer"))&&draft.Text=="Keep draft","Mode switch preserves draft and editor instance");
 Check(!Close(main,null,CloseReason.UserClosing,false).Cancel,"Management mode exit unchanged");Put(main,"_isManagementMode",false);Check(!Close(main,null,CloseReason.WindowsShutDown,false).Cancel,"System shutdown is not intercepted");Check(!Close(main,null,CloseReason.ApplicationExitCall,false).Cancel,"Application exit unchanged");Check(Close(main,null,CloseReason.UserClosing,true).Cancel,"Existing cancellation respected");
 CheckDraftInputs(editor);
 CheckModeButtons(main,panel,design,editor,Array.IndexOf(args,"--reopen")>=0,Array.IndexOf(args,"--left")>=0,Array.IndexOf(args,"--clean")>=0);
 }Console.WriteLine("PASS: "+checks+" design-mode exit checks");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
