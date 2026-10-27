using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

internal static class UpdateRegression
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
        Console.WriteLine("PASS: " + message);
    }

    private static int Main(string[] args)
    {
        try
        {
            Assembly debug = Assembly.LoadFile(Path.GetFullPath(args[1]));
            Check(debug.GetType("PineTreeLauncher.ProgramUpdater") == null,
                "Debug contains no updater code");
            Assembly release = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Check(!release.GetReferencedAssemblies().Any(a => a.Name == "PineTree"),
                "Launcher does not load PineTree through a static reference");
            Type updater = release.GetType("PineTreeLauncher.ProgramUpdater", true);
            if (args.Length > 4 && args[4] == "--database")
            {
                object registered = updater.GetMethod("GetLatestUpdate", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
                Check(registered != null, "Release can read the actual ProgramUpdate record");
            }
            Type infoType = updater.GetNestedType("UpdateInfo", BindingFlags.NonPublic);
            object info = Activator.CreateInstance(infoType, true);
            Action<string, object> set = (name, value) => infoType.GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(info, value);
            MethodInfo apply = updater.GetMethod("ApplyUpdate", BindingFlags.Static | BindingFlags.NonPublic);
            string dll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PineTree.dll");
            string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "source", "PineTree.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(source));
            File.Copy(args[2], dll, true);
            File.Copy(args[3], source, true);
            set("Version", "2026.10.24.01");
            set("Url", new Uri(source).AbsoluteUri);
            set("Required", true);
            set("Hash", "incorrect-hash");
            bool rejected = false;
            try { apply.Invoke(null, new[] { info }); }
            catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
            Check(rejected && AssemblyName.GetAssemblyName(dll).Version == new Version("1.0.0.0"),
                "Hash failure preserves the old DLL");
            Check(!File.Exists(dll + ".download"), "Failed download is cleaned up");
            set("Version", "2026.10.25.1");
            set("Hash", null);
            rejected = false;
            try { apply.Invoke(null, new[] { info }); }
            catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
            Check(rejected && AssemblyName.GetAssemblyName(dll).Version == new Version("1.0.0.0"),
                "Version mismatch preserves the old DLL");
            string hash;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(source))
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            set("Version", "2026.10.24.01");
            set("Hash", hash);
            apply.Invoke(null, new[] { info });
            Check(AssemblyName.GetAssemblyName(dll).Version == new Version("2026.10.24.1"),
                "BIN download is installed as the new DLL");
            Check(AssemblyName.GetAssemblyName(dll + ".bak").Version == new Version("1.0.0.0"),
                "Replacement keeps a backup of the previous DLL");
            set("Url", "file:///does-not-exist.bin");
            apply.Invoke(null, new[] { info });
            Check(true, "Equal version skips download");
            set("Version", "1.0.0.0");
            apply.Invoke(null, new[] { info });
            Check(AssemblyName.GetAssemblyName(dll).Version == new Version("2026.10.24.1"),
                "Newer local DLL is not downgraded");
            File.Delete(dll);
            set("Url", new Uri(source).AbsoluteUri);
            set("Version", "2026.10.24.1");
            apply.Invoke(null, new[] { info });
            Check(File.Exists(dll), "Missing local DLL is downloaded");
            File.Copy(args[2], dll, true);
            apply.Invoke(null, new[] { info });
            Check(AssemblyName.GetAssemblyName(dll).Version == new Version("2026.10.24.1"),
                "A later replacement succeeds when a backup already exists");
            Console.WriteLine("Passed " + checks + " checks.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
