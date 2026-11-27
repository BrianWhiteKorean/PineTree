using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Run in a fresh process: the first engine assembly load must happen off the UI thread.
internal static class BannerOrderProgressRegression
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static int engineLoadThread;
    private static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    private static Task Load(object target, string path) { return (Task)target.GetType().GetMethod("LoadSourceFileAsync", Flags).Invoke(target, new object[] { path }); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static bool EngineLoaded() { foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) if (a.GetName().Name == "PineTree.PdfEngine") return true; return false; }
    private static Form Progress(object target) { return (Form)Field(target, "_sourceProgress"); }
    private static async Task Wait(Task task) { if (await Task.WhenAny(task, Task.Delay(180000)) != task) throw new TimeoutException(); await task; }

    [STAThread] private static int Main(string[] args)
    {
        int exit = 0;
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            int uiThread = Thread.CurrentThread.ManagedThreadId;
            AppDomain.CurrentDomain.AssemblyLoad += delegate(object sender, AssemblyLoadEventArgs e)
            {
                if (e.LoadedAssembly.GetName().Name == "PineTree.PdfEngine") Interlocked.Exchange(ref engineLoadThread, Thread.CurrentThread.ManagedThreadId);
            };
            var type = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("PineTree.UserControlsOrder.UcOrder현수막", true);
            string folder = Path.GetFullPath(args[1]);
            using (var control = (UserControl)Activator.CreateInstance(type))
            using (var host = new Form { Width = 1170, Height = 780, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Left = -4000, Top = -4000 })
            {
                control.Dock = DockStyle.Fill; host.Controls.Add(control);
                host.Shown += async delegate
                {
                    try
                    {
                        Check(!EngineLoaded(), "constructor must not load engine");
                        int ticks = 0;
                        using (var timer = new System.Windows.Forms.Timer { Interval = 30 })
                        {
                            timer.Tick += delegate { ticks++; };
                            timer.Start();
                            var clock = Stopwatch.StartNew();
                            var first = Load(control, Path.Combine(folder, "source.eps"));
                            Check(clock.ElapsedMilliseconds < 1500, "drop kickoff blocked the UI for " + clock.ElapsedMilliseconds + "ms");
                            Console.WriteLine("Drop kickoff: " + clock.ElapsedMilliseconds + " ms");
                            var progress = Progress(control);
                            Check(progress != null && progress.Visible, "progress must be visible before engine loading completes");
                            Check(((ProgressBar)Field(progress, "progressBar1")).Style == ProgressBarStyle.Marquee, "progress must animate without invented percentages");
                            Check(!((Label)Field(progress, "label1")).Visible && !progress.ShowInTaskbar, "progress window shows bar only");
                            using (var image = new Bitmap(progress.Width, progress.Height)) { progress.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(folder, "source-progress.png")); }
                            await Task.Delay(200);
                            Check(ticks > 0, "UI heartbeat must continue during first engine load");
                            await Wait(first);
                            Check(engineLoadThread != 0 && engineLoadThread != uiThread, "engine assembly loaded on the UI thread");
                            Check(Field(control, "_sourceDetails") != null && ((PictureBox)Field(control, "pictureBox1")).Image != null, "EPS source inspection and preview failed");
                            Check(Progress(control) == null && progress.IsDisposed, "successful source load must close progress");
                            Console.WriteLine("Cold EPS load: " + clock.ElapsedMilliseconds + " ms; UI heartbeat ticks: " + ticks);
                        }
                        string broken = Path.Combine(folder, "broken-progress.eps");
                        File.WriteAllText(broken, "This is not a valid EPS file.");
                        var failed = Load(control, broken);
                        var errorProgress = Progress(control);
                        Check(errorProgress != null && errorProgress.Visible, "failed source must also show progress while processing");
                        await Wait(failed);
                        Check(Field(control, "_sourceDetails") == null && errorProgress.IsDisposed && Progress(control) == null, "failure must close progress and clear source");
                        var old = Load(control, Path.Combine(folder, "source.eps"));
                        var shared = Progress(control);
                        var latest = Load(control, Path.Combine(folder, "source.png"));
                        Check(object.ReferenceEquals(shared, Progress(control)), "replacement must reuse one progress window");
                        await Wait(old); await Wait(latest);
                        Check(((string)Field(control, "_sourcePath")).EndsWith("source.png") && Field(control, "_sourceDetails") != null && Progress(control) == null, "replacement must finish with newest image and no progress leak");
                        var pending = Load(control, Path.Combine(folder, "source.eps"));
                        var closingProgress = Progress(control);
                        control.Dispose();
                        await Wait(pending);
                        Check(closingProgress.IsDisposed && Progress(control) == null, "disposing control must close progress and cancel source processing");
                        Console.WriteLine("PASS: progress integration checks: " + checks);
                    }
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
