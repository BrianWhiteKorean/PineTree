using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using PineTree.PdfEngine;

internal static class SourcePreviewCacheRegression
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Save(string path, Color color)
    {
        using (var image = new Bitmap(80, 160))
        {
            using (var graphics = Graphics.FromImage(image)) graphics.Clear(color);
            image.Save(path, ImageFormat.Png);
        }
    }
    static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(args[0]);
            string source = Path.Combine(args[0], "cache-source.png");
            Save(source, Color.Red);
            var first = OrderDocumentService.PreviewAsync(source, 0, 800, false, CancellationToken.None).Result;
            first.PreviewImage.Dispose();
            var timer = Stopwatch.StartNew();
            var second = OrderDocumentService.PreviewAsync(source, 0, 800, false, CancellationToken.None).Result;
            Console.WriteLine("Cached preview: " + timer.ElapsedMilliseconds + " ms");
            using (second.PreviewImage)
            {
                Check(second.PreviewImage.Height == 800, "cached bitmap survives disposal by earlier caller");
                using (var bitmap = new Bitmap(second.PreviewImage)) Check(bitmap.GetPixel(200, 400).R > 240, "cached pixels");
            }
            // Hit must work even with the worker executable temporarily unavailable.
            string python = Directory.GetFiles(args[1], "python.exe", SearchOption.AllDirectories)[0], hidden = python + ".cache-test";
            File.Move(python, hidden);
            try
            {
                var hit = OrderDocumentService.PreviewAsync(source, 0, 800, false, CancellationToken.None).Result;
                hit.PreviewImage.Dispose();
                Check(OrderDocumentService.InspectAsync(source, CancellationToken.None).Result.PageCount == 1, "preview seeds inspection cache");
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Check(OrderDocumentService.PreviewPageAsync(source, 0, 800, false, cancelled.Token).Result.IsCanceled, "cached page respects cancellation");
                }
            }
            finally { File.Move(hidden, python); }
            Save(source, Color.Blue);
            File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(2));
            var changed = OrderDocumentService.PreviewAsync(source, 0, 800, false, CancellationToken.None).Result;
            using (changed.PreviewImage)
            using (var bitmap = new Bitmap(changed.PreviewImage)) Check(bitmap.GetPixel(200, 400).B > 240, "modified source invalidates cached preview");
            var resized = OrderDocumentService.PreviewAsync(source, 0, 400, false, CancellationToken.None).Result;
            using (resized.PreviewImage) Check(resized.PreviewImage.Height == 400, "resolution is part of cache identity");
            Console.WriteLine("PASS: cache reuse, ownership, cancellation, invalidation and resolution");
            if (args.Length > 2)
            {
                timer.Restart();
                OrderDocumentService.InspectAsync(args[2], CancellationToken.None).Wait();
                Console.WriteLine("AI metadata ready: " + timer.ElapsedMilliseconds + " ms");
                timer.Restart();
                var preview = OrderDocumentService.PreviewPageAsync(args[2], 0, 1600, false, CancellationToken.None).Result;
                preview.PreviewImage.Dispose();
                Console.WriteLine("AI background preview: " + timer.ElapsedMilliseconds + " ms");
                timer.Restart();
                OrderDocumentService.InspectAsync(args[2], CancellationToken.None).Wait();
                preview = OrderDocumentService.PreviewPageAsync(args[2], 0, 1600, false, CancellationToken.None).Result;
                preview.PreviewImage.Dispose();
                Console.WriteLine("AI repeat metadata + preview: " + timer.ElapsedMilliseconds + " ms");
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
