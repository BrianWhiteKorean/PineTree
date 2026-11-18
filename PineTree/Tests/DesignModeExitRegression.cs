using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
class DesignModeExitRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static Assembly app;static int checks;static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Put(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 static Control Bare(string name,Type baseType){var o=FormatterServices.GetUninitializedObject(app.GetType(name,true));baseType.GetConstructor(Type.EmptyTypes).Invoke(o,new object[0]);return (Control)o;}
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
 [STAThread]static int Main(string[] args){try{Application.EnableVisualStyles();app=Assembly.LoadFrom(Path.GetFullPath(args[0]));using(var main=(Form)Bare("PineTree.Forms.FormMain",typeof(Form)))using(var management=Bare("PineTree.UserControls.UcContainer",typeof(UserControl)))using(var design=Bare("PineTree.UserControls.UcDesignModeContainer",typeof(UserControl))){Call(main,"InitializeComponent");var panel=(Panel)Field(main,"panel1");panel.Controls.Add(management);panel.Controls.Add(design);Put(main,"_managementContainer",management);Put(main,"_designModeContainer",design);Call(main,"InitializeModeControls");var draft=new TextBox{Text="Keep draft"};design.Controls.Add(draft);
 Put(main,"_isManagementMode",false);Check(Close(main,"btnStay",CloseReason.UserClosing,false).Cancel,"No keeps program running");Check(!(bool)Field(main,"_isManagementMode"),"No keeps design mode");
 Check(Close(main,"close",CloseReason.UserClosing,false).Cancel,"Closing prompt keeps program running");Check(Close(main,"escape",CloseReason.UserClosing,false).Cancel,"Escape keeps program running");
 Check(!Close(main,"btnExit",CloseReason.UserClosing,false).Cancel,"Yes allows exit");
 Check(Close(main,"btnManagement",CloseReason.UserClosing,false).Cancel,"Management cancels program exit");Check((bool)Field(main,"_isManagementMode"),"Management mode activated");Check(Object.ReferenceEquals(design,Field(main,"_designModeContainer"))&&draft.Text=="Keep draft","Mode switch preserves draft and editor instance");
 Check(!Close(main,null,CloseReason.UserClosing,false).Cancel,"Management mode exit unchanged");Put(main,"_isManagementMode",false);Check(!Close(main,null,CloseReason.WindowsShutDown,false).Cancel,"System shutdown is not intercepted");Check(!Close(main,null,CloseReason.ApplicationExitCall,false).Cancel,"Application exit unchanged");Check(Close(main,null,CloseReason.UserClosing,true).Cancel,"Existing cancellation respected");
 }Console.WriteLine("PASS: "+checks+" design-mode exit checks");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
