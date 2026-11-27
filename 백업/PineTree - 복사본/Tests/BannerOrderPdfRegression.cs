using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class BannerOrderPdfRegression
{
    private static object Finish(object editor,string code) { return editor.GetType().Assembly.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null,new object[]{code}); }
    private static int checks;
    private static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
    private static object Get(object target, string name) { return target.GetType().GetProperty(name).GetValue(target, null); }
    private static void Set(object target, string name, object value) { target.GetType().GetProperty(name).SetValue(target, value, null); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static async Task PumpAsync(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready()) { await Task.Delay(20); if (watch.Elapsed.TotalSeconds > 240) throw new TimeoutException(); }
    }
    private static void Number(object control, string name, decimal value) { Set(Field(control, name), "Value", value); }
    private static DragEventArgs DropArgs(params string[] paths) { return new DragEventArgs(new DataObject(DataFormats.FileDrop, paths), 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None); }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);

    private static void SendShellDrop(Control target, params string[] paths)
    {
        byte[] names = Encoding.Unicode.GetBytes(string.Join("\0", paths) + "\0\0");
        IntPtr drop = GlobalAlloc(0x42, new UIntPtr((uint)(20 + names.Length)));
        if (drop == IntPtr.Zero) throw new Exception("HDROP allocation failed");
        IntPtr buffer = GlobalLock(drop);
        Marshal.WriteInt32(buffer, 0, 20);
        Marshal.WriteInt32(buffer, 16, 1);
        Marshal.Copy(names, 0, IntPtr.Add(buffer, 20), names.Length);
        GlobalUnlock(drop);
        // The receiving window owns the HDROP and must release it with DragFinish.
        SendMessage(target.Handle, 0x0233, drop, IntPtr.Zero);
    }

    [STAThread] private static int Main(string[] args)
    {
        int exitCode = 0;
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Console.WriteLine("Creating editor");
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var type = assembly.GetType("PineTree.UserControlsOrder.UcOrder현수막", true);
            string folder = Path.GetFullPath(args[1]);
            using (var host = new Form { Size = new Size(1050, 780), StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), ShowInTaskbar = false })
            using (var control = (UserControl)Activator.CreateInstance(type))
            {
                control.Dock = DockStyle.Fill; host.Controls.Add(control);
                host.Shown += async delegate
                {
                    try { await RunChecksAsync(control, host, folder); Console.WriteLine("PASS: banner editor PDF integration checks: " + checks); }
                    catch (Exception ex) { Console.Error.WriteLine(ex); exitCode = 1; }
                    finally { host.Close(); }
                };
                Application.Run(host);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        return exitCode;
    }

    private static async Task RunChecksAsync(UserControl control, Form host, string folder)
    {
                var picture = (PictureBox)Field(control, "pictureBox1");
                Check(picture.AllowDrop && picture.SizeMode == PictureBoxSizeMode.Zoom, "drop and image display initialized");
                Check(!(bool)Get(Field(control, "btnPreview"), "Enabled"), "generation disabled without source");
                var settings = Call(control, "ReadPdfGenerationSettings");
                Check((double)Get(settings, "TargetWidthMm") == 0 && (double)Get(settings, "LineWidthMm") == .18, "visible generation defaults");
                Check(Convert.ToDecimal(Get(Field(control, "inputNumericBox3"), "Value")) == 0m, "default area");
                Check(Convert.ToDecimal(Get(Field(control, "tbTargetHeight"), "Value")) == 0 && Convert.ToDecimal(Get(Field(control, "inputNumericBox4"), "Value")) == 0, "dimensions and quantity initially zero");
                Check(((System.Collections.IList)Field(control,"_postProcesses")).Count==0, "finishing list initially empty");
                Number(control, "tbTargetWidth", 600); Number(control, "tbTargetHeight", 1800);
                Number(control, "inputNumericBox4", 3);
                Check(Convert.ToDecimal(Get(Field(control, "inputNumericBox3"), "Value")) == 3.24m, "area includes quantity");
                Number(control, "inputNumericBox4", 1);
                var invalid = DropArgs(Path.Combine(folder, "unsupported.txt"));
                Call(control, "Source_DragEnter", picture, invalid);
                Check(invalid.Effect == DragDropEffects.None, "unsupported drop rejected");
                var multiple = DropArgs(Path.Combine(folder, "source.pdf"), Path.Combine(folder, "source.png"));
                Call(control, "Source_DragEnter", picture, multiple);
                Check(multiple.Effect == DragDropEffects.None, "multiple files rejected");

                var inputPanel = Field(control, "c1InputPanel1");
                var button = Field(control, "btnPreview");
                MethodInfo boundsMethod = null;
                foreach (var method in inputPanel.GetType().GetMethods())
                    if (method.Name == "GetItemBounds" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType.IsInstanceOfType(button)) boundsMethod = method;
                var buttonBounds = (Rectangle)boundsMethod.Invoke(inputPanel, new object[] { button });
                Check(buttonBounds.Right <= ((Control)inputPanel).ClientSize.Width && buttonBounds.Width > 0, "preview button stays within the editor width");
                Console.WriteLine("Input settings passed; testing file drops");
                foreach (string extension in new[] { "pdf", "eps", "ai", "jpg", "png" })
                {
                    Console.WriteLine("Loading " + extension);
                    var drop = DropArgs(Path.Combine(folder, "source." + extension));
                    Call(control, "Source_DragEnter", picture, drop);
                    Check(drop.Effect == DragDropEffects.Copy, extension + " drop accepted");
                    await (Task)Call(control, "LoadSourceFileAsync", Path.Combine(folder, "source." + extension));
                    Check(Field(control, "_sourceDetails") != null && picture.Image != null, extension + " inspection and thumbnail");
                    Check(Convert.ToString(Get(Field(control, "tbFileInfo"), "Text")).Contains("source." + extension), extension + " filename information");
                }
                Console.WriteLine("Testing rapid replacement");
                var first = (Task)Call(control, "LoadSourceFileAsync", Path.Combine(folder, "source.eps"));
                var latest = (Task)Call(control, "LoadSourceFileAsync", Path.Combine(folder, "source.png"));
                await latest; await first;
                Check(Convert.ToString(Get(Field(control, "tbFileInfo"), "Text")).Contains("PNG"), "latest drop wins over cancelled operation");
                Check(Convert.ToDecimal(Get(Field(control, "tbTargetWidth"), "Value")) == 51m && Convert.ToDecimal(Get(Field(control, "tbTargetHeight"), "Value")) == 102m, "source DPI dimensions populate inputs");
                Check(Convert.ToDecimal(Get(Field(control, "inputNumericBox4"), "Value")) == 1, "drop sets quantity to one");
                Number(control, "tbTargetWidth", 600); Number(control, "tbTargetHeight", 1800);
                Call(control,"CommitPostProcess",-1,Finish(control,"CUT"));
                var hole=Finish(control,"HOLE");Set(Get(hole,"Parameters"),"TypeCode","CUSTOM_SPACING");Call(control,"CommitPostProcess",-1,hole);
                Check((int)hole.GetType().GetMethod("GetMarkCount").Invoke(hole,new object[]{600m,1800m})==12,"custom spacing mark count");
                Set(Get(((System.Collections.IList)Field(control,"_postProcesses"))[1],"Parameters"),"TypeCode","FOUR_CORNERS");
                Set(Field(control, "cbDeploy"), "SelectedOption", Field(control, "inputOption5"));
                settings = Call(control, "ReadPdfGenerationSettings");
                Check((string)Get(settings, "FitMode") == "COVER", "cover option mapped");
                Set(Field(control, "cbDeploy"), "SelectedOption", Field(control, "inputOption4"));
                settings = Call(control, "ReadPdfGenerationSettings");
                string output = Path.Combine(folder, "generated.pdf");
                Console.WriteLine("Generating output PDF");
                var generation = (Task)Call(control, "GeneratePdfToPathAsync", output, settings);
                Check(!(bool)Get(Field(control, "btnPreview"), "Enabled") && !(bool)Get(Field(control, "tbTargetWidth"), "Enabled"), "inputs locked during generation");
                await generation;
                var result = Get(generation, "Result");
                Check(Math.Abs((double)Get(result, "OutputWidthMm") - 604) < .0001 && Math.Abs((double)Get(result, "OutputHeightMm") - 1804) < .0001 && (int)Get(result, "MarkCount") == 4, "editor values reach PDF engine");
                Check(File.Exists(output) && (bool)Get(Field(control, "btnPreview"), "Enabled"), "generation restores input state");
                using (var bitmap = new Bitmap(host.Width, host.Height)) { host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(folder, "banner-control.png")); }
                Console.WriteLine("Testing preview window");
                Call(control, "PreviewOutput", control, EventArgs.Empty);
                Form preview = null;
                await PumpAsync(() => { foreach (Form form in Application.OpenForms) if (form.GetType().Name == "DialogDocumentPreview") preview = form; return preview != null; });
                preview.Location = new Point(-5000, -5000);
                await PumpAsync(() => ((PictureBox)Field(preview, "_picture")).Image != null || ((Label)Field(preview, "_status")).Text.Contains("실패"));
                Check(((PictureBox)Field(preview, "_picture")).Image != null, "preview button opens rendered output in separate window");
                string temporaryPdf = (string)Field(preview, "_source");
                Check(File.Exists(temporaryPdf), "temporary PDF exists while preview open");
                preview.Close(); Application.DoEvents();
                Check(!File.Exists(temporaryPdf), "temporary PDF removed on preview close");
                Console.WriteLine("Testing native Shell drops for elevated hosting");
                Call(control, "InitializeFileDrop", true);
                Check(!picture.AllowDrop && Field(control, "_shellDropWindow") != null, "elevated mode uses Shell drop instead of OLE");
                Check((GetWindowLong(picture.Handle, -20) & 0x10) != 0, "picture window registered for WM_DROPFILES");
                string unicodeSource = Path.Combine(folder, "한글 원본.png");
                File.Copy(Path.Combine(folder, "source.png"), unicodeSource, true);
                SendShellDrop(picture, unicodeSource);
                await PumpAsync(() => !(bool)Field(control, "_inspecting"));
                Check(Field(control, "_sourceDetails") != null && picture.Image != null && Convert.ToString(Get(Field(control, "tbFileInfo"), "Text")).Contains("한글 원본.png"), "native HDROP reaches Unicode file inspection and preview");
                string currentSource = (string)Field(control, "_sourcePath");
                SendShellDrop(picture, Path.Combine(folder, "unsupported.txt"));
                SendShellDrop(picture, unicodeSource, Path.Combine(folder, "source.pdf"));
                Check((string)Field(control, "_sourcePath") == currentSource, "native drop rejects unsupported and multiple files");
                typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(picture, null);
                Check((GetWindowLong(picture.Handle, -20) & 0x10) != 0, "native drop registration survives handle recreation");
                Call(control, "InitializeFileDrop", false);
                Check(picture.AllowDrop && Field(control, "_shellDropWindow") == null, "ordinary OLE drop restored and native hook released");
                var pending = (Task)Call(control, "LoadSourceFileAsync", Path.Combine(folder, "source.eps"));
                control.Dispose(); await pending;
                Check(control.IsDisposed, "dispose cancels pending inspection");
    }
}
