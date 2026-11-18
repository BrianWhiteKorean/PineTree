using System;
using System.Drawing;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

// Live server integration; creates fixture files only, never saves or changes an order.
internal static class BannerServerFilesRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static object Field(object o,string n) { return o.GetType().GetField(n,Flags).GetValue(o); }
    private static object Get(object o,string n) { return o.GetType().GetProperty(n).GetValue(o,null); }
    private static void Set(object o,string n,object v) { o.GetType().GetProperty(n).SetValue(o,v,null); }
    private static object Call(object o,string n,params object[] args) { return o.GetType().GetMethod(n,Flags).Invoke(o,args); }
    private static void Num(object o,string n,decimal v) { Set(Field(o,n),"Value",v); }
    private static decimal Value(object o,string n) { return Convert.ToDecimal(Get(Field(o,n),"Value")); }
    private static void Check(bool v,string message) { if(!v)throw new Exception(message);checks++; }
    private static object Capture(object o) { return o.GetType().GetMethod("CaptureEditorState").Invoke(o,null); }
    private static object Decode(object state) {
        var json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);
        return state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
    }
    private static async Task Idle(object editor) {
        var end=DateTime.UtcNow.AddMinutes(4);
        do { await Task.Delay(30); if(DateTime.UtcNow>end)throw new TimeoutException("Editor did not finish processing"); }
        while((bool)Get(editor,"IsProcessing"));
    }
    private static async Task GridRestore(Form host,Assembly app,object saved)
    {
        var dialogType=app.GetType("PineTree.UserControls.UcOrderDesigner",true);
        var dialog=(UserControl)FormatterServices.GetUninitializedObject(dialogType);
        typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(dialog,new object[0]);
        using(dialog) using(var dialogHost=new Form { ShowInTaskbar=false, StartPosition=FormStartPosition.Manual, Left=-4000, Top=-4000 }) {
            Call(dialog,"InitializeComponent");Call(dialog,"InitOrderItemTable");Call(dialog,"InitOrderEditor");Call(dialog,"InitPreviewEvent");
            var item=Activator.CreateInstance(app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem",true));
            Set(item,"PriceItemSeqNo",101L);Set(item,"PriceItemName","Server restore fixture");Set(item,"UnitName","회배");Set(item,"BillingBasis","면적");Set(item,"UnitPrice",4000m);
            Call(dialog,"AddOrderItem",item,null);
            var table=(DataTable)Field(dialog,"m_orderItems");var row=table.Rows[0];
            row["CategoryName"]="현수막";row["WidthMm"]=600m;row["HeightMm"]=1800m;row["Qty"]=5m;
            row["SupplyAmount"]=12345m;row["VatAmount"]=1234m;row["TotalAmount"]=13579m;
            row["RequestFilePath"]=Get(saved,"SourceFilePath");
            row["EditorStateJson"]=saved.GetType().GetMethod("ToJson").Invoke(saved,null);
            var grid=Field(dialog,"FGrid1");Set(grid,"Row",Get(Get(grid,"Rows"),"Fixed"));Call(dialog,"UpdateSelectedOrderEditor");
            dialogHost.ClientSize=dialog.Size;dialogHost.Controls.Add(dialog);dialogHost.Show(host);
            var editor=(UserControl)Field(dialog,"m_orderEditor");await Idle(editor);
            Check(Field(editor,"_sourceDetails")!=null && ((PictureBox)Field(editor,"pictureBox1")).Image!=null,"order grid displays source restored from server");
            Check((decimal)row["TotalAmount"]==13579m && Value(editor,"inputNumericBox4")==5,"order grid restoration keeps saved amount and quantity");
            var items=(System.Collections.IList)Call(dialog,"CreateOrderItemSaveDataList");
            var persisted=saved.GetType().GetMethod("FromJson").Invoke(null,new[]{Get(items[0],"EditorStateJson")});
            Check(Get(persisted,"SourceServerFile")!=null && Get(persisted,"OutputServerFile")!=null,"order save data includes both server file references");
            Check((string)Get(items[0],"RequestFilePath")== (string)Get(saved,"SourceFilePath"),"order save keeps original path rather than temporary download path");
            dialogHost.Close();
        }
    }
    private static async Task Run(Form host,Assembly app,string folder)
    {
        Directory.CreateDirectory(folder);
        string sourcePath=Path.Combine(folder,"server-source.png");
        using(var image=new Bitmap(300,600)) {
            image.SetResolution(150,150);
            using(var graphics=Graphics.FromImage(image)) { graphics.Clear(Color.Gold);graphics.FillRectangle(Brushes.Teal,20,40,180,450); }
            image.Save(sourcePath,System.Drawing.Imaging.ImageFormat.Png);
        }
        byte[] original=File.ReadAllBytes(sourcePath);
        var type=app.GetType("PineTree.UserControlsOrder.UcOrder수성_그룹A",true);
        object saved;
        using(var editor=(UserControl)Activator.CreateInstance(type)) {
            editor.Dock=DockStyle.Fill;host.Controls.Add(editor);
            await (Task)Call(editor,"LoadSourceFileAsync",sourcePath);
            Check(Field(editor,"_sourceDetails")!=null,"fixture inspected");
            Num(editor,"tbTargetWidth",600);Num(editor,"tbTargetHeight",1800);Num(editor,"inputNumericBox4",5);
            Num(editor,"tbEdgeBottom",9);
            var hole=app.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null,new object[]{"HOLE"});
            Call(editor,"CommitPostProcess",-1,hole);
            var settings=Call(editor,"ReadPdfGenerationSettings");
            var generation=(Task)Call(editor,"GenerateAndUploadPdfAsync",Path.Combine(folder,"server-output.pdf"),settings);
            Check((bool)Get(editor,"IsProcessing"),"generation/upload locks editor and order save");
            await generation;
            saved=Capture(editor);
            Check(Get(saved,"SourceServerFile")!=null && Get(saved,"OutputServerFile")!=null,"original and generated PDF uploaded");
            Check((string)Get(Get(saved,"SourceServerFile"),"OriginalFileName")=="server-source.png","original filename retained");
            var storage=app.GetType("PineTree.Classes.OrderFileStorageService");
            var download=(Task)storage.GetMethod("DownloadAsync").Invoke(null,new[]{Get(saved,"SourceServerFile"),(object)System.Threading.CancellationToken.None});
            await download;
            string downloaded=(string)Get(download,"Result");
            Check(Convert.ToBase64String(File.ReadAllBytes(downloaded))==Convert.ToBase64String(original),"server round-trip preserves exact source bytes");
            File.Delete(downloaded);Directory.Delete(Path.GetDirectoryName(downloaded));
            saved=Decode(saved);
            Check(Get(saved,"SourceServerFile")!=null && Get(saved,"OutputServerFile")!=null,"server references survive JSON round-trip");
            host.Controls.Remove(editor);
        }
        File.Delete(sourcePath);
        await GridRestore(host,app,saved);
        using(var restored=(UserControl)Activator.CreateInstance(type)) {
            restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{saved});
            int valueEvents=0;restored.GetType().GetEvent("OrderValuesChanged").AddEventHandler(restored,new EventHandler(delegate { valueEvents++; }));
            restored.Dock=DockStyle.Fill;host.Controls.Add(restored);await Idle(restored);
            Check(Field(restored,"_sourceDetails")!=null && ((PictureBox)Field(restored,"pictureBox1")).Image!=null,"missing local source restored and analyzed from server");
            Check(Value(restored,"tbTargetWidth")==600 && Value(restored,"tbTargetHeight")==1800 && Value(restored,"inputNumericBox4")==5,"server analysis preserves saved target size and quantity");
            Check(Value(restored,"tbEdgeBottom")==9 && ((System.Collections.IList)Field(restored,"_postProcesses")).Count==1,"server analysis preserves bleed and finishing");
            Check(valueEvents==0,"server restoration does not recalculate saved order amounts");
            Check((string)Get(Capture(restored),"SourceFilePath")==sourcePath,"temporary download path does not overwrite saved source reference");
            Check((bool)Get(Field(restored,"btnPreview"),"Enabled"),"restored source can generate preview");
            Num(restored,"tbTargetWidth",700);
            Check(Get(Capture(restored),"OutputServerFile")==null && Get(Capture(restored),"SourceServerFile")!=null,"editing invalidates stale output but keeps server source");
            File.WriteAllBytes(sourcePath,original);
            await (Task)Call(restored,"LoadSourceFileAsync",sourcePath);
            Check(Get(Capture(restored),"SourceServerFile")==null,"new drop clears previous server source reference");
            host.Controls.Remove(restored);
        }
        var unavailable=Decode(saved);
        var missing=Get(unavailable,"SourceServerFile");
        Set(missing,"Sha256",new string('0',64));Set(missing,"FileId",new string('0',64)+".png");
        using(var restored=(UserControl)Activator.CreateInstance(type)) {
            restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{unavailable});
            restored.Dock=DockStyle.Fill;host.Controls.Add(restored);await Idle(restored);
            Check(Field(restored,"_sourceDetails")==null && !(bool)Get(Field(restored,"btnPreview"),"Enabled"),"unavailable server file never silently uses a different local file");
            Check(Value(restored,"tbTargetWidth")==600 && Value(restored,"inputNumericBox4")==5,"download failure retains saved edit values");
            Check(Get(Field(restored,"tbFileInfo"),"Text").ToString().Contains("404"),"download failure displayed in file information");
            host.Controls.Remove(restored);
        }
        Console.WriteLine("PASS: live server/upload/download/editor restoration checks: "+checks);
    }
    [STAThread] private static int Main(string[] args)
    {
        int exit=0;
        try {
            Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var path=Path.GetFullPath(args[0]);
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
            var app=Assembly.LoadFrom(path);
            using(var host=new Form { Width=1050,Height=780,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-4000,Top=-4000 }) {
                host.Shown+=async delegate { try { await Run(host,app,Path.GetFullPath(args[1])); } catch(Exception ex) { Console.Error.WriteLine(ex);exit=1; } finally { host.Close(); } };
                Application.Run(host);
            }
        } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
        return exit;
    }
}
