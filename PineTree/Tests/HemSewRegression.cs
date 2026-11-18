using System;
using System.Collections;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
internal static class HemSewRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F|BindingFlags.Public).Invoke(o,a);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 static void Mode(object o,string n){o.GetType().GetField("_updatingInputs",F).SetValue(o,true);Set(Field(o,"cbZoomType"),"SelectedOption",Field(o,n));o.GetType().GetField("_updatingInputs",F).SetValue(o,false);Call(o,"ApplyZoomDimensions");Call(o,"UpdateDocumentState");}
 [STAThread]static int Main(string[] args){try{
  Application.EnableVisualStyles();var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var st=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
  var setting=st.GetMethod("Create").Invoke(null,new object[]{"HEM_SEW"});
  Check((decimal)Get(Get(setting,"Parameters"),"DimensionMm")==15,"new setting defaults to 15mm");
  var dialogType=app.GetType("PineTree.UserControlsOrder.Dlg현수막_사방시접미싱",true);
  using(var d=(Form)Activator.CreateInstance(dialogType,new[]{setting})) {
   Check((decimal)Get(Field(d,"tbLength"),"Value")==15 && (bool)Get(Field(d,"tbLength"),"ReadOnly"),"single displays read-only 15mm");
   Set(Field(d,"cbType"),"SelectedIndex",1);Check((decimal)Get(Field(d,"tbLength"),"Value")==30,"double automatically displays 30mm");
   d.ShowInTaskbar=false;d.StartPosition=FormStartPosition.Manual;d.Left=-4000;d.Show();Application.DoEvents();if(args.Length>1){using(var bitmap=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(bitmap,d.ClientRectangle);bitmap.Save(Path.GetFullPath(args[1]));}}Field(d,"btnOK").GetType().GetMethod("PerformClick").Invoke(Field(d,"btnOK"),null);
   setting=Get(d,"Setting");Check(setting!=null&&(decimal)Get(Get(setting,"Parameters"),"DimensionMm")==30,"dialog saves double 30mm");
  }
  using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder.UcOrder수성_그룹A",true))) {
   editor.GetType().GetField("_sourceWidthMm",F).SetValue(editor,180m);editor.GetType().GetField("_sourceHeightMm",F).SetValue(editor,120m);
   Mode(editor,"inputOption7");Call(editor,"CommitPostProcess",-1,setting);
   var pdf=Call(editor,"ReadPdfGenerationSettings");var hems=(IList)Get(pdf,"HemSewPasses");
   Check(hems.Count==1 && (double)Get(hems[0],"MarginMm")==30 && (double)Get(hems[0],"LineThicknessMm")==.6,"10x uses 30mm margins and .6mm stroke");
   Check((decimal)Call(editor,"FinishedWidthMm")==1860 && (decimal)Call(editor,"FinishedHeightMm")==1260,"double final dimensions");
   pdf.GetType().GetMethod("Validate").Invoke(pdf,null);
   Check((double)Call(pdf,"FinishedWidthMm")==1860,"engine and UI width agree");
   var summary=(string)st.GetMethod("Summary").Invoke(setting,new object[]{1800m,1200m});Check(!summary.Contains("반영 대기")&&summary.Contains("30"),"summary reflects active PDF operation");
   var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);var json=state.GetType().GetMethod("ToJson").Invoke(state,null);var restored=state.GetType().GetMethod("FromJson").Invoke(null,new[]{json});
   using(var other=(UserControl)Activator.CreateInstance(editor.GetType())){
    other.GetType().GetMethod("RestoreEditorState").Invoke(other,new[]{restored});var item=((IList)Field(other,"_postProcesses"))[0];
    using(var d=(Form)Call(other,"CreatePostProcessDialog",item))Check((decimal)Get(Field(d,"tbLength"),"Value")==30&&(int)Get(Field(d,"cbType"),"SelectedIndex")==1,"state restores type and displayed margin");
   }
   Mode(editor,"inputOption6");pdf=Call(editor,"ReadPdfGenerationSettings");hems=(IList)Get(pdf,"HemSewPasses");
   Check((double)Get(hems[0],"LineThicknessMm")==.06 && (decimal)Call(editor,"FinishedWidthMm")==240,"1:1 keeps 30mm margin and .06mm stroke");
   Mode(editor,"inputOption13");Set(Field(editor,"tbTargetWidth"),"Value",450m);pdf=Call(editor,"ReadPdfGenerationSettings");hems=(IList)Get(pdf,"HemSewPasses");
   Check((double)Get(hems[0],"LineThicknessMm")==.15 && (double)Get(hems[0],"MarginMm")==30,"fixed width 2.5x changes thickness only");
   var single=st.GetMethod("Create").Invoke(null,new object[]{"HEM_SEW"});Call(editor,"CommitPostProcess",0,single);Mode(editor,"inputOption7");
   Check((decimal)Call(editor,"FinishedWidthMm")==1830&&(decimal)Call(editor,"FinishedHeightMm")==1230,"single sample dimensions 1830x1230");
  }
  Console.WriteLine("PASS: hem type/defaults/scale/state/UI/PDF settings: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
