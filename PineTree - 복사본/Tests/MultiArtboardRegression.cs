using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class MultiArtboardRegression
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static int cancellationExceptions;
    static object Field(object o, string n) { return o.GetType().GetField(n, Flags).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n, Flags).Invoke(o, a); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static async Task Wait(Func<bool> ready) { var clock = Stopwatch.StartNew(); while (!ready()) { if (clock.Elapsed.TotalSeconds > 240) throw new TimeoutException(); await Task.Delay(20); } }
    static async Task Run(Form host, UserControl editor, Assembly assembly, string folder)
    {
        Console.WriteLine("Loading source");
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "source.ai"));
        Console.WriteLine("Source loaded");
        var tabs = (TabControl)Field(editor, "tabControl1");
        Check(tabs.TabPages.Count == 2, "source has two tabs");
        Check(((PictureBox)tabs.TabPages[0].Controls[0]).Image != null, "first page rendered");
        var firstImage = ((PictureBox)tabs.TabPages[0].Controls[0]).Image;
        tabs.SelectedIndex = 1;
        Console.WriteLine("Selecting second source page");
        var pendingPages = (IDictionary)Field(editor, "_sourcePageLoads");
        var pending = pendingPages[1];
        tabs.SelectedIndex = 0;
        Check(Object.ReferenceEquals(firstImage, ((PictureBox)tabs.TabPages[0].Controls[0]).Image), "return to first page is immediate while second loads");
        tabs.SelectedIndex = 1;
        Check(Object.ReferenceEquals(pending, pendingPages[1]), "in-flight source render reused");
        await Wait(() => ((PictureBox)tabs.TabPages[1].Controls[0]).Image != null);
        Check(((PictureBox)tabs.TabPages[1].Controls[0]).Image != null, "second page rendered");
        Check(Object.ReferenceEquals(firstImage, ((PictureBox)tabs.TabPages[0].Controls[0]).Image), "first bitmap retained in bounded cache");
        tabs.SelectedIndex = 0;
        Check(Object.ReferenceEquals(firstImage, ((PictureBox)tabs.TabPages[0].Controls[0]).Image), "first page uses cached bitmap");
        var settings = Call(editor, "ReadPdfGenerationSettings");
        Check((bool)settings.GetType().GetProperty("GenerateAllPages").GetValue(settings, null), "banner enables AI pages");
        settings.GetType().GetProperty("MakeCutline").SetValue(settings, true, null);
        string output = Path.Combine(folder, "output.pdf");
        Console.WriteLine("Generating output");
        await (Task)Call(editor, "GeneratePdfToPathAsync", output, settings);
        var type = assembly.GetType("PineTree.Dialogs.DialogDocumentPreview", true);
        using (var viewer = (Form)Activator.CreateInstance(type, new object[] { output, true }))
        {
            viewer.ShowInTaskbar = false; viewer.StartPosition = FormStartPosition.Manual; viewer.Location = new Point(-4000, -4000);
            viewer.Show(host);
            Console.WriteLine("Rendering result");
            await Wait(() => Field(viewer, "_render") == null && ((PictureBox)Field(viewer, "_picture")).Image != null);
            var pages = (TabControl)Field(viewer, "_pages");
            Check(pages.TabPages.Count == 2, "result has two tabs");
            var outputFirstImage = ((PictureBox)Field(viewer, "_picture")).Image;
            for (int i = 0; i < 2; i++) Check(pages.TabPages[i].Controls["btnSavePage" + (i + 1)] is Button, "per-tab save button");
            pages.SelectedIndex = 1;
            pages.SelectedIndex = 0;
            Check(Object.ReferenceEquals(outputFirstImage, ((PictureBox)Field(viewer, "_picture")).Image), "result page one cached during next page load");
            pages.SelectedIndex = 1;
            await Wait(() => Field(viewer, "_render") == null);
            Check((int)Field(viewer, "_page") == 1 && ((PictureBox)Field(viewer, "_picture")).Image != null, "result page two rendered");
            var outputSecondImage = ((PictureBox)Field(viewer, "_picture")).Image;
            pages.SelectedIndex = 0; pages.SelectedIndex = 1;
            Check(Object.ReferenceEquals(outputSecondImage, ((PictureBox)Field(viewer, "_picture")).Image), "result page two cache returns synchronously");
            await (Task)Call(viewer, "SavePdfToPathAsync", Path.Combine(folder, "saved-all.pdf"));
            await (Task)Call(viewer, "SavePdfCoreAsync", Path.Combine(folder, "saved-page2.pdf"), (int?)1);
            Check(File.Exists(Path.Combine(folder, "saved-page2.pdf")), "independent save completed");
            // Close with a genuinely uncached worker running; no first-chance cancellation exception.
            var unfinished = (Task)Call(viewer, "GetPageAsync", 0, true);
            await Task.Delay(80);
            viewer.Close();
            await unfinished;
            await Wait(() => Field(viewer, "_render") == null);
        }
        tabs.SelectedIndex = 1;
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "single.ai"));
        Check(tabs.TabPages.Count == 1 && ((PictureBox)Field(editor, "pictureBox1")).Image != null, "new single source clears old tabs");
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "many.ai"));
        for (int i = 1; i < 6; i++)
        {
            tabs.SelectedIndex = i;
            int index = i;
            await Wait(() => ((PictureBox)tabs.TabPages[index].Controls[0]).Image != null);
            int images = 0;
            foreach (TabPage tab in tabs.TabPages) if (((PictureBox)tab.Controls[0]).Image != null) images++;
            Check(images <= 4, "source preview cache bounded to four images");
        }
        // Invalidate pending source work without cancelling merely because the selected tab changed.
        tabs.SelectedIndex = 0;
        await (Task)Call(editor, "LoadSourceFileAsync", Path.Combine(folder, "single.ai"));
        Check(cancellationExceptions == 0, "no first-chance cancellation exceptions on switch, close or replacement");
    }
    [STAThread] static int Main(string[] args)
    {
        int exit = 0;
        try
        {
            Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            AppDomain.CurrentDomain.FirstChanceException += delegate(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) {
                if (e.Exception is OperationCanceledException) System.Threading.Interlocked.Increment(ref cancellationExceptions);
            };
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            using (var host = new Form { Size = new Size(1050, 780), StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), ShowInTaskbar = false })
            using (var editor = (UserControl)Activator.CreateInstance(assembly.GetType("PineTree.UserControlsOrder.UcOrder현수막", true)))
            {
                editor.Dock = DockStyle.Fill; host.Controls.Add(editor);
                host.Shown += async delegate { try { await Run(host, editor, assembly, Path.GetFullPath(args[1])); Console.WriteLine("PASS: " + checks + " multi-artboard UI checks"); } catch (Exception e) { Console.Error.WriteLine(e); exit = 1; } finally { host.Close(); } };
                Application.Run(host);
            }
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        return exit;
    }
}
