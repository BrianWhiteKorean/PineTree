using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class WorkingArtboardRegression
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks, cancellations;
    static bool pet;
    static object Field(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    static object Get(object o, string n) { return o.GetType().GetProperty(n).GetValue(o, null); }
    static void Set(object o, string n, object v) { o.GetType().GetProperty(n).SetValue(o, v, null); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static object Capture(object o) { return o.GetType().GetMethod("CaptureEditorState").Invoke(o, null); }
    static Task CallDocument(object document, string method, params object[] args)
    { return (Task)document.GetType().GetMethod(method).Invoke(document, args); }
    static void SetQuantity(object editor, decimal quantity)
    { editor.GetType().GetMethod("SetOrderQuantity").Invoke(editor, new object[] { quantity }); }
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static async Task Wait(Func<bool> ready) { var t = Stopwatch.StartNew(); while (!ready()) { if (t.Elapsed.TotalSeconds > 240) throw new TimeoutException(); await Task.Delay(20); } }
    static bool NoTabs(Control c) { if (c is TabControl) return false; foreach (Control child in c.Controls) if (!NoTabs(child)) return false; return true; }
    static async Task Ready(object editor) { await Wait(() => Field(editor, "_sourceDetails") != null && (bool)Get(Field(editor, "btnPreview"), "Enabled")); }
    static object Finish(object editor, string code) { return editor.GetType().Assembly.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null, new object[] { code }); }
    static async Task Run(Form host, UserControl editor, Assembly assembly, string folder)
    {
        Console.WriteLine("Loading two-artboard source");
        var document = Get(editor, "SourceDocument");
        await CallDocument(document, "LoadAsync", Path.Combine(folder, "source.ai"));
        Check(Field(editor, "_sourceDetails") != null && !(bool)Get(document, "IsBusy"), "metadata ready after source load");
        SetQuantity(editor, 4m);
        await Wait(() => Get(document, "CurrentImage") != null);
        Check((decimal)Get(editor, "Quantity") == 4m, "input survives preview completion");
        Check(NoTabs(editor), "editor has no TabControl");
        Check((int)Get(document, "PageCount") == 2 && (int)Get(document, "WorkingPage") == 0, "artboard count and initial selection");
        var firstImage = Get(document, "CurrentImage");
        var hole = Finish(editor, "HOLE");
        Call(editor, "CommitPostProcess", -1, hole);
        if (pet) SetQuantity(editor, 3m);
        else Set(Field(editor, "tbEdgeTop"), "Value", 7m);
        var secondLoad = CallDocument(document, "SelectPageAsync", 1);
        Check((int)Get(document, "WorkingPage") == 1, "source selects working page");
        await CallDocument(document, "SelectPageAsync", 0);
        Check(Object.ReferenceEquals(firstImage, Get(document, "CurrentImage")), "first preview cache is immediate during second load");
        Check(((IList)Field(editor, "_postProcesses")).Count == 1 && (pet ? (decimal)Get(editor, "Quantity") : Convert.ToDecimal(Get(Field(editor, "tbEdgeTop"), "Value"))) == (pet ? 3m : 7m), "first finishing and independent inputs retained");
        await CallDocument(document, "SelectPageAsync", 1);
        await secondLoad;
        await Ready(editor);
        Check(!(bool)Get(document, "IsBusy"), "completed artboard clears busy state");
        Check(((IList)Field(editor, "_postProcesses")).Count == 0, "second artboard does not inherit first finishing");
        Check((pet ? (decimal)Get(editor, "Quantity") : Convert.ToDecimal(Get(Field(editor, "tbEdgeTop"), "Value"))) == (pet ? 1m : 0m), "second inputs independent");
        var cut = Finish(editor, "CUT"); Call(editor, "CommitPostProcess", -1, cut);
        if (pet)
        {
            var parameters = Get(cut, "Parameters");
            Set(parameters, "CutInsetTopMm", (decimal?)3m); Set(parameters, "CutInsetBottomMm", (decimal?)5m);
            Set(parameters, "CutInsetLeftMm", (decimal?)7m); Set(parameters, "CutInsetRightMm", (decimal?)11m);
            Call(editor, "CommitPostProcess", 0, cut);
            using (var dialog = (Form)Call(editor, "CreatePostProcessDialog", cut))
                Check(dialog.GetType().Name == "Dlg후가공공통_재단선", "PET opens shared cut dialog");
        }
        if (pet) SetQuantity(editor, 5m);
        else Set(Field(editor, "tbEdgeBottom"), "Value", 9m);
        var settings = Call(editor, "ReadPdfGenerationSettings");
        Check(!(bool)Get(settings, "GenerateAllPages") && (int)Get(settings, "SourcePageIndex") == 1, "generation targets selected page only");
        Check((bool)Get(settings, "MakeCutline") && !(bool)Get(settings, "MakeHole"), "second finishing only");
        if (pet)
        {
            Check((double)Get(settings, "EdgeTopMm") == 0 && (double)Get(settings, "EdgeBottomMm") == 0
                && (double)Get(settings, "EdgeLeftMm") == 0 && (double)Get(settings, "EdgeRightMm") == 0, "PET keeps zero outer bleed");
            Check((double)Get(settings, "CutInsetTopMm") == 3 && (double)Get(settings, "CutInsetBottomMm") == 5
                && (double)Get(settings, "CutInsetLeftMm") == 7 && (double)Get(settings, "CutInsetRightMm") == 11, "PET keeps independent cut insets");
            var scroll = Finish(editor, "AL_SCROLL");
            Call(editor, "CommitPostProcess", -1, scroll);
            var combined = Call(editor, "ReadPdfGenerationSettings");
            Check((bool)Get(combined, "MakeAlScroll") && (double)Get(combined, "CutInsetTopMm") == 3
                && (double)Get(combined, "CutInsetRightMm") == 11, "PET scroll retains explicit CUT override");
            Call(editor, "RemovePostProcess", 1);
        }
        string second = Path.Combine(folder, "working-page2.pdf");
        await (Task)Call(editor, "GeneratePdfToPathAsync", second, settings);
        var state = Capture(editor);
        string json = (string)state.GetType().GetMethod("ToJson").Invoke(state, null);
        state = state.GetType().GetMethod("FromJson").Invoke(null, new object[] { json });
        Check((int)Get(state, "WorkingArtboardIndex") == 1 && ((IList)Get(state, "Artboards")).Count == 2, "selected page and independent states serialized");
        await CallDocument(document, "SelectPageAsync", 0);
        settings = Call(editor, "ReadPdfGenerationSettings");
        Check((int)Get(settings, "SourcePageIndex") == 0 && (bool)Get(settings, "MakeHole") && !(bool)Get(settings, "MakeCutline"), "first finishing restored after second edit");
        await (Task)Call(editor, "GeneratePdfToPathAsync", Path.Combine(folder, "working-page1.pdf"), settings);
        using (var restored = (UserControl)Activator.CreateInstance(editor.GetType()))
        {
            restored.GetType().GetMethod("RestoreEditorState").Invoke(restored, new object[] { state });
            restored.Dock = DockStyle.Fill; host.Controls.Add(restored); restored.BringToFront();
            var restoredDocument = Get(restored, "SourceDocument");
            await CallDocument(restoredDocument, "EnsureLoadedAsync");
            await Ready(restored);
            Check((int)Field(restored, "_workingPage") == 1 && ((IList)Field(restored, "_postProcesses")).Count == 1, "reload restores selected artboard and finishing");
            var restoredSettings = Call(restored, "ReadPdfGenerationSettings");
            Check((bool)Get(restoredSettings, "MakeCutline") && (pet ? (decimal)Get(restored, "Quantity") : Convert.ToDecimal(Get(Field(restored, "tbEdgeBottom"), "Value"))) == (pet ? 5m : 9m), "second settings survive file reload");
            if (pet) Check((double)Get(restoredSettings, "CutInsetLeftMm") == 7 && (double)Get(restoredSettings, "EdgeLeftMm") == 0, "PET insets and zero bleed survive reload");
            await CallDocument(restoredDocument, "SelectPageAsync", 0); await Ready(restored);
            Check((bool)Get(Call(restored, "ReadPdfGenerationSettings"), "MakeHole"), "first independent settings survive file reload");
            host.Controls.Remove(restored);
        }
        var type = assembly.GetType("PineTree.Dialogs.DialogDocumentPreview", true);
        using (var viewer = (Form)Activator.CreateInstance(type, new object[] { second, true }))
        {
            viewer.ShowInTaskbar = false; viewer.StartPosition = FormStartPosition.Manual; viewer.Location = new Point(-4000, -4000); viewer.Show(host);
            await Wait(() => Field(viewer, "_render") == null && ((PictureBox)Field(viewer, "_picture")).Image != null);
            Check(NoTabs(viewer) && (int)Field(viewer, "_pageCount") == 1, "output preview has one page and no tabs");
            Check(!((ToolStripButton)Field(viewer, "_next")).Visible, "single page has no next-page navigation");
            await (Task)Call(viewer, "SavePdfToPathAsync", Path.Combine(folder, "working-saved.pdf"));
            viewer.Close();
        }
        await CallDocument(document, "LoadAsync", Path.Combine(folder, "single.ai"));
        Check((int)Get(document, "PageCount") == 1 && ((IList)Field(editor, "_postProcesses")).Count == 0, "new file clears prior independent finishing");
        await CallDocument(document, "LoadAsync", Path.Combine(folder, "source.jpg"));
        await Wait(() => Get(document, "CurrentImage") != null);
        Check((int)Get(document, "PageCount") == 1, "JPEG input preserved");
        await CallDocument(document, "LoadAsync", Path.Combine(folder, "source.pdf"));
        Check((int)Get(document, "PageCount") == 2 && (int)Get(document, "WorkingPage") == 0, "PDF input sets initial working page");
        Check(cancellations == 0, "normal page switches have no cancellation exceptions");
    }
    [STAThread] static int Main(string[] args)
    {
        int exit = 0;
        try
        {
            Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            AppDomain.CurrentDomain.FirstChanceException += delegate(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) { if (e.Exception is OperationCanceledException) System.Threading.Interlocked.Increment(ref cancellations); };
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            pet = args.Length > 2 && args[2] == "Pet";
            using (var host = new Form { Size = new Size(1050, 780), StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), ShowInTaskbar = false })
            using (var editor = (UserControl)Activator.CreateInstance(assembly.GetType("PineTree.UserControlsOrder." + (pet ? "UcOrder수성_그룹B" : "UcOrder수성_그룹A"), true)))
            {
                editor.Dock = DockStyle.Fill; host.Controls.Add(editor);
                host.Shown += async delegate { try { await Run(host, editor, assembly, Path.GetFullPath(args[1])); Console.WriteLine("PASS: " + checks + " working artboard UI checks"); } catch (Exception e) { Console.Error.WriteLine(e); exit = 1; } finally { host.Close(); } };
                Application.Run(host);
            }
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        return exit;
    }
}
