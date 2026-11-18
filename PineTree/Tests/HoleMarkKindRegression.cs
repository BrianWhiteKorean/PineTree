using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Collections;
class HoleMarkKindRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 [STAThread]static int Main(string[] args){try{
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
  var settingType=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
  Application.EnableVisualStyles();
  foreach(string type in new[]{"UcOrder수성_그룹A","UcOrder페트"})
  using(var host=new Form { ShowInTaskbar=false, StartPosition=FormStartPosition.Manual, Left=-4000, Top=-4000, Width=1100, Height=800 })
  using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder."+type,true))){
   host.Controls.Add(editor);host.Show();
   Set(Field(editor,"tbTargetWidth"),"Value",600m); Set(Field(editor,"tbTargetHeight"),"Value",1500m);
   if(type=="UcOrder페트") {
    bool opened=false;
    foreach(var option in (IEnumerable)Get(Field(editor,"cbWork"),"Items"))
     if((string)Get(option,"Text")=="타공") Set(Field(editor,"cbWork"),"SelectedOption",option);
    Check((string)Get(Get(Field(editor,"cbWork"),"SelectedOption"),"Text")=="타공","PET hole option selected");
    using(var timer=new System.Windows.Forms.Timer { Interval=50 }) {
     timer.Tick+=delegate {
      foreach(Form dialog in Application.OpenForms) {
       if(dialog.GetType().Name!="Dlg후가공공통_타공마크") continue;
       timer.Stop();opened=true;
       dialog.DialogResult=DialogResult.Cancel;dialog.Close();break;
      }
     };
     timer.Start();
     Call(editor,"AddPostProcess");timer.Stop();
    }
    Check(opened,"PET Add opens shared hole dialog");
    Check(((IList)Field(editor,"_postProcesses")).Count==0,"cancel leaves finishing list unchanged");
   }
   var hole=settingType.GetMethod("Create").Invoke(null,new object[]{"HOLE"});
   foreach(int index in new[]{1,0}){
    object accepted;
    using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",hole)){
     Check(dialog.GetType().Name=="Dlg후가공공통_타공마크","shared hole dialog used");
     Check((int)Get(Field(dialog,"cbHoleKind"),"SelectedIndex")== (index==1?0:1),"selected kind restored");
     Set(Field(dialog,"cbHoleKind"),"SelectedIndex",index);
     dialog.Shown+=delegate {var b=Field(dialog,"btnOK");b.GetType().GetMethod("PerformClick").Invoke(b,null);};
     Check(dialog.ShowDialog()==DialogResult.OK,"dialog accepts kind");accepted=Get(dialog,"Setting");
    }
    string kind=index==1?"CROSS":"DOT";
    Check((string)Get(Get(accepted,"Parameters"),"HoleMarkKind")==kind,"dialog setting kind");
    Call(editor,"CommitPostProcess",index==1?-1:0,accepted);
    Check((string)Get(Call(editor,"ReadPdfGenerationSettings"),"HoleMarkKind")==kind,"kind reaches engine");
    var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
    string json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);
    var decoded=state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
    using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())){
     restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{decoded});
     var items=(IList)Field(restored,"_postProcesses");
     Check((string)Get(Get(items[0],"Parameters"),"HoleMarkKind")==kind,"kind JSON roundtrip");
    }
    hole=accepted;
   }
  }
  Console.WriteLine("PASS "+checks+" hole kind UI/state/engine checks");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
