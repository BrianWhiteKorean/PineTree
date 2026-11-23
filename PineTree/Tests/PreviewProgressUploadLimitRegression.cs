using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

// Offline integration checks: no server uploads or order changes.
internal static class PreviewProgressUploadLimitRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
    private static async Task WaitUntil(Func<bool> ready)
    {
        var clock = Stopwatch.StartNew();
        while (!ready()) { if (clock.Elapsed.TotalSeconds > 180) throw new TimeoutException(); await Task.Delay(20); }
    }

    private static async Task Run(Form host, Assembly app, string folder)
    {
        Directory.CreateDirectory(folder);
        var storage = app.GetType("PineTree.Classes.OrderFileStorageService", true);
        var allowed = storage.GetMethod("CanUploadSource");
        string boundary = Path.Combine(folder, "boundary.ai");
        foreach (long length in new[] { 100L * 1024 * 1024 - 1, 100L * 1024 * 1024, 100L * 1024 * 1024 + 1 })
        {
            using (var stream = File.Create(boundary)) stream.SetLength(length);
            Check((bool)allowed.Invoke(null, new object[] { boundary }) == (length < 100L * 1024 * 1024), "100 MB upload boundary: " + length);
        }
        File.Delete(boundary);
        string source = Path.Combine(folder, "source.png");
        using (var bitmap = new Bitmap(120, 240))
        {
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Teal);
            bitmap.Save(source, System.Drawing.Imaging.ImageFormat.Png);
        }
        string output = null;
        foreach (string name in new[] { "UcOrder수성_그룹B", "UcOrder수성_그룹A" })
        using (var editor = (UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder." + name, true)))
        {
            editor.Dock = DockStyle.Fill;
            host.Controls.Add(editor);
            var document = editor.GetType().GetProperty("SourceDocument").GetValue(editor, null);
            await (Task)document.GetType().GetMethod("LoadAsync").Invoke(document, new object[] { source });
            Check(Field(editor, "_sourceDetails") != null, name + " source load: "
                + document.GetType().GetProperty("FileInfo").GetValue(document, null));
            var settings = Call(editor, "ReadPdfGenerationSettings");
            output = Path.Combine(folder, name + ".pdf");
            var task = (Task)Call(editor, "GeneratePdfToPathAsync", output, settings);
            if (task.IsFaulted) await task;
            var progress = (Form)Field(editor, "_sourceProgress");
            Check(progress != null && progress.Visible && progress.Owner == host, name + " generation progress visible: "
                + (progress == null ? "null" : "visible=" + progress.Visible + ", owner=" + progress.Owner));
            Check(((ProgressBar)Field(progress, "progressBar1")).Style == ProgressBarStyle.Marquee, "generation progress animates");
            await task;
            Check(File.Exists(output) && progress.IsDisposed && Field(editor, "_sourceProgress") == null, name + " success closes progress");
            // A directory cannot be used as an output file; exercise failure cleanup.
            string invalidOutput = Path.Combine(folder, "invalid-output.pdf");
            Directory.CreateDirectory(invalidOutput);
            task = (Task)Call(editor, "GeneratePdfToPathAsync", invalidOutput, settings);
            progress = (Form)Field(editor, "_sourceProgress");
            bool failed = false;
            try { await task; } catch { failed = true; }
            Check(failed && progress.IsDisposed && Field(editor, "_sourceProgress") == null, name + " failure closes progress");
            host.Controls.Remove(editor);
        }
        var viewerType = app.GetType("PineTree.Dialogs.DialogDocumentPreview", true);
        using (var viewer = (Form)Activator.CreateInstance(viewerType, new object[] { output, true }))
        {
            viewer.ShowInTaskbar = false;
            viewer.Show(host);
            await WaitUntil(() => Field(viewer, "_renderProgress") != null);
            var progress = (Form)Field(viewer, "_renderProgress");
            Check(progress != null && progress.Visible && progress.Owner == viewer, "preview rendering progress visible");
            Call(viewer, "Render");
            Check(object.ReferenceEquals(progress, Field(viewer, "_renderProgress")), "replacement render reuses progress");
            await WaitUntil(() => Field(viewer, "_render") == null);
            Check(((PictureBox)Field(viewer, "_picture")).Image != null && progress.IsDisposed, "latest render closes progress");
            Call(viewer, "Render");
            Check(Field(viewer, "_renderProgress") == null, "cached preview does not open progress");
        }
        using (var viewer = (Form)Activator.CreateInstance(viewerType, new object[] { output, true }))
        {
            viewer.ShowInTaskbar = false;
            viewer.Show(host);
            await WaitUntil(() => Field(viewer, "_renderProgress") != null);
            var progress = (Form)Field(viewer, "_renderProgress");
            viewer.Dispose();
            Check(progress.IsDisposed && Field(viewer, "_renderProgress") == null, "closing preview disposes progress");
        }
        using (var viewer = (Form)Activator.CreateInstance(viewerType, new object[] { Path.Combine(folder, "missing.pdf"), true }))
        {
            viewer.ShowInTaskbar = false;
            viewer.Show(host);
            await WaitUntil(() => Field(viewer, "_renderProgress") != null
                || ((Label)Field(viewer, "_status")).Text.Contains("실패"));
            await WaitUntil(() => Field(viewer, "_render") == null);
            Check(Field(viewer, "_renderProgress") == null && ((Label)Field(viewer, "_status")).Text.Contains("실패"), "render failure closes progress");
        }
        Console.WriteLine("PASS: offline upload limit and preview progress checks: " + checks);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        int exit = 0;
        try
        {
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Assembly.LoadFrom(Path.GetFullPath(args[2]));
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            using (var host = new Form { Width = 1000, Height = 700, ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Left = -4000, Top = -4000 })
            {
                host.Shown += async delegate
                {
                    try { await Run(host, app, Path.GetFullPath(args[1])); }
                    catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; }
                    finally { host.Close(); }
                };
                Application.Run(host);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        return exit;
    }
}
