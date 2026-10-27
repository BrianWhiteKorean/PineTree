using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Intentionally has no compile-time reference to either application assembly.
internal static class PdfEngineDeploymentRegression
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Console.WriteLine("PASS: " + message);
    }

    private static object Await(object value)
    {
        var task = (Task)value;
        task.GetAwaiter().GetResult();
        return value.GetType().GetProperty("Result").GetValue(value, null);
    }

    private static object Invoke(Type service, string method, params object[] args)
    {
        return service.GetMethod(method).Invoke(null, args);
    }

    private static int Main(string[] args)
    {
        try
        {
            string application = Path.GetFullPath(args[0]);
            string source = Path.GetFullPath(args[1]);
            string output = Path.GetFullPath(args[2]);
            Check(!Directory.Exists(Path.Combine(Path.GetDirectoryName(application), "Tools", "PdfWorker")), "No external PdfWorker folder");
            Check(!File.Exists(Path.Combine(Path.GetDirectoryName(application), "PineTree.PdfEngine.dll")), "No adjacent PdfEngine DLL");
            var app = Assembly.LoadFrom(application);
            RuntimeHelpers.RunModuleConstructor(app.ManifestModule.ModuleHandle);
            var engine = Assembly.Load("PineTree.PdfEngine");
            Check(string.IsNullOrEmpty(engine.Location), "Costura loaded embedded PdfEngine from memory");
            var service = engine.GetType("PineTree.PdfEngine.OrderDocumentService", true);
            var settingsType = engine.GetType("PineTree.PdfEngine.PdfGenerationSettings", true);
            if (args.Length > 3 && args[3] == "cancel-prepare")
            {
                using (var cancel = new CancellationTokenSource(100))
                {
                    bool cancelled = false;
                    try { Await(Invoke(service, "InspectAsync", source, cancel.Token)); }
                    catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled, "Cold runtime preparation can be cancelled");
                }
                return 0;
            }
            var first = Invoke(service, "InspectAsync", source, CancellationToken.None);
            var second = Invoke(service, "InspectAsync", source, CancellationToken.None);
            foreach (object pending in new[] { first, second })
            {
                var info = Await(pending);
                Check((bool)info.GetType().GetProperty("Success").GetValue(info, null), "Concurrent inspection succeeds");
            }
            if (args.Length > 3 && args[3] == "inspect") return 0;
            var preview = Await(Invoke(service, "PreviewAsync", source, 0, 800, false, CancellationToken.None));
            using (var image = (IDisposable)preview.GetType().GetProperty("PreviewImage").GetValue(preview, null))
                Check(image != null, "Preview image returned");
            var settings = Activator.CreateInstance(settingsType);
            settingsType.GetProperty("MakeCutline").SetValue(settings, true, null);
            settingsType.GetProperty("SpotColorName").SetValue(settings, "deployment-test-cut", null);
            var generated = Await(Invoke(service, "GenerateAsync", source, output, settings, CancellationToken.None));
            Check(File.Exists(output) && new FileInfo(output).Length > 0, "PDF generated using embedded worker");
            Check(Math.Abs((double)generated.GetType().GetProperty("OutputWidthMm").GetValue(generated, null) - 604) < 0.000001, "Generation settings survive DLL boundary");
            string sentinel = output + ".cancel.pdf";
            File.WriteAllText(sentinel, "keep-existing-output");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                bool cancelled = false;
                try { Await(Invoke(service, "GenerateAsync", source, sentinel, settings, cancel.Token)); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && File.ReadAllText(sentinel) == "keep-existing-output", "Cancellation preserves existing output");
            }
            File.Delete(sentinel);
            Console.WriteLine("Deployment checks passed: " + checks);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
