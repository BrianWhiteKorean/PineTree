using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class StandaloneUpdateRegression
{
    private static int Main()
    {
        try
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.GetFiles(directory, "*.config").Length != 0 ||
                Directory.GetFiles(directory, "*.dll").Length != 0)
                throw new Exception("Test must start without external config or DLL files.");
            Assembly launcher = Assembly.LoadFrom(Path.Combine(directory, "PineTreeLauncher.exe"));
            Type updater = launcher.GetType("PineTreeLauncher.ProgramUpdater", true);
            updater.GetMethod("UpdateIfNeeded", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            string dllPath = Path.Combine(directory, "PineTree.dll");
            AssemblyName downloaded = AssemblyName.GetAssemblyName(dllPath);
            if (downloaded.Name != "PineTree") throw new Exception("Wrong downloaded assembly.");
            Console.WriteLine("PASS: EXE-only setup reads embedded DB settings and downloads PineTree.dll.");
            Console.WriteLine("Downloaded version: " + downloaded.Version);
            Assembly pine = Assembly.LoadFrom(dllPath);
            RuntimeHelpers.RunModuleConstructor(pine.ManifestModule.ModuleHandle);
            if (pine.GetType("PineTree.Forms.FormMain", true) == null) throw new Exception("Missing main form.");
            Assembly.Load("PineTree.PdfEngine").GetType("PineTree.PdfEngine.OrderDocumentService", true);
            Console.WriteLine("PASS: Downloaded DLL exposes FormMain and resolves PdfEngine.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
