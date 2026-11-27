using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Collections;
internal static class AlScrollRegression
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 [STAThread]static int Main(string[] args){try{
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
  Assembly.LoadFrom(Path.GetFullPath("../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll"));
  var type=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
  foreach(var name in new[]{"족자및쫄대","족자및 쫄대"})Check((string)type.GetMethod("CodeForName").Invoke(null,new object[]{name})=="AL_SCROLL","combo text mapping");
  var setting=type.GetMethod("Create").Invoke(null,new object[]{"AL_SCROLL"});
  using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder.UcOrder페트",true))){
   using(var d=(Form)Call(editor,"CreatePostProcessDialog",setting)){
    Check(d.GetType().Name=="Dlg페트_AL족자","factory opens AL dialog");
    Check(Convert.ToDecimal(Get(Field(d,"tbTop"),"Value"))==5m&&Convert.ToDecimal(Get(Field(d,"tbBottom"),"Value"))==5m,"default 5mm margins");
    Set(Field(d,"tbTop"),"Value",7m);Set(Field(d,"tbBottom"),"Value",13m);Set(Field(d,"tbLineWidth"),"Value",.25m);Set(Field(d,"tbColorName"),"Text","CutLine");
    d.StartPosition=FormStartPosition.Manual;d.Left=-4000;d.Top=-4000;d.ShowInTaskbar=false;d.Show();Application.DoEvents();((IButtonControl)Field(d,"btnOK")).PerformClick();
    setting=Get(d,"Setting");Check(setting!=null,"OK returns setting");
   }
   Call(editor,"CommitPostProcess",-1,setting);
   var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
   var json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);
   var saved=state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
   using(var other=(UserControl)Activator.CreateInstance(editor.GetType())){
    other.GetType().GetMethod("RestoreEditorState").Invoke(other,new object[]{saved});
    Set(Field(other,"tbTargetWidth"),"Value",904m);Set(Field(other,"tbTargetHeight"),"Value",1482m);
    var pdf=Call(other,"ReadPdfGenerationSettings");
    Check((bool)Get(pdf,"MakeAlScroll")&&(bool)Get(pdf,"MakeCutline"),"scroll and cut enabled");
    Check((double)Get(pdf,"ScrollTopMm")==7&&(double)Get(pdf,"ScrollBottomMm")==13,"saved margins reach PDF");
    Check((double)Get(pdf,"LineWidthMm")==.25&&(string)Get(pdf,"SpotColorName")=="CutLine","saved line and spot reach PDF");
    Check((double)Get(pdf,"CutInsetLeftMm")==2&&(double)Get(pdf,"CutInsetTopMm")==2,"default cut inset 2mm");
    Check((decimal)Call(other,"FinishedHeightMm")==1502m,"summary output height includes margins");
    pdf.GetType().GetMethod("Validate").Invoke(pdf,null);
    var list=(IList)Field(other,"_postProcesses");
    using(var d=(Form)Call(other,"CreatePostProcessDialog",list[0])){
     Check(Convert.ToDecimal(Get(Field(d,"tbTop"),"Value"))==7m&&Convert.ToDecimal(Get(Field(d,"tbBottom"),"Value"))==13m,"dialog restores margins");
     ((IButtonControl)Field(d,"btnCancel")).PerformClick();Check(Get(d,"Setting")==null,"cancel leaves state untouched");
    }
    var cut=type.GetMethod("Create").Invoke(null,new object[]{"CUT"});var p=Get(cut,"Parameters");Set(p,"CutInsetTopMm",(decimal?)3m);
    Call(other,"CommitPostProcess",-1,cut);pdf=Call(other,"ReadPdfGenerationSettings");Check((double)Get(pdf,"CutInsetTopMm")==3,"separate cut overrides inset");
    var first=list[0];list.RemoveAt(0);list.Add(first);pdf=Call(other,"ReadPdfGenerationSettings");Check((double)Get(pdf,"CutInsetTopMm")==3,"override independent of row order");
    Call(other,"RemovePostProcess",list.Count-1);pdf=Call(other,"ReadPdfGenerationSettings");Check(!(bool)Get(pdf,"MakeAlScroll"),"deleting scroll removes added margins");
   }
  }
  Console.WriteLine("PASS "+checks+" AL scroll UI/state/settings checks");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
