using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
internal static class BannerEditorStateRegression
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Finish(object editor,string code) { return editor.GetType().Assembly.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null,new object[]{code}); }
    private static int checks;
    private static object Field(object o,string n){return o.GetType().GetField(n,Flags).GetValue(o);}
    private static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
    private static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
    private static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,Flags).Invoke(o,a);}
    private static void Number(object o,string n,decimal v){Set(Field(o,n),"Value",v);}
    private static decimal NumberValue(object o,string n){return Convert.ToDecimal(Get(Field(o,n),"Value"));}
    private static object Capture(object o){return o.GetType().GetMethod("CaptureEditorState").Invoke(o,null);}
    private static void Restore(object o,object state){o.GetType().GetMethod("RestoreEditorState").Invoke(o,new[]{state});}
    private static object SortJson(object value) {
        var map=value as System.Collections.Generic.Dictionary<string,object>;
        if(map!=null){var sorted=new System.Collections.Generic.SortedDictionary<string,object>();foreach(var pair in map)sorted.Add(pair.Key,SortJson(pair.Value));return sorted;}
        var array=value as object[];return array==null?value:Array.ConvertAll(array,SortJson);
    }
    private static string Json(object state){var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();return serializer.Serialize(SortJson(serializer.DeserializeObject((string)state.GetType().GetMethod("ToJson").Invoke(state,null))));}
    private static void Check(bool v,string message){if(!v)throw new Exception(message);checks++;}
    private static async Task WaitUntil(Func<bool> ready){var clock=Stopwatch.StartNew();while(!ready()){if(clock.Elapsed.TotalSeconds>180)throw new TimeoutException();await Task.Delay(20);}}
    [STAThread] private static int Main(string[] args)
    {
        int exit=0;
        try{
            Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
            var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=app.GetType("PineTree.UserControlsOrder.UcOrder현수막",true);
            string folder=Path.GetFullPath(args[1]);
            using(var host=new Form{Width=1050,Height=780,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-4000,Top=-4000})
            using(var source=(UserControl)Activator.CreateInstance(type)){
                source.Dock=DockStyle.Fill;host.Controls.Add(source);
                host.Shown+=async delegate{
                    try{
                        await (Task)Call(source,"LoadSourceFileAsync",Path.Combine(folder,"source.png"));
                        Check(Field(source,"_sourceDetails")!=null,"source inspection succeeded");
                        Number(source,"tbTargetWidth",600);Number(source,"tbTargetHeight",1800);Number(source,"inputNumericBox4",5);
                        Number(source,"tbEdgeBottom",9);Set(Field(source,"cbDeploy"),"SelectedOption",Field(source,"inputOption5"));
                        var hole=Finish(source,"HOLE");Set(Get(hole,"Parameters"),"TypeCode","CUSTOM_SPACING");Set(Get(hole,"Parameters"),"HoleSpacingMm",600m);Call(source,"CommitPostProcess",-1,hole);
                        var captured=Capture(source);string originalJson=Json(captured);
                        var stateType=captured.GetType();var decoded=stateType.GetMethod("FromJson").Invoke(null,new object[]{originalJson});
                        using(var restored=(UserControl)Activator.CreateInstance(type)){
                            Restore(restored,decoded);
                            Check(Json(Capture(restored))==originalJson,"serialized settings restored before source preview");
                            host.Controls.Remove(source);restored.Dock=DockStyle.Fill;host.Controls.Add(restored);
                            await WaitUntil(()=>!(bool)Get(restored,"IsProcessing"));
                            Check(((PictureBox)Field(restored,"pictureBox1")).Image!=null,"saved source thumbnail restored");
                            Check(NumberValue(restored,"inputNumericBox4")==5 && NumberValue(restored,"tbTargetWidth")==600 && NumberValue(restored,"tbTargetHeight")==1800,"thumbnail restore does not reset quantity or target dimensions");
                            Check(NumberValue(restored,"tbEdgeBottom")==9 && (decimal)Get(Capture(restored),"HoleSpacingMm")==600,"thumbnail restore preserves finishing settings");
                            Check(Json(Capture(restored))==originalJson,"full source-backed state round-trip");
                            host.Controls.Remove(restored);
                        }
                        var missing=stateType.GetMethod("FromJson").Invoke(null,new object[]{originalJson});
                        Set(missing,"SourceFilePath",Path.Combine(folder,"missing-source-restore.png"));
                        using(var restored=(UserControl)Activator.CreateInstance(type)){
                            Restore(restored,missing);restored.Dock=DockStyle.Fill;host.Controls.Add(restored);await Task.Delay(100);
                            Check(!(bool)Get(restored,"IsProcessing") && Field(restored,"_sourceDetails")==null,"missing source does not start processing");
                            Check(NumberValue(restored,"inputNumericBox4")==5 && NumberValue(restored,"tbEdgeBottom")==9,"missing source preserves saved inputs");
                            Check(Get(Field(restored,"tbFileInfo"),"Text").ToString().Contains("원본파일을 찾을 수 없습니다"),"missing source is shown as non-blocking status");
                            Check(Json(Capture(restored))==Json(missing),"missing-source status does not modify persisted metadata");
                            host.Controls.Remove(restored);
                        }
                        Console.WriteLine("PASS: editor state/file restoration checks: "+checks);
                    }catch(Exception e){Console.Error.WriteLine(e);exit=1;}finally{host.Close();}
                };Application.Run(host);
            }
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}return exit;
    }
}
