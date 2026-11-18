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
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static async Task Wait(Func<bool> ready) { var t = Stopwatch.StartNew(); while (!ready()) { if (t.Elapsed.TotalSeconds > 240) throw new TimeoutException(); await Task.Delay(20); } }
    static bool NoTabs(Control c) { if (c is TabControl) return false; foreach (Control child in c.Controls) if (!NoTabs(child)) return false; return true; }
    static async Task Ready(object editor) { await Wait(() => Field(editor, "_sourceDetails") != null && (bool)Get(Field(editor, "btnPreview"), "Enabled")); }
    static object Finish(object editor, string code) { return editor.GetType().Assembly.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null, new object[] { code }); }
    static async Task Run(Form host, UserControl editor, Assembly assembly, string folder)
    {
        Console.WriteLine("Loading two-artboard source");
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "source.ai"));
        Check(Field(editor, "_sourceDetails") != null && !(bool)Field(editor, "_inspecting"), "metadata ready before preview finishes");
        Check(((PictureBox)Field(editor, "pictureBox1")).Image == null, "first preview runs after metadata load returns");
        Set(Field(editor, "inputNumericBox4"), "Value", 4m);
        await Wait(() => ((PictureBox)Field(editor, "pictureBox1")).Image != null);
        Check(Convert.ToDecimal(Get(Field(editor, "inputNumericBox4"), "Value")) == 4m, "input edited during rendering survives preview completion");
        Check(NoTabs(editor), "editor has no TabControl");
        var buttons = (Panel)Field(editor, "panelCanvas");
        Check(buttons.Controls.Count == 2 && (int)Field(editor, "_workingPage") == 0, "artboard buttons and initial selection");
        var firstImage = ((PictureBox)Field(editor, "pictureBox1")).Image;
        var hole = Finish(editor, "HOLE");
        Call(editor, "CommitPostProcess", -1, hole);
        Set(Field(editor, pet ? "inputNumericBox4" : "tbEdgeTop"), "Value", pet ? 3m : 7m);
        ((Button)buttons.Controls[1]).PerformClick();
        Check((int)Field(editor, "_workingPage") == 1, "button selects working page");
        Check(Field(editor, "_artboardProgress") is Form && ((Form)Field(editor, "_artboardProgress")).Visible, "uncached artboard shows progress dialog immediately");
        ((Button)buttons.Controls[0]).PerformClick();
        Check(Field(editor, "_artboardProgress") == null, "cached artboard closes progress dialog immediately");
        Check(Object.ReferenceEquals(firstImage, ((PictureBox)Field(editor, "pictureBox1")).Image), "first preview cache is immediate during second load");
        Check(((IList)Field(editor, "_postProcesses")).Count == 1 && Convert.ToDecimal(Get(Field(editor, pet ? "inputNumericBox4" : "tbEdgeTop"), "Value")) == (pet ? 3m : 7m), "first finishing and independent inputs retained");
        ((Button)buttons.Controls[1]).PerformClick();
        Check(Field(editor, "_artboardProgress") is Form, "in-flight artboard shows progress again");
        await Ready(editor);
        Check(Field(editor, "_artboardProgress") == null, "completed artboard closes progress dialog");
        Check(((IList)Field(editor, "_postProcesses")).Count == 0, "second artboard does not inherit first finishing");
        Check(Convert.ToDecimal(Get(Field(editor, pet ? "inputNumericBox4" : "tbEdgeTop"), "Value")) == (pet ? 1m : 2m), "second inputs independent");
        var cut = Finish(editor, "CUT"); Call(editor, "CommitPostProcess", -1, cut);
        if (pet)
        {
            var parameters = Get(cut, "Parameters");
            Set(parameters, "CutInsetTopMm", (decimal?)3m); Set(parameters, "CutInsetBottomMm", (decimal?)5m);
            Set(parameters, "CutInsetLeftMm", (decimal?)7m); Set(parameters, "CutInsetRightMm", (decimal?)11m);
            Call(editor, "CommitPostProcess", 0, cut);
            using (var dialog = (Form)Call(editor, "CreatePostProcessDialog", cut))
                Check(dialog.GetType().Name == "Dlg페트_재단", "PET retains its own cut dialog");
        }
        Set(Field(editor, pet ? "inputNumericBox4" : "tbEdgeBottom"), "Value", pet ? 5m : 9m);
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
        Check(buttons.Controls[1].BackColor != buttons.Controls[0].BackColor, "selection visibly distinct");
        var state = Capture(editor);
        string json = (string)state.GetType().GetMethod("ToJson").Invoke(state, null);
        state = state.GetType().GetMethod("FromJson").Invoke(null, new object[] { json });
        Check((int)Get(state, "WorkingArtboardIndex") == 1 && ((IList)Get(state, "Artboards")).Count == 2, "selected page and independent states serialized");
        ((Button)buttons.Controls[0]).PerformClick();
        settings = Call(editor, "ReadPdfGenerationSettings");
        Check((int)Get(settings, "SourcePageIndex") == 0 && (bool)Get(settings, "MakeHole") && !(bool)Get(settings, "MakeCutline"), "first finishing restored after second edit");
        await (Task)Call(editor, "GeneratePdfToPathAsync", Path.Combine(folder, "working-page1.pdf"), settings);
        using (var restored = (UserControl)Activator.CreateInstance(editor.GetType()))
        {
            restored.GetType().GetMethod("RestoreEditorState").Invoke(restored, new object[] { state });
            restored.Dock = DockStyle.Fill; host.Controls.Add(restored); restored.BringToFront();
            await Ready(restored);
            Check((int)Field(restored, "_workingPage") == 1 && ((IList)Field(restored, "_postProcesses")).Count == 1, "reload restores selected artboard and finishing");
            var restoredSettings = Call(restored, "ReadPdfGenerationSettings");
            Check((bool)Get(restoredSettings, "MakeCutline") && Convert.ToDecimal(Get(Field(restored, pet ? "inputNumericBox4" : "tbEdgeBottom"), "Value")) == (pet ? 5m : 9m), "second settings survive file reload");
            if (pet) Check((double)Get(restoredSettings, "CutInsetLeftMm") == 7 && (double)Get(restoredSettings, "EdgeLeftMm") == 0, "PET insets and zero bleed survive reload");
            Call(restored, "SelectWorkingArtboard", 0); await Ready(restored);
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
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "single.ai"));
        Check(buttons.Controls.Count == 1 && ((IList)Field(editor, "_postProcesses")).Count == 0, "new file clears prior independent finishing");
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "source.jpg"));
        await Wait(() => ((PictureBox)Field(editor, "pictureBox1")).Image != null);
        Check(buttons.Controls.Count == 1 && ((PictureBox)Field(editor, "pictureBox1")).Image != null, "JPEG input preserved");
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "source.pdf"));
        Check(buttons.Controls.Count == 2 && (int)Field(editor, "_workingPage") == 0, "PDF input creates working-page buttons");
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
            using (var editor = (UserControl)Activator.CreateInstance(assembly.GetType("PineTree.UserControlsOrder." + (pet ? "UcOrder페트" : "UcOrder수성_그룹A"), true)))
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
