using System;
using System.Collections;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class DesignPadSourceRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Assembly app;
    private static int checks;
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    private static void SendShellDrop(Control target, string path)
    {
        byte[] names = Encoding.Unicode.GetBytes(path + "\0\0");
        IntPtr drop = GlobalAlloc(0x42, new UIntPtr((uint)(20 + names.Length)));
        if (drop == IntPtr.Zero) throw new Exception("HDROP allocation failed");
        IntPtr buffer = GlobalLock(drop);
        Marshal.WriteInt32(buffer, 0, 20); Marshal.WriteInt32(buffer, 16, 1);
        Marshal.Copy(names, 0, IntPtr.Add(buffer, 20), names.Length);
        GlobalUnlock(drop);
        SendMessage(target.Handle, 0x0233, drop, IntPtr.Zero);
    }
    private static object Field(object o, string name) { return o.GetType().GetField(name, Flags).GetValue(o); }
    private static object Get(object o, string name) { return o.GetType().GetProperty(name, Flags).GetValue(o, null); }
    private static void Set(object o, string name, object value) { o.GetType().GetProperty(name, Flags).SetValue(o, value, null); }
    private static object Call(object o, string name, params object[] args)
    {
        try { return o.GetType().GetMethod(name, Flags).Invoke(o, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); ++checks; }
    private static decimal Number(object editor, string name) { if (name == "inputNumericBox4" && editor.GetType().GetField(name, Flags) == null) return (decimal)Get(editor, "Quantity"); return Convert.ToDecimal(Get(Field(editor, name), "Value")); }
    private static void Number(object editor, string name, decimal value) { if (name == "inputNumericBox4" && editor.GetType().GetField(name, Flags) == null) { editor.GetType().GetMethod("SetOrderQuantity").Invoke(editor, new object[] { value }); return; } Set(Field(editor, name), "Value", value); }
    private static object Bare(string typeName, Type baseType)
    {
        var target = FormatterServices.GetUninitializedObject(app.GetType(typeName, true));
        baseType.GetConstructor(Type.EmptyTypes).Invoke(target, new object[0]);
        Call(target, "InitializeComponent"); Call(target, "InitOrderItemTable");
        Call(target, "InitOrderEditor"); Call(target, "InitPreviewEvent");
        return target;
    }
    private static async Task Wait(Func<bool> ready)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(90);
        while (!ready()) { if (DateTime.UtcNow > limit) throw new TimeoutException("Source operation timed out"); await Task.Delay(30); }
    }
    private static object Source(object editor) { return Get(editor, "SourceDocument"); }
    private static void Radio(object pad, int number, string priceCategory = "수성")
    {
        // The source test uses a local catalog; live catalog filtering is tested separately.
        var loading = pad.GetType().GetField("m_loadingProductSelection", Flags);
        loading.SetValue(pad, true);
        try
        {
            Set(Field(pad, "rbEquipment1"), "Checked", true);
            var prices = new DataTable(); prices.Columns.Add("PriceCategory"); prices.Rows.Add(priceCategory);
            Call(pad, "BindProductCombo", Field(pad, "cbCategory"), prices, "PriceCategory");
            var table = new DataTable(); table.Columns.Add("CategoryName");
            foreach (string name in new[] { "현수막", "페트", "합성지", "켈지", "인화지" }) table.Rows.Add(name);
            Call(pad, "BindProductCombo", Field(pad, "cbCategoryName"), table, "CategoryName");
            Set(Field(pad, "cbCategoryName"), "SelectedIndex", number - 1);
        }
        finally { loading.SetValue(pad, false); }
        Call(pad, "UpdateSelectedOrderEditor");
    }
    private static void NoSourceUi(Control editor)
    {
        foreach (string name in new[] { "pictureBox1", "panelCanvas", "tbFileInfo", "c1SplitterPanel2" })
            Check(editor.GetType().GetField(name, Flags) == null, "Material designer must remove " + name);
    }
    private static async Task Generate(object editor, string path)
    {
        var settings = Call(editor, "ReadPdfGenerationSettings");
        await (Task)Call(editor, "GeneratePdfToPathAsync", path, settings);
        Check(File.Exists(path), "Material PDF generated: " + Path.GetFileName(path));
    }
    private static async Task Run(Form host, object pad, string folder)
    {
        Radio(pad, 1);
        var banner = (Control)Field(pad, "m_orderEditor");
        Check(banner.GetType().Name == "UcOrder수성_그룹A", "Radio 1 selects banner");
        NoSourceUi(banner);
        var source = Source(banner);
        Check(Object.ReferenceEquals(source, Field(pad, "m_sourceDocument")), "Pad owns the shared source");
        Console.WriteLine("Loading common source");
        await (Task)Call(source, "LoadAsync", Path.Combine(folder, "source.ai"));
        Check((int)Get(source, "PageCount") == 2 && Get(source, "CurrentImage") != null, "Common analysis and preview complete: " + Get(source, "FileInfo"));
        Check(Number(banner, "tbTargetWidth") == 212m && Number(banner, "tbTargetHeight") == 106m, "Analysis dimensions delivered to banner");
        Check(((PictureBox)Field(pad, "pictureBox1")).Image != null, "Pad's original preview displays the image");
        Number(banner, "inputNumericBox4", 4m); Number(banner, "tbEdgeTop", 7m);
        var hole = app.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null, new object[] { "HOLE" });
        Call(banner, "CommitPostProcess", -1, hole);
        Radio(pad, 2);
        var pet = (Control)Field(pad, "m_orderEditor");
        Check(pet.GetType().Name == "UcOrder페트", "Radio 2 selects PET");
        NoSourceUi(pet);
        Check(Object.ReferenceEquals(Source(pet), source), "Material switch reuses analysis and preview cache");
        Check(Number(pet, "tbTargetWidth") == 212m && Number(pet, "inputNumericBox4") == 1m, "PET receives analysis and independent quantity");
        Number(pet, "inputNumericBox4", 3m);
        Check(((IList)Field(pet, "_postProcesses")).Count == 0, "Finishing remains material-specific");
        Radio(pad, 1);
        Check(Object.ReferenceEquals(banner, Field(pad, "m_orderEditor")), "Banner instance retained");
        Check(Number(banner, "inputNumericBox4") == 4m && Number(banner, "tbEdgeTop") == 7m, "Banner draft retained");
        await (Task)Call(source, "SelectPageAsync", 1);
        Check(Number(banner, "tbTargetWidth") == 106m && Number(banner, "tbTargetHeight") == 212m, "Second page metadata delivered");
        Check(((IList)Field(banner, "_postProcesses")).Count == 0, "New page has independent finishing");
        Number(banner, "inputNumericBox4", 6m);
        Radio(pad, 2);
        Check(Number(pet, "inputNumericBox4") == 1m, "PET second-page quantity independent");
        Number(pet, "inputNumericBox4", 5m);
        await (Task)Call(source, "SelectPageAsync", 0);
        Check(Number(pet, "inputNumericBox4") == 3m, "PET first-page state restored");
        Radio(pad, 1);
        Check(Number(banner, "inputNumericBox4") == 4m && ((IList)Field(banner, "_postProcesses")).Count == 1, "Banner first-page settings restored");
        var image = Get(source, "CurrentImage");
        Radio(pad, 3);
        var syntheticPaper = (Control)Field(pad, "m_orderEditor");
        Check(syntheticPaper.GetType() == banner.GetType() && !Object.ReferenceEquals(syntheticPaper, banner),
            "Water-based synthetic paper uses Group A with a separate material draft");
        Check(Object.ReferenceEquals(Source(syntheticPaper), source) && Number(syntheticPaper, "inputNumericBox4") == 1m,
            "Synthetic paper shares the source but has independent quantity");
        Radio(pad, 3, "라텍스");
        Check(Field(pad, "m_orderEditor") == null, "Synthetic paper in another price category leaves the editor empty");
        for (int i = 4; i <= 5; ++i)
        {
            Radio(pad, i);
            Check(((Panel)Field(pad, "panelEdit")).Controls.Count == 0, "Unimplemented radio leaves editor empty");
            Check(Object.ReferenceEquals(image, Get(source, "CurrentImage")), "Unimplemented material preserves common source");
        }
        Radio(pad, 1);
        // Grid changes do not choose or replace the material.
        var table = (DataTable)Field(pad, "m_orderItems");
        var row = table.NewRow(); row["CategoryName"] = "페트"; row["Qty"] = 1m; row["SupplyAmount"] = 0m;
        row["VatAmount"] = 0m; row["TotalAmount"] = 0m; table.Rows.Add(row);
        Set(Field(pad, "FGrid1"), "Row", 1);
        Check(Object.ReferenceEquals(banner, Field(pad, "m_orderEditor")), "Grid row selection cannot replace radio-selected editor");
        await Generate(banner, Path.Combine(folder, "banner-page1.pdf"));
        Radio(pad, 2);
        await (Task)Call(source, "SelectPageAsync", 1);
        await Generate(pet, Path.Combine(folder, "pet-page2.pdf"));
        var state = Call(pet, "CaptureEditorState");
        Check((int)Get(state, "WorkingArtboardIndex") == 1 && (string)Get(state, "SourceFilePath") == Path.Combine(folder, "source.ai"), "Capture retains source reference and selected page");
        using (var restored = (Control)Activator.CreateInstance(pet.GetType()))
        {
            Call(restored, "RestoreEditorState", state);
            var restoredSource = Source(restored);
            await (Task)Call(restoredSource, "EnsureLoadedAsync");
            Check(Number(restored, "inputNumericBox4") == 5m && (int)Get(restoredSource, "WorkingPage") == 1, "Saved page and material settings restore through external source");
        }
        using (var bitmap = new Bitmap(((Control)pad).Width, ((Control)pad).Height))
        {
            ((Control)pad).DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(folder, "design-pad.png"));
        }
        Console.WriteLine("PASS: shared source, radios, material/page drafts, capture/restore and PDF generation");
        using (var modal = (Form)Bare("PineTree.Dialogs.DialogOrder", typeof(Form)))
        {
            var itemType = app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem", true);
            var item = Activator.CreateInstance(itemType);
            Set(item, "PriceItemSeqNo", 101L); Set(item, "PriceItemName", "Preview fixture");
            Set(item, "UnitName", "장"); Set(item, "BillingBasis", "수량"); Set(item, "UnitPrice", 1000m);
            Call(modal, "AddOrderItem", item, null);
            var modalTable = (DataTable)Field(modal, "m_orderItems");
            modalTable.Rows[0]["CategoryName"] = "합성지";
            modalTable.Rows[0]["PriceCategory"] = "라텍스";
            Set(Field(modal, "FGrid1"), "Row", 1); Call(modal, "UpdateSelectedOrderEditor");
            Check(Field(modal, "m_orderEditor").GetType().Name == "UcOrderBase",
                "Modal uses the default editor for synthetic paper in another price category");
            modalTable.Rows[0]["PriceCategory"] = "수성";
            Set(Field(modal, "FGrid1"), "Row", 1); Call(modal, "UpdateSelectedOrderEditor");
            var modalEditor = (Control)Field(modal, "m_orderEditor");
            Check(modalEditor.GetType() == banner.GetType(), "Modal uses Group A for water-based synthetic paper");
            Check(Field(modal, "m_sourcePreview") != null && Field(modal, "m_materialPanel") == modalEditor.Parent, "Modal hosts a separate common preview and compact editor");
            Check(!Object.ReferenceEquals(Source(modalEditor), source), "Modal order item owns an independent source document");
            modal.StartPosition = FormStartPosition.Manual; modal.Left = -4000; modal.Top = -4000; modal.ShowInTaskbar = false;
            modal.Show(host);
            await (Task)Call(Source(modalEditor), "LoadAsync", Path.Combine(folder, "source.jpg"));
            Check(Number(modalEditor, "tbTargetWidth") > 0m && Get(Source(modalEditor), "CurrentImage") != null, "Modal common source passes analysis to its material");
            var savedItems = (IList)Call(modal, "CreateOrderItemSaveDataList");
            Check(!string.IsNullOrEmpty((string)Get(savedItems[0], "EditorStateJson")), "Modal save mapping includes editor source state");
            using (var bitmap = new Bitmap(modal.Width, modal.Height))
            {
                modal.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(folder, "modal.png"));
            }
            modal.Close();
        }
        Check(!banner.IsDisposed && Get(source, "CurrentImage") != null, "Closing modal leaves shared design source intact");
        var preview = Field(pad, "m_sourcePreview");
        var picture = (Control)Field(pad, "pictureBox1");
        Call(preview, "InitializeFileDrop", false);
        var accepted = new DragEventArgs(new DataObject(DataFormats.FileDrop, new[] { Path.Combine(folder, "source.jpg") }), 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None);
        Call(preview, "DragEnter", picture, accepted);
        Check(accepted.Effect == DragDropEffects.Copy, "Common preview accepts supported file drops");
        var rejected = new DragEventArgs(new DataObject(DataFormats.FileDrop, new[] { Path.Combine(folder, "source.jpg"), Path.Combine(folder, "source.ai") }), 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None);
        Call(preview, "DragEnter", picture, rejected);
        Check(rejected.Effect == DragDropEffects.None, "Common preview retains single-file drop rule");
        var stale = (Task)Call(source, "LoadAsync", Path.Combine(folder, "source.ai"));
        Call(preview, "DragDrop", picture, accepted);
        await stale;
        await Wait(() => !(bool)Get(source, "IsPreviewBusy") && Get(source, "CurrentImage") != null);
        Check((int)Get(source, "PageCount") == 1 && ((string)Get(source, "ReferencePath")).EndsWith("source.jpg"), "Latest drop wins over canceled analysis");
        Check(Number(pet, "inputNumericBox4") == 1m && ((IList)Field(banner, "_postProcesses")).Count == 0, "New source resets each material's page state");
        Call(preview, "InitializeFileDrop", true);
        Check(!picture.AllowDrop && Field(preview, "_shellDrop") != null, "Elevated host registers native Shell drops in common preview");
        SendShellDrop(picture, Path.Combine(folder, "source.ai"));
        await Wait(() => (int)Get(source, "PageCount") == 2 && !(bool)Get(source, "IsPreviewBusy") && Get(source, "CurrentImage") != null);
        Check(((string)Get(source, "ReferencePath")).EndsWith("source.ai"), "Native drop reaches shared analysis and material editors");
        Call(preview, "InitializeFileDrop", false);
        Check(picture.AllowDrop && Field(preview, "_shellDrop") == null, "Native hook is released when normal drop mode resumes");
        Call(pad, "ResetNewOrder");
        Check((int)Get(source, "PageCount") == 0 && Get(source, "CurrentImage") == null, "New order clears shared source and images");
        Console.WriteLine("PASS: modal integration, source replacement/cancellation, and new order reset");
    }
    [STAThread] private static int Main(string[] args)
    {
        int exit = 0;
        try
        {
            Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            using (var host = new Form { ClientSize = new Size(1500, 920), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Left = -4000, Top = -4000 })
            using (var pad = (Control)Bare("PineTree.UserControls.UcOrderDesignPad", typeof(UserControl)))
            {
                pad.Dock = DockStyle.Fill; host.Controls.Add(pad);
                host.Shown += async delegate
                {
                    try { await Run(host, pad, Path.GetFullPath(args[1])); Console.WriteLine("PASS: " + checks + " common-source checks"); }
                    catch (Exception e) { Console.Error.WriteLine(e); exit = 1; }
                    finally { host.Close(); }
                };
                Application.Run(host);
            }
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        return exit;
    }
}
