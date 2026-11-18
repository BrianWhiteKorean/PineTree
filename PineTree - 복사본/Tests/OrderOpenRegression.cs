using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Compile with csc /r:System.Windows.Forms.dll /r:System.Data.dll.
// Pass PineTree.dll as the first argument. Uses read-only DB queries.
internal static class OrderOpenRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Type dashboardType = assembly.GetType("PineTree.UserControls.UcDashboardHome", true);
            if (args.Length > 1 && args[1] == "--expect-connection-failure")
            {
                using (var dashboard = (Control)Activator.CreateInstance(dashboardType))
                {
                    bool failed = false;
                    try { dashboardType.GetMethod("ShowNewOrderDialog", PrivateInstance).Invoke(dashboard, null); }
                    catch (TargetInvocationException ex)
                    {
                        Exception cause = ex.GetBaseException();
                        Check(cause is System.Net.Sockets.SocketException, "Expected blocked DB connection");
                        failed = true;
                    }
                    Check(failed, "Connection failure occurred");
                    Check(!Application.UseWaitCursor, "Cursor restored on constructor failure");
                    Check(!(bool)dashboardType.GetField("_isOpeningNewOrder", PrivateInstance).GetValue(dashboard), "Guard released on failure");
                }
                Console.WriteLine("PASS: cursor and duplicate-open guard restored after DB connection failure");
                return 0;
            }
            Type repository = assembly.GetType("PineTree.Classes.CommonCodeRepository", true);
            using (DataTable combined = (DataTable)repository.GetMethod("GetOrderEntryItems").Invoke(null, null))
            {
                foreach (string group in new[] { "주문채널", "배송방법" })
                using (DataTable original = (DataTable)repository.GetMethod("GetItems").Invoke(null, new object[] { group }))
                {
                    DataRow[] rows = combined.Select("GroupName = '" + group + "'", "SeqNo");
                    Check(rows.Length == original.Rows.Count, "Combined lookup row count: " + group);
                    for (int i = 0; i < rows.Length; i++)
                    {
                        Check(Convert.ToString(rows[i]["SeqNo"]) == Convert.ToString(original.Rows[i]["SeqNo"]), "Row order");
                        Check(Convert.ToString(rows[i]["ItemName"]) == Convert.ToString(original.Rows[i]["ItemName"]), "Item text");
                    }
                }
            }

            using (var dashboard = (Control)Activator.CreateInstance(dashboardType))
            {
                foreach (bool previousWait in new[] { false, true, false })
                {
                    Application.UseWaitCursor = previousWait;
                    Exception failure = null;
                    bool inspected = false;
                    using (var timer = new Timer { Interval = 50 })
                    {
                        timer.Tick += delegate
                        {
                            foreach (Form form in Application.OpenForms)
                            {
                                if (form.GetType().FullName != "PineTree.Dialogs.DialogOrder") continue;
                                timer.Stop();
                                try
                                {
                                    Check(Application.UseWaitCursor == previousWait, "Wait cursor restored when dialog is shown");
                                    Check(form.UseWaitCursor == previousWait, "Dialog cursor restored");
                                    Check((bool)dashboardType.GetField("_isOpeningNewOrder", PrivateInstance).GetValue(dashboard), "Duplicate-open guard active");
                                    dashboardType.GetMethod("ShowNewOrderDialog", PrivateInstance).Invoke(dashboard, null);
                                    inspected = true;
                                }
                                catch (Exception ex) { failure = ex; }
                                form.DialogResult = DialogResult.Cancel;
                                form.Close();
                                break;
                            }
                        };
                        timer.Start();
                        var clock = Stopwatch.StartNew();
                        dashboardType.GetMethod("ShowNewOrderDialog", PrivateInstance).Invoke(dashboard, null);
                        Console.WriteLine("Open and cancel (previous wait=" + previousWait + "): " + clock.ElapsedMilliseconds + " ms");
                    }
                    if (failure != null) throw failure;
                    Check(inspected, "Modal dialog inspected");
                    Check(Application.UseWaitCursor == previousWait, "Cursor preserved after cancel");
                    Check(!(bool)dashboardType.GetField("_isOpeningNewOrder", PrivateInstance).GetValue(dashboard), "Duplicate-open guard released");
                }
            }
            Console.WriteLine("PASS: lookup contents/order, cursor restoration, cancel, repeated opens, duplicate-open guard");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Application.UseWaitCursor = false; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
