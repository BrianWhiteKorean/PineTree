using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

// Read-only integration checks against the configured DB; no saves or DB writes.
internal static class PriceItemEquipmentFilterRegression
{
    private static Assembly app;
    private static Type repo;
    private static int checks;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static object Field(object x, string name) { return x.GetType().GetField(name, Flags).GetValue(x); }
    private static object Get(object x, string name) { return x.GetType().GetProperty(name).GetValue(x, null); }
    private static void Set(object x, string name, object value) { x.GetType().GetProperty(name).SetValue(x, value, null); }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); ++checks; }
    private static DataTable Query(string name, params object[] args)
    {
        return (DataTable)repo.GetMethods().Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
    }
    private static DataTable Sql(string sql)
    {
        using (var conn = (DbConnection)app.GetType("PineTree.Classes.DB").GetMethod("CreateConnection").Invoke(null, null))
        using (var cmd = conn.CreateCommand())
        {
            conn.Open(); cmd.CommandText = sql;
            using (var reader = cmd.ExecuteReader()) { var t = new DataTable(); t.Load(reader); return t; }
        }
    }
    private static HashSet<string> Values(DataTable t, string col)
    { return new HashSet<string>(t.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[col]))); }
    private static HashSet<string> Options(object combo)
    { return new HashSet<string>(((IEnumerable)Get(combo, "Items")).Cast<object>().Select(Convert.ToString)); }
    private static Task Call(object view, string method)
    { return (Task)view.GetType().GetMethod(method, Flags).Invoke(view, null); }
    private static async Task Wait(Func<bool> ready)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!ready()) { if (DateTime.UtcNow > until) throw new TimeoutException("Filter cascade did not finish"); await Task.Delay(30); }
    }
    private static async Task Run(Control view)
    {
        var equipment = Field(view, "cbEquipment"); var category = Field(view, "cbCategory");
        var categoryName = Field(view, "cbCategoryName"); var select = Field(view, "btnSelect");
        await Wait(() => (bool)Get(equipment, "Enabled") && (bool)Get(select, "Enabled") && Field(view, "_priceItems") != null);
        Check(Options(equipment).SetEquals(new[] { "[전체]", "수성", "라텍스" }), "Equipment labels must be readable");
        var devices = Query("GetEquipment");
        foreach (DataRow device in devices.Rows)
        {
            int id = Convert.ToInt32(device["EquipmentId"]); string name = Convert.ToString(device["EquipmentName"]);
            var items = ((IEnumerable)Get(equipment, "Items")).Cast<object>().ToList();
            int index = -1; for (int i = 0; i < items.Count; i++) if (Convert.ToString(items[i]) == name) index = i;
            Check(index > 0, "Equipment option missing"); Set(equipment, "SelectedIndex", index);
            await Wait(() => (bool)Get(select, "Enabled"));
            Check(Convert.ToInt32(view.GetType().GetMethod("GetSelectedEquipmentId", Flags).Invoke(view, null)) == id, "Equipment ID must match selection");
            var expectedCategories = Values(Sql("SELECT DISTINCT pi.PriceCategory FROM PriceItems pi JOIN PriceItemEquipment pie ON pie.PriceItemSeqNo=pi.SeqNo WHERE pi.IsActive=1 AND pie.EquipmentId=" + id), "PriceCategory");
            expectedCategories.Add("[전체]");
            Check(Options(category).SetEquals(expectedCategories), "Category options must match equipment links");
            Check(expectedCategories.Contains("포맥스&폼보드"), "Common category must be present for both equipment types");
            var categoryItems = ((IEnumerable)Get(category, "Items")).Cast<object>().ToList();
            for (int i = 0; i < categoryItems.Count; i++)
            {
                Set(category, "SelectedIndex", i); await Wait(() => (bool)Get(select, "Enabled"));
                string cat = Convert.ToString(Get(category, "Text"));
                var expectedNames = Values(Query("GetCategoryNames", cat == "[전체]" ? null : cat, id), "CategoryName"); expectedNames.Add("[전체]");
                Check(Options(categoryName).SetEquals(expectedNames), "Subcategory options must match equipment and category");
                await Call(view, "LoadPriceItemsAsync");
                var actual = (DataTable)Field(view, "_priceItems");
                string catClause = cat == "[전체]" ? "" : " AND pi.PriceCategory='" + cat.Replace("'", "''") + "'";
                var expected = Sql("SELECT pi.SeqNo FROM PriceItems pi JOIN PriceItemEquipment pie ON pie.PriceItemSeqNo=pi.SeqNo WHERE pi.IsActive=1 AND (pi.CustCode IS NULL OR pi.CustCode='') AND pie.EquipmentId=" + id + catClause);
                Check(Values(actual, "SeqNo").SetEquals(Values(expected, "SeqNo")), "Grid must match equipment/category/basic customer scope");
                Check(actual.Rows.Count == Values(actual, "SeqNo").Count, "Common items must not be duplicated");
            }
            // Verify server-side combinations against an independent SQL query.
            var expectedDedicated = Sql("SELECT pi.SeqNo FROM PriceItems pi JOIN PriceItemEquipment pie ON pie.PriceItemSeqNo=pi.SeqNo WHERE pi.IsActive=1 AND pi.CustCode='85' AND pi.PriceItemName LIKE '%현수막%' AND pie.EquipmentId=" + id);
            Check(Values(Query("GetPriceItemsForCustomer", null, null, "현수막", "85", id), "SeqNo").SetEquals(Values(expectedDedicated, "SeqNo")), "Equipment/name/dedicated-customer combination");
        }
        Set(equipment, "SelectedIndex", 1); Set(equipment, "SelectedIndex", 2); Set(equipment, "SelectedIndex", 1);
        await Wait(() => (bool)Get(select, "Enabled"));
        int finalId = Convert.ToInt32(view.GetType().GetMethod("GetSelectedEquipmentId", Flags).Invoke(view, null));
        var finalCategories = Values(Query("GetPriceCategories", finalId), "PriceCategory"); finalCategories.Add("[전체]");
        Check(Options(category).SetEquals(finalCategories), "Rapid equipment switches must retain final selection results");
        Set(equipment, "SelectedIndex", 0); await Wait(() => (bool)Get(select, "Enabled"));
        await Call(view, "LoadPriceItemsAsync");
        Check(Values((DataTable)Field(view, "_priceItems"), "SeqNo").SetEquals(Values(Query("GetPriceItemsForCustomer", null, null, "", ""), "SeqNo")), "All equipment must preserve existing search behavior");
        Console.WriteLine("PASS: " + checks + " equipment cascade and combined-query checks (read-only)");
    }
    [STAThread] private static int Main(string[] args)
    {
        int exit = 0;
        try
        {
            Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            app = Assembly.LoadFrom(Path.GetFullPath(args[0])); repo = app.GetType("PineTree.Classes.PriceItemRepository", true);
            using (var view = (Control)Activator.CreateInstance(app.GetType("PineTree.UserControls.UcPriceItems", true)))
            using (var form = new Form { ClientSize = new Size(1500, 900), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Left = -4000, Top = -4000 })
            {
                view.Dock = DockStyle.Fill; form.Controls.Add(view);
                form.Shown += async delegate { try { await Run(view); } catch (Exception e) { Console.Error.WriteLine(e); exit = 1; } finally { form.Close(); } };
                Application.Run(form);
            }
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        return exit;
    }
}
