using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
internal static class HoleColorRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Check(bool b,string msg){if(!b)throw new Exception(msg);checks++;}
 static Color Picker(object d,string n){return (Color)Get(Field(d,n),"Color");}
 [STAThread] static int Main(string[] args){try{
  Application.EnableVisualStyles();
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
  var type=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
  var dt=app.GetType("PineTree.UserControlsOrder.Dlg후가공공통_타공마크",true);
  var initial=type.GetMethod("Create").Invoke(null,new object[]{"HOLE"});
  object saved;
  using(var d=(Form)Activator.CreateInstance(dt,new object[]{initial,100m,80m})){
   Check(Picker(d,"colorPickerHole").ToArgb()==Color.White.ToArgb(),"default white background");
   Check(Picker(d,"colorPickerMarker").ToArgb()==Color.Black.ToArgb(),"default black marker");
   Set(Field(d,"colorPickerHole"),"Color",Color.FromArgb(12,34,56));
   Set(Field(d,"colorPickerMarker"),"Color",Color.FromArgb(210,90,45));
   d.ShowInTaskbar=false;d.StartPosition=FormStartPosition.Manual;d.Left=-4000;d.Show();Application.DoEvents();
   Field(d,"btnOK").GetType().GetMethod("PerformClick").Invoke(Field(d,"btnOK"),null);
   saved=Get(d,"Setting");Check(saved!=null,"OK saves colors");
   Check((string)Get(Get(saved,"Parameters"),"HoleBackgroundColor")=="#0C2238","background stored as hex");
   Check((string)Get(Get(saved,"Parameters"),"HoleMarkerColor")=="#D25A2D","marker stored as hex");
   Check((string)Get(Get(initial,"Parameters"),"HoleBackgroundColor")=="#FFFFFF","editing leaves original setting intact");
  }
  foreach(var name in new[]{"UcOrder수성_그룹A","UcOrder수성_그룹B"}) {
   using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder."+name,true))) {
    Set(Field(editor,"tbTargetWidth"),"Value",100m);Set(Field(editor,"tbTargetHeight"),"Value",80m);
    Call(editor,"CommitPostProcess",-1,saved);
    var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
    var json=state.GetType().GetMethod("ToJson").Invoke(state,null);
    var decoded=state.GetType().GetMethod("FromJson").Invoke(null,new[]{json});
    using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())) {
     restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{decoded});
     Set(Field(restored,"cbZoomType"),"SelectedOption",Field(restored,"inputOption6"));
     Set(Field(restored,"tbTargetWidth"),"Value",100m);Set(Field(restored,"tbTargetHeight"),"Value",80m);
     var item=((IList)Field(restored,"_postProcesses"))[0];
     using(var d=(Form)Call(restored,"CreatePostProcessDialog",item)) {
      Check(Picker(d,"colorPickerHole").ToArgb()==Color.FromArgb(12,34,56).ToArgb(),name+" reopens background");
      Check(Picker(d,"colorPickerMarker").ToArgb()==Color.FromArgb(210,90,45).ToArgb(),name+" reopens marker");
      Set(Field(d,"colorPickerHole"),"Color",Color.Yellow);
      Field(d,"btnCancel").GetType().GetMethod("PerformClick").Invoke(Field(d,"btnCancel"),null);
      Check(Get(d,"Setting")==null,"cancel discards edits");
     }
     var pdf=Call(restored,"ReadPdfGenerationSettings");
     Check((string)Get(pdf,"HoleBackgroundColor")=="#0C2238" && (string)Get(pdf,"HoleMarkerColor")=="#D25A2D",name+" sends both colors to PDF");
     pdf.GetType().GetMethod("Validate").Invoke(pdf,null);
    }
   }
  }
  var legacy=app.GetType("PineTree.Classes.BannerOrderEditorState").GetMethod("FromJson").Invoke(null,new object[]{"{\"Version\":1,\"PostProcesses\":[{\"ProcessCode\":\"HOLE\",\"Parameters\":{\"TypeCode\":\"FOUR_CORNERS\"}}]}"});
  var old=((IList)Get(legacy,"PostProcesses"))[0];
  using(var d=(Form)Activator.CreateInstance(dt,new object[]{old,100m,80m}))
   Check(Picker(d,"colorPickerHole").ToArgb()==Color.White.ToArgb()&&Picker(d,"colorPickerMarker").ToArgb()==Color.Black.ToArgb(),"legacy missing colors use defaults");
  Console.WriteLine("PASS: hole color UI/defaults/cancel/state/banner/PET/PDF settings: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
