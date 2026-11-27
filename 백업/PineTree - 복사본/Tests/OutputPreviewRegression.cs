using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
internal static class OutputPreviewRegression
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    private static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    private static void Check(bool v,string message){if(!v)throw new Exception(message);checks++;}
    private static async Task WaitUntil(Func<bool> ready){var clock=Stopwatch.StartNew();while(!ready()){if(clock.Elapsed.TotalSeconds>180)throw new TimeoutException();await Task.Delay(20);}}
    private static string Hash(string file){using(var sha=SHA256.Create())using(var input=File.OpenRead(file))return Convert.ToBase64String(sha.ComputeHash(input));}
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool GetKeyboardState(byte[] state);
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool SetKeyboardState(byte[] state);
    private static HandledMouseEventArgs Wheel(Control target,int delta,Point p){
        var original=new byte[256];GetKeyboardState(original);
        var state=(byte[])original.Clone();state[0x11]=0x80;state[0xA2]=0x80;SetKeyboardState(state);
        try { return PlainWheel(target,delta,p); }
        finally { SetKeyboardState(original); }
    }
    private static HandledMouseEventArgs PlainWheel(Control target,int delta,Point p){var e=new HandledMouseEventArgs(MouseButtons.None,0,p.X,p.Y,delta);target.GetType().GetMethod("OnMouseWheel",F).Invoke(target,new object[]{e});return e;}
    private static double Zoom(object o){return (double)Field(o,"_zoom");}
    private static async Task Save(object o,string path){await (Task)Call(o,"SavePdfToPathAsync",path);}
    private static async Task Run(Form viewer,string source,string folder)
    {
        var picture=(PictureBox)Field(viewer,"_picture");var viewport=(Panel)Field(viewer,"_viewport");var save=(ToolStripButton)Field(viewer,"_save");
        await WaitUntil(()=>picture.Image!=null || ((Label)Field(viewer,"_status")).Text.Contains("실패"));
        Check(picture.Image!=null,"real output PDF rendered");
        Check(viewer.Text=="출력 미리보기","output title exact");
        Check(save.Owner!=null && save.Visible && save.Enabled && save.Text=="파일 저장","save button visible");
        Call(viewer,"Fit");double fit=Zoom(viewer);int version=(int)Field(viewer,"_version");
        Point center=new Point(viewport.ClientSize.Width/2,viewport.ClientSize.Height/2);
        PlainWheel(viewport,-120,center);
        Check(Zoom(viewer)==fit,"plain wheel does not zoom");
        var wheel=Wheel(viewport,120,center);
        Check(wheel.Handled && Math.Abs(Zoom(viewer)-fit*1.08)<.000001,"viewport wheel zooms slowly by eight percent");
        Wheel(viewport,-120,center);Check(Math.Abs(Zoom(viewer)-fit)<.000001,"wheel down reverses zoom");
        double before=Zoom(viewer);Wheel(viewport,15,center);Check(Zoom(viewer)>before && Zoom(viewer)<before*1.02,"small trackpad delta has a small step");
        Call(viewer,"SetZoom",1.5);
        Point anchor=new Point(300,200);double relativeX=(anchor.X-picture.Left)/(double)picture.Width, relativeY=(anchor.Y-picture.Top)/(double)picture.Height;
        Wheel(viewport,120,anchor);
        Check(Math.Abs((anchor.X-picture.Left)/(double)picture.Width-relativeX)<.003 && Math.Abs((anchor.Y-picture.Top)/(double)picture.Height-relativeY)<.003,"wheel retains image point under cursor");
        before=Zoom(viewer);Wheel(picture,120,new Point(picture.Width/2,picture.Height/2));Check(Math.Abs(Zoom(viewer)-before*1.08)<.000001,"wheel over picture also zooms");
        Check((int)Field(viewer,"_version")==version,"wheel uses cached image without PDF regeneration");
        Call(viewer,"SetZoom",3d);
        await WaitUntil(()=>Field(viewer,"_detailImage")!=null || ((Label)Field(viewer,"_status")).Text.Contains("실패"));
        var detail=(Image)Field(viewer,"_detailImage");
        var detailBounds=(Rectangle)Field(viewer,"_detailBounds");
        Check(detail!=null && detail.Width>=detailBounds.Width && detail.Height>=detailBounds.Height,"zoom rerenders visible PDF region at screen resolution");
        Check(picture.Image!=null && (int)Field(viewer,"_version")==version,"detail rendering preserves overview and zoom state");
        // Start one real render, then move repeatedly while it is running.
        Call(viewer,"SetZoom",2.5d);Call(viewer,"RenderDetail");
        var running=(CancellationTokenSource)Field(viewer,"_detailRender");
        Check(running!=null,"detail worker started");
        for(int i=0;i<8;i++) Call(viewer,"SetZoom",2.6+i*.03);
        Check(object.ReferenceEquals(running,Field(viewer,"_detailRender")) && !running.IsCancellationRequested,"rapid zoom keeps one worker without cancellation exceptions");
        await WaitUntil(()=>Field(viewer,"_detailImage")!=null && Field(viewer,"_detailRender")==null);
        Check(!((Label)Field(viewer,"_status")).Text.Contains("실패"),"latest zoom rendered after obsolete result discarded");
        int previousY=viewport.AutoScrollPosition.Y;double zoomBeforeScroll=Zoom(viewer);
        PlainWheel(viewport,-120,new Point(50,50));
        Check(Zoom(viewer)==zoomBeforeScroll && viewport.AutoScrollPosition.Y<previousY,"plain wheel scrolls vertically without zoom");
        await WaitUntil(()=>Field(viewer,"_detailImage")!=null && Field(viewer,"_detailRender")==null);
        using(var detailBitmap=new Bitmap(viewer.Width,viewer.Height)){viewer.DrawToBitmap(detailBitmap,new Rectangle(Point.Empty,detailBitmap.Size));detailBitmap.Save(Path.Combine(folder,"output-preview-detail.png"));}
        Call(viewer,"SetZoom",100d);Check(Zoom(viewer)==5,"upper zoom bound");
        Call(viewer,"SetZoom",.001d);Check(Zoom(viewer)==.05 && picture.Width>0 && picture.Height>0,"lower zoom bound");
        Call(viewer,"Fit");Check(picture.Dock==DockStyle.Fill && viewport.AutoScrollMinSize==Size.Empty && viewport.AutoScrollPosition==Point.Empty,"fit restores centered full view");
        await CheckQuietCancellation(viewer,source);
        string original=Hash(source);string target=Path.Combine(folder,"saved-output.pdf");
        var saving=(Task)Call(viewer,"SavePdfToPathAsync",target);
        Check(!(bool)save.Enabled && (bool)Field(viewer,"_saving"),"save button disabled during copy");await saving;
        Check(Hash(target)==original && Hash(source)==original,"saved PDF is byte-identical and source untouched");
        Check(save.Enabled && !(bool)Field(viewer,"_saving") && (string)Field(viewer,"_lastSavedPath")==Path.GetFullPath(target),"save state restored");
        File.WriteAllText(target,"replace this test file");await Save(viewer,target);Check(Hash(target)==original,"overwrite replaces target with original PDF");
        bool rejected=false;try{await Save(viewer,source);}catch(ArgumentException){rejected=true;}Check(rejected && Hash(source)==original,"source cannot overwrite itself");
        rejected=false;try{await Save(viewer,Path.Combine(folder,"wrong.txt"));}catch(ArgumentException){rejected=true;}Check(rejected && save.Enabled,"invalid extension rejected and button restored");
        using(var cancel=new CancellationTokenSource()){
            cancel.Cancel();rejected=false;
            try{viewer.GetType().GetMethod("CopyPdfFile",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{source,target,cancel.Token});}
            catch(TargetInvocationException e){rejected=e.InnerException is OperationCanceledException;}
            Check(rejected && Hash(target)==original,"cancelled save preserves existing destination");
        }
        Check(Directory.GetFiles(folder,".pinetree-save-*.tmp").Length==0,"no partial PDF staging files remain");
        using(var bitmap=new Bitmap(viewer.Width,viewer.Height)){viewer.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"output-preview.png"));}
        using(var originalViewer=(Form)Activator.CreateInstance(viewer.GetType(),new object[]{source})){
            Check(originalViewer.Text.StartsWith("파일 미리보기 · ") && ((ToolStripButton)Field(originalViewer,"_save")).Owner==null,"original source preview keeps its own title/context");
        }
        viewer.Close();Check(File.Exists(target) && Hash(target)==original,"saved PDF survives closing preview");
        Console.WriteLine("PASS: output preview title/wheel/save checks: "+checks);
    }
    private static async Task CheckQuietCancellation(Form owner,string source)
    {
        Type service=null;
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if(assembly.GetName().Name=="PineTree.PdfEngine") service=assembly.GetType("PineTree.PdfEngine.OrderDocumentService");
        var preview=service.GetMethod("PreviewRegionAsync");
        int exceptions=0;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> watch=delegate(object sender,System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) {
            if(e.Exception is OperationCanceledException) Interlocked.Increment(ref exceptions);
        };
        AppDomain.CurrentDomain.FirstChanceException+=watch;
        try {
            using(var cancel=new CancellationTokenSource()) {
                cancel.Cancel();
                var task=(Task)preview.Invoke(null,new object[]{source,0,3000,false,new RectangleF(0,0,1,1),cancel.Token});await task;
                Check(task.Status==TaskStatus.RanToCompletion && (bool)task.GetType().GetProperty("Result").GetValue(task,null).GetType().GetProperty("IsCanceled").GetValue(task.GetType().GetProperty("Result").GetValue(task,null),null),"pre-cancelled viewport returns cancellation result without an exception");
            }
            using(var cancel=new CancellationTokenSource()) {
                var task=(Task)preview.Invoke(null,new object[]{source,0,5000,false,new RectangleF(0,0,1,1),cancel.Token});
                await Task.Delay(80);cancel.Cancel();await task;
                var result=task.GetType().GetProperty("Result").GetValue(task,null);
                Check((bool)result.GetType().GetProperty("IsCanceled").GetValue(result,null),"in-flight viewport stops quietly");
            }
            using(var child=(Form)Activator.CreateInstance(owner.GetType(),new object[]{source,true})) {
                child.StartPosition=FormStartPosition.Manual;child.Location=new Point(-4000,-4000);child.ShowInTaskbar=false;child.Show(owner);
                await WaitUntil(()=>((PictureBox)Field(child,"_picture")).Image!=null && Field(child,"_render")==null);
                Call(child,"SetZoom",3d);Call(child,"RenderDetail");
                Check(Field(child,"_detailRender")!=null,"close test has an active region worker");
                await Task.Delay(80);child.Close();
                await WaitUntil(()=>Field(child,"_detailRender")==null);
                Check(child.IsDisposed,"closing preview releases active region worker");
            }
            Check(exceptions==0,"no first-chance cancellation exception during region cancel or preview close");
        }
        finally { AppDomain.CurrentDomain.FirstChanceException-=watch; }
    }
    [STAThread]private static int Main(string[] args)
    {
        int exit=0;
        try{
            Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            string assemblyPath=Path.GetFullPath(args[0]), source=Path.GetFullPath(args[1]), folder=Path.GetFullPath(args[2]);Directory.CreateDirectory(folder);
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assemblyPath),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
            var type=Assembly.LoadFrom(assemblyPath).GetType("PineTree.Dialogs.DialogDocumentPreview",true);
            using(var viewer=(Form)Activator.CreateInstance(type,new object[]{source,true})){
                viewer.StartPosition=FormStartPosition.Manual;viewer.Left=-4000;viewer.Top=-4000;viewer.ShowInTaskbar=false;
                viewer.Shown+=async delegate{try{await Run(viewer,source,folder);}catch(Exception e){Console.Error.WriteLine(e);exit=1;viewer.Close();}};
                Application.Run(viewer);
            }
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}return exit;
    }
}
