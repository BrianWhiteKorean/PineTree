using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
internal static class DesignPadOptionalAccessoriesRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 delegate bool Callback(IntPtr w,IntPtr data);
 [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint thread,Callback cb,IntPtr data);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr parent,Callback cb,IntPtr data);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr w,StringBuilder s,int n);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr w,int msg,IntPtr wp,IntPtr lp);
 [STAThread]static int Main(string[] args){try{
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=app.GetType("PineTree.UserControls.UcOrderDesignPad",true);
  var pad=(UserControl)FormatterServices.GetUninitializedObject(type);typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(pad,new object[0]);
  using(pad){Call(pad,"InitializeComponent");Call(pad,"InitOrderItemTable");Call(pad,"InitOrderEditor");
   type.GetField("m_sSelectedCustCode",F).SetValue(pad,"TEST");Set(Field(pad,"tbOrderName"),"Text","   ");
   var combo=Field(pad,"cbItem");Get(combo,"Items").GetType().GetMethod("AddText",new[]{typeof(string)}).Invoke(Get(combo,"Items"),new object[]{"테스트 제품"});Set(combo,"Enabled",true);
   foreach(var scenario in new[]{new object[]{0,"","제품을 선택해주세요"},new object[]{0,"입력한 이름","제품을 선택해주세요"},new object[]{1,"","주문이름을 입력해주세요"},new object[]{1,"   ","주문이름을 입력해주세요"}}) {
    Set(combo,"SelectedIndex",scenario[0]);Set(Field(pad,"tbOrderName"),"Text",scenario[1]);string expected=(string)scenario[2];bool seen=false;
    using(var timer=new Timer{Interval=50}){
     timer.Tick+=delegate{EnumThreadWindows(GetCurrentThreadId(),delegate(IntPtr w,IntPtr d){bool match=false;
      EnumChildWindows(w,delegate(IntPtr c,IntPtr q){var text=new StringBuilder(256);GetWindowText(c,text,256);if(text.ToString()==expected)match=true;return true;},IntPtr.Zero);
      var title=new StringBuilder(256);GetWindowText(w,title,256);
      if(title.ToString()=="주문정보"){seen=match;SendMessage(w,0x0010,IntPtr.Zero,IntPtr.Zero);}return true;},IntPtr.Zero);};
     timer.Start();Check(!(bool)Call(pad,"CheckValidation"),"invalid product/name blocks save");timer.Stop();
    }
    Check(seen,"exact validation message: "+expected);
   }
   Set(Field(pad,"tbOrderName"),"Text","부속품 없는 주문");
   Check((bool)Call(pad,"CheckValidation"),"zero accessories pass validation");
   var save=Call(pad,"CreateOrderSaveData");Check((string)Get(save,"OrderTitle")=="부속품 없는 주문","header preserves required name");
   Check(((IList)Get(save,"Items")).Count==0,"save model has explicitly empty accessory list");
   var resolve=app.GetType("PineTree.Classes.OrderRepository").GetMethod("GetOrderItems",BindingFlags.Static|BindingFlags.NonPublic);
   Check(((IList)resolve.Invoke(null,new[]{save})).Count==0,"repository does not create a phantom legacy item");
   Set(save,"Items",null);Check(((IList)resolve.Invoke(null,new[]{save})).Count==1,"legacy null-list contract remains supported");
  }
  Console.WriteLine("PASS: required order name / optional accessories / save model: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
