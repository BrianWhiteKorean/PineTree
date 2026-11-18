using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class RopeSewDialogRegression
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    private static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
    private static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
    private static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    private static void Num(object o,string n,decimal v){Set(Field(o,n),"Value",v);}
    private static decimal Value(object o,string n){return Convert.ToDecimal(Get(Field(o,n),"Value"));}
    private static void Check(bool v,string message){if(!v)throw new Exception(message);checks++;}
    private static bool Rejects(Action action){try{action();return false;}catch(TargetInvocationException e){return e.InnerException is ArgumentException;}}
    [STAThread] private static int Main(string[] args)
    {
        try {
            Application.EnableVisualStyles();
            string path=Path.GetFullPath(args[0]);var app=Assembly.LoadFrom(path);
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
            var settingType=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
            var initial=settingType.GetMethod("Create").Invoke(null,new object[]{"ROPE_SEW"});
            using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder.UcOrder수성_그룹A",true)))
            using(var host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-4000,Top=-4000,Width=1100,Height=800}) {
                host.Controls.Add(editor);host.Show();
                foreach(string edge in new[]{"tbEdgeTop","tbEdgeBottom","tbEdgeLeft","tbEdgeRight"}) Check(Value(editor,edge)==0,"zero image extension default");
                editor.GetType().GetField("_sourceWidthMm",F).SetValue(editor,180m);
                editor.GetType().GetField("_sourceHeightMm",F).SetValue(editor,120m);
                Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,"inputOption7"));
                Call(editor,"UpdateDocumentState");
                using(var draft=(Form)Call(editor,"CreatePostProcessDialog",initial)) {
                    Check(draft.GetType().Name=="Dlg현수막_사방줄미싱","correct dialog opened");
                    Check((int)Get(Field(draft,"cbType"),"SelectedIndex")==0,"first sewing type selected by default");
                    var binding=app.GetType("PineTree.UserControlsOrder.PostProcessDialogBinding",true);
                    foreach(string field in new[]{"tbTop","tbBottom","tbLeft","tbRight"}) {
                        Check(Value(draft,field)==15,"default 15: "+field);
                        Set(Field(draft,field),"Text",string.Empty);
                        Check(Rejects(()=>binding.GetMethod("Read").Invoke(null,new[]{Field(draft,field)})),"required field: "+field);
                        Num(draft,field,30);
                    }
                    Set(Field(draft,"cbType"),"SelectedIndex",-1);
                    var codes=settingType.GetMethod("OptionsFor").Invoke(null,new object[]{"ROPE_SEW"});
                    Check(Rejects(()=>binding.GetMethod("Selected").Invoke(null,new[]{Field(draft,"cbType"),codes})),"sewing type required");
                }
                // Exercise the parent's real Add flow and the dialog OK callback.
                Set(Field(editor,"cbWork"),"SelectedOption",Field(editor,"inputOption10"));
                using(var timer=new Timer{Interval=50}) {
                    timer.Tick+=delegate {
                        foreach(Form form in Application.OpenForms) {
                            if(form.GetType().Name!="Dlg현수막_사방줄미싱")continue;
                            timer.Stop();Set(Field(form,"cbType"),"SelectedIndex",1);
                            Num(form,"tbTop",31);Num(form,"tbBottom",32);Num(form,"tbLeft",33);Num(form,"tbRight",34);
                            Field(form,"btnOK").GetType().GetMethod("PerformClick").Invoke(Field(form,"btnOK"),null);break;
                        }
                    };
                    timer.Start();Call(editor,"AddPostProcess");timer.Stop();
                }
                var items=(System.Collections.IList)Field(editor,"_postProcesses");
                Check(items.Count==1,"dialog result committed to parent");
                var item=items[0];var p=Get(item,"Parameters");
                Check((string)Get(p,"TypeCode")=="ROPE_8" && (decimal)Get(p,"RopeTopMm")==31 && (decimal)Get(p,"RopeBottomMm")==32 && (decimal)Get(p,"RopeLeftMm")==33 && (decimal)Get(p,"RopeRightMm")==34,"all direction values returned");
                var summary=(string)settingType.GetMethod("Summary").Invoke(item,new object[]{1800m,1200m});
                Check(summary.Contains("31") && summary.Contains("34") && !summary.Contains("반영 대기"),"parent summary shows active sewing values");
                var settings=Call(editor,"ReadPdfGenerationSettings");var passes=(System.Collections.IList)Get(settings,"RopeSewPasses");
                Check(passes.Count==1 && (double)Get(passes[0],"LeftMm")==33 && (double)Get(passes[0],"BottomMm")==32,"PDF receives individual margins");
                Check(Math.Abs((double)Get(passes[0],"LineThicknessMm")-.17*25.4/72)<1e-9,"guide thickness stays in output millimetres");
                decimal expected=1800+33+34;
                Check(Math.Abs((decimal)Call(editor,"FinishedWidthMm")-expected)<.000001m,"parent output dimensions include margins without stroke padding");
                if(args.Length>=3) {
                    foreach(string side in new[]{"TopMm","BottomMm","LeftMm","RightMm"})Set(passes[0],side,15d);
                    foreach(string side in new[]{"EdgeTopMm","EdgeBottomMm","EdgeLeftMm","EdgeRightMm"})Set(settings,side,0d);
                    string output=Path.GetFullPath(args[2]);Directory.CreateDirectory(Path.GetDirectoryName(output));
                    var service=AppDomain.CurrentDomain.GetAssemblies();Type documentService=null;
                    foreach(var assembly in service) { documentService=assembly.GetType("PineTree.PdfEngine.OrderDocumentService");if(documentService!=null)break; }
                    var task=(System.Threading.Tasks.Task)documentService.GetMethod("GenerateAsync").Invoke(null,new object[]{Path.GetFullPath(args[1]),output,settings,System.Threading.CancellationToken.None});
                    task.GetAwaiter().GetResult();
                    Check(File.Exists(output),"actual .NET service serializes rope settings and generates PDF");
                }
                var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
                var json=state.GetType().GetMethod("ToJson").Invoke(state,null);
                var decoded=state.GetType().GetMethod("FromJson").Invoke(null,new[]{json});
                using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())) {
                    restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{decoded});
                    var stored=((System.Collections.IList)Field(restored,"_postProcesses"))[0];
                    using(var dialog=(Form)Call(restored,"CreatePostProcessDialog",stored)) {
                        Check((int)Get(Field(dialog,"cbType"),"SelectedIndex")==1 && Value(dialog,"tbTop")==31 && Value(dialog,"tbBottom")==32 && Value(dialog,"tbLeft")==33 && Value(dialog,"tbRight")==34,"all values restore into dialog");
                    }
                }
                Set(p,"RopeLeftMm",(decimal?)0m);
                Check(Rejects(()=>settingType.GetMethod("Validate").Invoke(item,null)),"zero margin rejected");
                var legacy=settingType.GetMethod("Create").Invoke(null,new object[]{"ROPE_SEW"});Set(Get(legacy,"Parameters"),"DimensionMm",25m);
                using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",legacy))
                    Check(Value(dialog,"tbTop")==25 && Value(dialog,"tbRight")==25,"legacy one-value settings remain readable");
            }
            Console.WriteLine("PASS: rope sewing dialog/defaults/required/parent/state/PDF settings checks: "+checks);return 0;
        } catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
