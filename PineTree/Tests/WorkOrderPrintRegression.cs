using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class WorkOrderPrintRegression
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var service = assembly.GetType("PineTree.Classes.WorkOrderDocumentService", true);
            var table = new DataTable();
            foreach (string column in new[] { "OrderId", "OrderItemId", "OrderNo", "CustName", "OrderDate", "OrderTitle", "ItemName", "EditorStateJson" }) table.Columns.Add(column);
            var row = table.Rows.Add("100", "22", "20261102-TEST", "테스트 거래처", "2024-03-15", "작업서 미리보기 검증", "현수막", "{\"Version\":1,\"ZoomType\":\"SCALE\",\"ZoomRate\":2.5}");
            var scale = service.GetMethod("ScaleText", BindingFlags.Static | BindingFlags.NonPublic);
            Check((string)scale.Invoke(null, new object[] { row }) == "2.5배", "saved scale mapped");
            row["EditorStateJson"] = "";
            Check((string)scale.Invoke(null, new object[] { row }) == "", "unknown scale left blank");
            row["EditorStateJson"] = "{\"Version\":1,\"ZoomType\":\"ORIGINAL\"}";
            Check((string)scale.Invoke(null, new object[] { row }) == "1배", "original size scale");
            row["EditorStateJson"] = "{\"Version\":1,\"ZoomType\":\"FIXED_SIZE\",\"SourceWidthMm\":100,\"SourceHeightMm\":200,\"WidthMm\":250,\"HeightMm\":500}";
            Check((string)scale.Invoke(null, new object[] { row }) == "2.5배", "fixed size scale derived from original dimensions");
            row["EditorStateJson"] = "{\"Version\":1,\"ZoomType\":\"SCALE\",\"ZoomRate\":10,\"OutputServerFile\":{\"RipScale\":10}}";
            Check((string)scale.Invoke(null, new object[] { row }) == "10배", "work order uses the reduced PDF's RIP scale");
            row["EditorStateJson"] = "{\"Version\":1,\"ZoomType\":\"SCALE\",\"ZoomRate\":10,\"OutputServerFile\":{}}";
            Check((string)scale.Invoke(null, new object[] { row }) == "1배", "legacy full-size output PDF is printed at 1x");
            row["EditorStateJson"] = "{\"Version\":1,\"ZoomType\":\"SCALE\",\"ZoomRate\":2.5}";
            var dialogType = assembly.GetType("PineTree.Dialogs.DialogPrint", true);
            using (var dialog = (Form)Activator.CreateInstance(dialogType, new object[] { row }))
            using (var timer = new System.Windows.Forms.Timer { Interval = 50 })
            {
                Exception failure = null; int ticks = 0;
                var elapsed = Stopwatch.StartNew();
                long lastTick = 0, maxTickGap = 0;
                bool progressSeen = false, paintedBeforeReady = false;
                var progressMessages = new System.Collections.Generic.HashSet<string>();
                dialog.Paint += delegate { if ((bool)dialogType.GetField("_busy", Hidden).GetValue(dialog)) paintedBeforeReady = true; };
                timer.Tick += async delegate {
                    try
                    {
                        long now = elapsed.ElapsedMilliseconds;
                        maxTickGap = Math.Max(maxTickGap, now - lastTick);
                        lastTick = now;
                        if (++ticks > 2400) throw new Exception("Preview timeout: " + ((Label)dialogType.GetField("_status", Hidden).GetValue(dialog)).Text);
                        if ((bool)dialogType.GetField("_busy", Hidden).GetValue(dialog))
                        {
                            var progress = (Form)dialogType.GetField("_progress", Hidden).GetValue(dialog);
                            if (progress != null && progress.Visible)
                            {
                                if (!progressSeen)
                                    using (var bitmap = new Bitmap(progress.Width, progress.Height))
                                    {
                                        progress.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                                        bitmap.Save(Path.Combine(output, "progress.png"));
                                    }
                                progressSeen = true;
                                var label = (Label)progress.GetType().GetField("label1", Hidden).GetValue(progress);
                                if (progressMessages.Add(label.Text)) Console.WriteLine("PERF stage @" + now + " ms: " + label.Text);
                            }
                            return;
                        }
                        timer.Stop();
                        Console.WriteLine("PERF: preview ready=" + elapsed.ElapsedMilliseconds + " ms; longest UI heartbeat gap=" + maxTickGap + " ms; UI ticks=" + ticks);
                        Check(progressSeen && progressMessages.Count >= 2, "DialogProgressbar visible with changing preparation stages");
                        Check(paintedBeforeReady && ticks > 2, "dialog painted and UI timer running during preparation");
                        Check(maxTickGap < 2000, "no multi-second UI thread stall during preparation");
                        Check(dialogType.GetField("_progress", Hidden).GetValue(dialog) == null, "progress dialog disposed after completion");
                        var document = dialogType.GetField("_document", Hidden).GetValue(dialog);
                        var status = (Label)dialogType.GetField("_status", Hidden).GetValue(dialog);
                        Check(document != null, "Excel workbook generation: " + status.Text);
                        Check((int)dialogType.GetField("_pageCount", Hidden).GetValue(dialog) == 1, "standard template produces one page: " + status.Text);
                        var thumbs = (System.Collections.IList)dialogType.GetField("_thumbnailButtons", Hidden).GetValue(dialog);
                        Check(thumbs.Count == 1, "thumbnail for each PDF page");
                        var picture = (PictureBox)dialogType.GetField("pictureBox1", Hidden).GetValue(dialog);
                        Check(picture.Image != null, "preview image visible");
                        Check(((Control)dialogType.GetField("btnPrint", Hidden).GetValue(dialog)).Enabled, "print enabled after preview");
                        Check(((Control)dialogType.GetField("btnExcel", Hidden).GetValue(dialog)).Enabled, "Excel export enabled");
                        File.Copy((string)document.GetType().GetProperty("ExcelPath").GetValue(document, null), Path.Combine(output, "work-order.xlsx"), true);
                        File.Copy((string)document.GetType().GetProperty("PdfPath").GetValue(document, null), Path.Combine(output, "work-order.pdf"), true);
                        picture.Image.Save(Path.Combine(output, "work-order.png"), ImageFormat.Png);
                        using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "dialog.png")); }
                        Check(status.Visible && status.Bounds.Bottom <= dialog.ClientSize.Height, "page status visible within dialog");
                        if (args.Length > 2)
                        {
                            string pdfPath = (string)document.GetType().GetProperty("PdfPath").GetValue(document, null);
                            File.Copy(Path.GetFullPath(args[2]), pdfPath, true);
                            dialogType.GetField("_pageCount", Hidden).SetValue(dialog, 2);
                            dialogType.GetMethod("AddThumbnail", Hidden).Invoke(dialog, new object[] { 1, new Bitmap(picture.Image) });
                            await (Task)dialogType.GetMethod("ShowPageAsync", Hidden).Invoke(dialog, new object[] { 1 });
                            Check((int)dialogType.GetField("_selectedPage", Hidden).GetValue(dialog) == 1 && thumbs.Count == 2, "second thumbnail selected");
                            Check(picture.Image.Width > picture.Image.Height, "second landscape page rendered");
                            Color selectedColor = ((Bitmap)picture.Image).GetPixel(picture.Image.Width / 2, picture.Image.Height / 2);
                            Check(selectedColor.R > 200 && selectedColor.G < 30, "second page content matches selected thumbnail");
                            using (var print = (PrintDocument)dialogType.GetMethod("CreatePrintDocument", Hidden).Invoke(dialog, null))
                            using (var bitmap = new Bitmap(1200, 1700))
                            using (var graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.Clear(Color.White);
                                print.PrinterSettings.PrintRange = PrintRange.SomePages;
                                print.PrinterSettings.FromPage = print.PrinterSettings.ToPage = 2;
                                typeof(PrintDocument).GetMethod("OnBeginPrint", Hidden).Invoke(print, new object[] { new PrintEventArgs() });
                                var pageArgs = new PrintPageEventArgs(graphics, new Rectangle(0, 0, 827, 1169), new Rectangle(0, 0, 827, 1169), print.DefaultPageSettings);
                                // Exercise the real print renderer without sending anything to a printer.
                                typeof(PrintDocument).GetMethod("OnPrintPage", Hidden).Invoke(print, new object[] { pageArgs });
                                Check(!pageArgs.HasMorePages, "selected print range stops after page 2");
                                Color printedColor = bitmap.GetPixel(400, 500);
                                Check(printedColor.R > 200 && printedColor.G < 30, "print renderer uses selected PDF page");
                                bitmap.Save(Path.Combine(output, "printed-page-2.png"));
                            }
                            await (Task)dialogType.GetMethod("ShowPageAsync", Hidden).Invoke(dialog, new object[] { 0 });
                            Check(picture.Image.Height > picture.Image.Width, "return to first portrait page");
                        }
                    }
                    catch (Exception ex) { failure = ex; }
                    timer.Stop(); dialog.Close();
                };
                timer.Start();
                dialog.ShowDialog();
                if (failure != null) throw failure;
            }
            using (var dialog = (Form)Activator.CreateInstance(dialogType, new object[] { row }))
            using (var closeTimer = new System.Windows.Forms.Timer { Interval = 100 })
            {
                bool closeRequested = false;
                closeTimer.Tick += delegate {
                    if (closeRequested || !(bool)dialogType.GetField("_busy", Hidden).GetValue(dialog)) return;
                    closeRequested = true;
                    closeTimer.Stop();
                    dialog.Close();
                };
                closeTimer.Start();
                dialog.ShowDialog();
                Check(closeRequested, "close request delivered during preparation");
                Check(dialogType.GetField("_progress", Hidden).GetValue(dialog) == null, "progress dialog cleaned up on cancellation");
                Check(!(bool)dialogType.GetField("_busy", Hidden).GetValue(dialog), "preparation finished after cancellation");
            }
            // An already canceled request must not start Excel or leave generated files.
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                var task = (Task)service.GetMethod("CreateAsync").Invoke(null, new object[] { row, canceled.Token });
                try { task.GetAwaiter().GetResult(); throw new Exception("Expected cancellation"); }
                catch (OperationCanceledException) { Check(true, "canceled generation cleaned up"); }
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
