using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.PdfEngine;

internal static class OrderPreviewSnapshotRegression
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static string root;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); ++checks; Console.WriteLine("PASS: " + message); }
    static object Field(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static void Prop(object o, string n, object v) { o.GetType().GetProperty(n).SetValue(o, v, null); }
    static OrderSourcePage Metadata(int index) {
        var result = new OrderDocumentResult { Success = true, FileType = "AI", FileInfo = "Saved DB analysis page " + index,
            PageCount = 2, SourceWidthMm = 1000 + index, SourceHeightMm = 500, HasCutline = true, CutlineColorName = "thru" };
        return new OrderSourcePage { Details = result, FileType = result.FileType, FileInfo = result.FileInfo,
            SourceWidthMm = result.SourceWidthMm, SourceHeightMm = result.SourceHeightMm, HasCutline = true, CutlineColorName = "thru" };
    }
    static OrderServerFile UnavailableOriginal() { string hash = new string('f',64); return new OrderServerFile {
        FileId = hash + ".ai", Sha256 = hash, OriginalFileName = "unavailable-original.ai", Length = 12345 }; }
    static BannerOrderEditorState SavedEditor() { return new BannerOrderEditorState { SourceFilePath = Path.Combine(root,"unavailable-original.ai"),
        SourceServerFile = UnavailableOriginal(), SourceFileInfo = "DB saved AI file info", WidthMm = 1000, HeightMm = 500,
        Quantity = 3, ZoomType = "FIXED_SIZE", SourceRotationDegreesClockwise = 90, EdgeBottomMm = 7 }; }
    static async Task RunAsync()
    {
        OrderPreviewSnapshot snapshot;
        using (var live = new OrderSourceDocument())
        {
            Prop(live, "FilePath", Path.Combine(root,"unavailable-original.ai"));
            Prop(live, "ReferencePath", "unavailable-original.ai"); Prop(live, "FileInfo", "DB saved AI file info"); Prop(live, "PageCount", 2);
            var images = (Dictionary<int,Image>)Field(live,"_images");
            var pages = (Dictionary<int,OrderSourcePage>)Field(live,"_pages");
            for (int i=0;i<2;++i) {
                var image = new Bitmap(i==0 ? 1234 : 2222, i==0 ? 777 : 555);
                using(var g=Graphics.FromImage(image)) { g.Clear(i==0 ? Color.Red : Color.Blue); g.FillRectangle(Brushes.Lime, image.Width-20, image.Height-20,20,20); }
                images[i]=image; pages[i]=Metadata(i);
            }
            snapshot = await live.CapturePreviewSnapshotAsync(CancellationToken.None);
            Check(snapshot.Pages.Count==2, "saving uploads all artboard previews");
            Check(snapshot.Pages[0].PixelWidth==1234 && snapshot.Pages[0].PixelHeight==777 && snapshot.Pages[1].PixelWidth==2222,
                "full native bitmap dimensions retained without viewport cropping");
            Check(snapshot.Pages[0].ImageFile.FileId.EndsWith(".png") && snapshot.Pages[0].Analysis.FileInfo=="Saved DB analysis page 0", "server PNG references and analysis metadata captured");
        }
        snapshot=OrderPreviewSnapshot.FromJson(snapshot.ToJson());
        using(var cached=new OrderSourceDocument { UseSavedPreviewsOnly=true }) {
            cached.Restore(SavedEditor()); cached.SetSavedPreview(snapshot);
            Check(cached.CurrentPage!=null && cached.CurrentPage.HasCutline, "editing metadata restored immediately from DB before downloading images");
            await cached.EnsureLoadedAsync();
            Check(cached.CurrentImage!=null && cached.CurrentImage.Width==1234 && cached.CurrentImage.Height==777,"server PNG restores preview with missing original file");
            var bitmap=(Bitmap)cached.CurrentImage;
            Check(bitmap.GetPixel(0,0).R>240 && bitmap.GetPixel(1233,776).G>240,"both corners of the complete image retained");
            Check(!File.Exists(cached.FilePath) && Field(cached,"_downloadedPath")==null,"opening a cached preview does not download original bytes");
            await cached.SelectPageAsync(1);
            Check(cached.CurrentImage!=null && cached.CurrentImage.Width==2222 && cached.CurrentPage.SourceWidthMm==1001,"artboard switching uses saved PNG and DB metadata");
            Check(object.ReferenceEquals(snapshot, await cached.CapturePreviewSnapshotAsync(CancellationToken.None)),"re-saving unchanged cached source reuses stored image references");
        }
        using(var legacy=new OrderSourceDocument { UseSavedPreviewsOnly=true }) {
            legacy.Restore(SavedEditor()); legacy.SetSavedPreview(null); await legacy.EnsureLoadedAsync();
            Check(legacy.CurrentImage==null && legacy.FileInfo.Contains("DB saved AI file info"),"legacy order keeps DB file info without automatic reanalysis");
        }
        var type=typeof(OrderRepository).Assembly.GetType("PineTree.Dialogs.DialogOrderDesignPad");
        using(var dialog=(Form)FormatterServices.GetUninitializedObject(type)) {
            typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(dialog,new object[0]);
            Call(dialog,"InitializeComponent"); Call(dialog,"InitializeOrderPricing"); Call(dialog,"InitOrderItemTable"); Call(dialog,"InitOrderEditor"); Call(dialog,"InitPreviewEvent");
            var mode=type.GetField("m_DialogMode",F);mode.SetValue(dialog,Enum.Parse(mode.FieldType,"Edit"));
            type.GetField("m_savedSourcePreview",F).SetValue(dialog,snapshot);
            var saved=new OrderDesignPadState { EquipmentName="수성", PriceCategory="수성", CategoryName="현수막", ProductName="snapshot fixture", UnitPrice=4000, Editor=SavedEditor() };
            Call(dialog,"ApplyDesignPadState",saved.ToJson());
            dialog.StartPosition=FormStartPosition.Manual; dialog.Location=new Point(-4000,-4000); dialog.Show();
            var doc=(OrderSourceDocument)Field(dialog,"m_sourceDocument"); await doc.EnsureLoadedAsync();
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while(doc.IsPreviewBusy && clock.Elapsed.TotalSeconds<30) await Task.Delay(20);
            var picture=(PictureBox)Field(dialog,"pictureBox1");
            Check(picture.Image!=null && picture.Image.Width==777 && picture.Image.Height==1234,"dialog shows saved full image with rotation applied once");
            var editor=Field(dialog,"m_orderEditor"); var restored=(BannerOrderEditorState)editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
            Check(restored.WidthMm==1000 && restored.Quantity==3 && restored.EdgeBottomMm==7,"DB metadata restores editor settings without analyzing source");
            dialog.Close();
        }
        Check(!Directory.Exists(Path.Combine(root,"forbidden-runtime")),"no PDF runtime or analyzer initialized while reopening orders");
        DatabaseRoundTrip(snapshot);
        Console.WriteLine("PASS: " + checks + " saved-preview checks");
    }
    static void DatabaseRoundTrip(OrderPreviewSnapshot snapshot)
    {
        var asm=typeof(OrderRepository).Assembly;
        using(var conn=(DbConnection)asm.GetType("PineTree.Classes.DB").GetMethod("CreateConnection").Invoke(null,null)) {
            conn.Open(); long id=0;
            using(var transaction=conn.BeginTransaction()) {
                try {
                    using(var cmd=conn.CreateCommand()) {
                        cmd.Transaction=transaction;cmd.CommandText="SELECT CustCode FROM Customers LIMIT 1";
                        var order=new OrderSaveData { CustCode=Convert.ToString(cmd.ExecuteScalar()), OrderDate=new DateTime(2000,1,1),
                            OrderTitle="Preview snapshot rollback regression", ProductionStatus="신규", SourceChannel="직접입력", CreatedBy="regression",
                            Items=new List<OrderItemSaveData>(), PreviewSnapshotJson=snapshot.ToJson() };
                        var save=typeof(OrderRepository).GetMethod("SaveOrderCore",BindingFlags.Static|BindingFlags.NonPublic);
                        id=(long)save.Invoke(null,new object[]{conn,transaction,order});
                        cmd.CommandText="SELECT p.OrderNo,p.SourceFileInfo,p.PreviewJSON,oi.OrderNo FROM OrderPreviewSnapshots p JOIN OrderInfo oi ON oi.OrderId=p.OrderId WHERE p.OrderId=@Id";
                        var parameter=cmd.CreateParameter();parameter.ParameterName="@Id";parameter.Value=id;cmd.Parameters.Add(parameter);
                        string stored;
                        using(var reader=cmd.ExecuteReader()) {
                            Check(reader.Read() && reader.GetString(0)==reader.GetString(3),"DB preview is keyed by actual allocated order number");
                            Check(reader.GetString(1)==snapshot.SourceFileInfo,"analysis file information persisted in DB");stored=reader.GetString(2);
                        }
                        Check(stored==snapshot.ToJson(),"all page analysis and server references persisted atomically with order");
                        order.OrderId=id;order.PreviewSnapshotJson=null;save.Invoke(null,new object[]{conn,transaction,order});
                        cmd.CommandText="SELECT PreviewJSON FROM OrderPreviewSnapshots WHERE OrderId=@Id";
                        Check(Convert.ToString(cmd.ExecuteScalar())==stored,"legacy order edits preserve saved previews");
                    }
                } finally {transaction.Rollback();}
            }
            Check(OrderPreviewSnapshotRepository.Get(id)==null,"test order and preview rows fully rolled back");
        }
    }
    [STAThread]static int Main(string[] args)
    {
        root=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string assemblyPath=Path.GetFullPath(args[0]);
        var app=Assembly.LoadFrom(assemblyPath);
        var engine=Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assemblyPath),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
        AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender, ResolveEventArgs e) {
            string name=new AssemblyName(e.Name).Name;
            return name=="PineTree" ? app : name=="PineTree.PdfEngine" ? engine : null;
        };
        Application.EnableVisualStyles();int exit=1;
        using(var host=new Form { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-5000,-5000) }) {
            host.Shown+=async delegate {try {await RunAsync();exit=0;}catch(Exception ex){Console.Error.WriteLine(ex);}finally{host.Close();}};
            Application.Run(host);
        }
        return exit;
    }
}
