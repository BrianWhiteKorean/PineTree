using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Run in a fresh process so a previous PDF operation cannot hide eager engine loading.
internal static class BannerOrderInitializationRegression
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void CheckEngineNotLoaded(string stage)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "PineTree.PdfEngine")
                throw new Exception("PDF engine loaded during " + stage);
    }
    private static void Measure(string stage, Action action)
    {
        var clock = Stopwatch.StartNew(); action();
        Console.WriteLine(stage + ": " + clock.ElapsedMilliseconds + " ms");
    }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var type = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("PineTree.UserControlsOrder.UcOrder현수막", true);
            CheckEngineNotLoaded("type lookup");
            for (int i = 0; i < 3; i++)
            {
                UserControl control = null;
                Measure("Full constructor " + (i + 1), () => control = (UserControl)Activator.CreateInstance(type));
                using (control)
                using (var host = new Form { Width = 1170, Height = 500, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Left = -4000, Top = -4000 })
                {
                    CheckEngineNotLoaded("constructor");
                    Measure("Attach and show " + (i + 1), () => { control.Dock = DockStyle.Fill; host.Controls.Add(control); host.Show(); Application.DoEvents(); });
                    CheckEngineNotLoaded("first display");
                    foreach (string name in new[] { "tbTargetWidth", "tbTargetHeight", "inputNumericBox3", "inputNumericBox4" })
                    {
                        var input = type.GetField(name, Flags).GetValue(control);
                        if (Convert.ToDecimal(input.GetType().GetProperty("Value").GetValue(input, null)) != 0) throw new Exception("Initial values must be zero");
                    }
                    var height = type.GetField("tbTargetHeight", Flags).GetValue(control);
                    height.GetType().GetProperty("Value").SetValue(height, 1800m, null);
                    var quantity = type.GetField("inputNumericBox4", Flags).GetValue(control);
                    quantity.GetType().GetProperty("Value").SetValue(quantity, 1m, null);
                    var width = type.GetField("tbTargetWidth", Flags).GetValue(control);
                    Measure("Edit width " + (i + 1), () => width.GetType().GetProperty("Value").SetValue(width, 700m, null));
                    var area = type.GetField("inputNumericBox3", Flags).GetValue(control);
                    if (Convert.ToDecimal(area.GetType().GetProperty("Value").GetValue(area, null)) != 1.26m)
                        throw new Exception("Editing did not update area");
                    CheckEngineNotLoaded("editing");
                }
            }
            Console.WriteLine("PASS: constructor, display and editing never load the PDF engine");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
