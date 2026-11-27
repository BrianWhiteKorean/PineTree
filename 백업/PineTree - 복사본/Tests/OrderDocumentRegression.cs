using PineTree.PdfEngine;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

internal static class OrderDocumentRegression
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static object Field(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
    private static string Text(object component)
    {
        return Convert.ToString(component.GetType().GetProperty("Text").GetValue(component, null));
    }
    private static void Text(object component, string value)
    {
        component.GetType().GetProperty("Text").SetValue(component, value, null);
    }
    private static void PumpUntil(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            Application.DoEvents();
            Thread.Sleep(20);
            if (watch.Elapsed > TimeSpan.FromSeconds(40)) throw new TimeoutException("UI operation timed out");
        }
    }
    private static string Hash(string path)
    {
        using (var hash = SHA256.Create()) using (var file = File.OpenRead(path))
            return BitConverter.ToString(hash.ComputeHash(file));
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string source = Path.GetFullPath(args[0]);
            string output = Path.GetFullPath(args[1]);
            string sourceHash = Hash(source);
            var details = OrderDocumentService.InspectAsync(source, CancellationToken.None).GetAwaiter().GetResult();
            Check(details.Success && details.FileType.StartsWith("AI : "), "C# metadata protocol");
            Check(details.FileInfo.Contains("60.00×180.00"), "design dimensions in file information");
            var preview = OrderDocumentService.PreviewAsync(source, 0, 1000, false, CancellationToken.None).GetAwaiter().GetResult();
            using (preview.PreviewImage)
                Check(preview.PreviewImage.Height > preview.PreviewImage.Width && preview.PreviewImage.Height <= 1002, "preview detached from temporary file");
            var result = OrderDocumentService.GenerateSampleAsync(source, output, CancellationToken.None).GetAwaiter().GetResult();
            Check(result.OutputPath == output && File.Exists(output), "async generation and atomic file publication");
            Check(new FileInfo(output).Length > 100000, "generated sample size");
            string sentinel = Path.Combine(Path.GetDirectoryName(output), "cancel-test.pdf");
            File.WriteAllText(sentinel, "preserve-existing-output");
            try
            {
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.CancelAfter(100);
                    bool cancelled = false;
                    try { OrderDocumentService.GenerateSampleAsync(source, sentinel, cancellation.Token).GetAwaiter().GetResult(); }
                    catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled && File.ReadAllText(sentinel) == "preserve-existing-output", "cancelled worker must preserve existing output");
                }
            }
            finally { File.Delete(sentinel); }
            bool blocked = false;
            try { OrderDocumentService.GenerateSampleAsync(source, source, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (ArgumentException) { blocked = true; }
            Check(blocked && Hash(source) == sourceHash, "source overwrite blocked");
            using (var form = new DialogOrderInfoEdit())
            {
                Text(Field(form, "tbSourceFile"), source);
                PumpUntil(() => Field(form, "_sourceDetails") != null || Text(Field(form, "tbFileType")).Contains("확인 필요"));
                Check(Text(Field(form, "tbSourceFile")) == source && Text(Field(form, "tbFileType")).StartsWith("AI : "), "order dialog source metadata binding");
                Check(Text(Field(form, "tbFileInfo")).Contains("60.00×180.00"), "order dialog file properties binding");
                Check((bool)Field(form, "btnPreview1").GetType().GetProperty("Enabled").GetValue(Field(form, "btnPreview1"), null), "preview button enabled after analysis");
                Text(Field(form, "inputTextBox2"), output);
                var item = (DialogPriceItemOrderItem)form.GetType().GetMethod("CreateOrderItem", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                Check(item.RequestFilePath == source && item.OutputFilePath == output, "existing order save mapping preserves file paths");
            }
            using (var form = new DialogDocumentPreview(source))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-4000, -4000);
                form.ShowInTaskbar = false;
                form.Show();
                var picture = (PictureBox)Field(form, "_picture");
                PumpUntil(() => picture.Image != null || Text(Field(form, "_status")).StartsWith("미리보기 실패"));
                Check(picture.Image != null, "PictureBox asynchronous image display");
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.ChangeExtension(output, ".preview-window.png"));
                }
            }
            Check(Hash(source) == sourceHash, "original AI unchanged after all operations");
            Console.WriteLine("Order document integration checks passed: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
