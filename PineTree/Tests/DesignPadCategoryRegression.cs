using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

// Reads PriceItems only; no login, order save, or database writes.
internal static class DesignPadCategoryRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Assembly app;
    private static Type repository;
    private static int checks;
    private static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    private static object Get(object target, string name) { return target.GetType().GetProperty(name, Flags).GetValue(target, null); }
    private static void Set(object target, string name, object value) { target.GetType().GetProperty(name, Flags).SetValue(target, value, null); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    private static DataTable Query(string name, params object[] args)
    {
        return (DataTable)Array.Find(repository.GetMethods(), m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); ++checks; }
    private static HashSet<string> Values(DataTable table, string column)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (DataRow row in table.Rows) { string value = Convert.ToString(row[column]).Trim(); if (value.Length > 0) values.Add(value); }
        return values;
    }
    private static HashSet<string> ComboValues(object combo)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (object option in (IEnumerable)Get(combo, "Items")) values.Add(Convert.ToString(Get(option, "Text")));
        return values;
    }
    private static string[] ComboSequence(object combo)
    {
        return ((IEnumerable)Get(combo, "Items")).Cast<object>().Select(x => Convert.ToString(Get(x, "Text"))).ToArray();
    }
    private static string[] ExpectedItemOrder(int equipmentId, string priceCategory, string categoryName)
    {
        // Independently sort raw records in C#, rather than using the repository's ordering.
        using (var conn = (DbConnection)app.GetType("PineTree.Classes.DB").GetMethod("CreateConnection").Invoke(null, null))
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT pi.PriceItemName,pi.SortOrder,pi.SeqNo FROM PriceItems pi
WHERE pi.IsActive=1 AND pi.ItemType='PRODUCT' AND pi.PriceCategory=@Category AND pi.CategoryName=@Name
AND pi.PriceItemName IS NOT NULL AND TRIM(pi.PriceItemName)<>''
AND EXISTS (SELECT 1 FROM PriceItemEquipment pie JOIN Equipment e ON e.EquipmentId=pie.EquipmentId
WHERE pie.PriceItemSeqNo=pi.SeqNo AND e.EquipmentId=@Equipment AND e.IsActive=1)";
            foreach (var value in new object[][] { new object[] { "@Category", priceCategory }, new object[] { "@Name", categoryName }, new object[] { "@Equipment", equipmentId } })
            {
                var p = cmd.CreateParameter(); p.ParameterName = (string)value[0]; p.Value = value[1]; cmd.Parameters.Add(p);
            }
            conn.Open(); var table = new DataTable();
            using (var reader = cmd.ExecuteReader()) table.Load(reader);
            return table.Rows.Cast<DataRow>()
                .GroupBy(r => Convert.ToString(r["PriceItemName"]).Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.All(r => r.IsNull("SortOrder")))
                .ThenBy(g => g.Where(r => !r.IsNull("SortOrder")).Select(r => Convert.ToInt32(r["SortOrder"])).DefaultIfEmpty(int.MaxValue).Min())
                .ThenBy(g => g.Min(r => Convert.ToInt64(r["SeqNo"])))
                .Select(g => g.Key).ToArray();
        }
    }
    private static async Task Wait(Func<bool> ready)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(30);
        while (!ready()) { if (DateTime.UtcNow > limit) throw new TimeoutException("Category cascade did not finish"); await Task.Delay(30); }
    }
    private static async Task VerifyItems(object pad, int equipmentId, string priceCategory, string categoryName)
    {
        HashSet<string> expected;
        using (var rows = Query("GetPriceItemNames", priceCategory, categoryName, equipmentId)) expected = Values(rows, "PriceItemName");
        bool hasItems = expected.Count > 0;
        expected.Add("[선택]");
        var combo = Field(pad, "cbItem");
        await Wait(() => ComboValues(combo).SetEquals(expected) && (bool)Get(combo, "Enabled") == hasItems);
        Check(ComboValues(combo).SetEquals(expected), "Items must match selected equipment and both categories");
        var expectedOrder = ExpectedItemOrder(equipmentId, priceCategory, categoryName);
        Check(ComboSequence(combo).SequenceEqual(new[] { "[선택]" }.Concat(expectedOrder)), "cbItem must follow numeric SortOrder, with duplicate names shown once");
        Check((int)Get(combo, "SelectedIndex") == 0 && (string)Get(combo, "Text") == "[선택]", "Item selection must reset to the placeholder");
        var editor = Field(pad, "m_orderEditor");
        bool usesGroupA = categoryName == "현수막" || (priceCategory == "수성" && categoryName == "합성지");
        bool usesLatexGroupA = priceCategory == "라텍스"
            && (categoryName == "라텍스 현수막" || categoryName == "라텍스현수막" || categoryName == "현수막");
        string expectedEditor = usesLatexGroupA ? "UcOrder라텍스_그룹A"
            : usesGroupA ? "UcOrder수성_그룹A"
            : categoryName == "페트" && priceCategory == "수성" ? "UcOrder수성_그룹B" : null;
        Check(expectedEditor == null ? editor == null : editor != null && editor.GetType().Name == expectedEditor,
            "Category must select the matching supported editor");
        if (editor != null)
            Check(Object.ReferenceEquals(Get(editor, "SourceDocument"), Field(pad, "m_sourceDocument")), "Editor must retain the shared source");
        if (hasItems) Set(combo, "SelectedIndex", 1);
    }
    private static async Task Run(object pad, string output)
    {
        var categories = Field(pad, "cbCategoryName");
        var priceCategories = Field(pad, "cbCategory");
        var items = Field(pad, "cbItem");
        Check(!(bool)Get(priceCategories, "Enabled") && !(bool)Get(categories, "Enabled") && !(bool)Get(items, "Enabled"), "Dependent combos initially disabled");
        var sourceDocument = Field(pad, "m_sourceDocument");
        string[] equipmentNames = { "수성", "라텍스" };
        int categoryCount = 0;
        int firstEquipmentId = 0;
        for (int radioIndex = 1; radioIndex <= 2; ++radioIndex)
        {
            string equipmentName = equipmentNames[radioIndex - 1];
            int equipmentId = 0;
            using (var devices = Query("GetEquipment"))
                foreach (DataRow row in devices.Rows)
                    if (Convert.ToString(row["EquipmentName"]) == equipmentName) equipmentId = Convert.ToInt32(row["EquipmentId"]);
            Check(equipmentId > 0, "Equipment must exist in DB");
            if (radioIndex == 1) firstEquipmentId = equipmentId;
            HashSet<string> expected;
            using (var rows = Query("GetPriceCategories", equipmentId)) expected = Values(rows, "PriceCategory");
            Set(Field(pad, "rbEquipment" + radioIndex), "Checked", true);
            Check((string)Call(pad, "GetSelectedEquipmentName") == equipmentName, "Radio must identify EquipmentName");
            await Wait(() => ComboValues(priceCategories).SetEquals(expected) && (bool)Get(priceCategories, "Enabled") == (expected.Count > 0));
            Check(ComboValues(priceCategories).SetEquals(expected), "PriceCategory list must match selected equipment");
            Check(expected.Contains("포맥스&폼보드"), "Common category must appear for both equipment types");
            int priceCount = Convert.ToInt32(Get(Get(priceCategories, "Items"), "Count"));
            for (int priceIndex = 0; priceIndex < priceCount; ++priceIndex)
            {
                Set(priceCategories, "SelectedIndex", priceIndex);
                string priceCategory = Convert.ToString(Get(priceCategories, "Text"));
                HashSet<string> expectedNames;
                using (var rows = Query("GetCategoryNames", priceCategory, equipmentId)) expectedNames = Values(rows, "CategoryName");
                await Wait(() => ComboValues(categories).SetEquals(expectedNames) && (bool)Get(categories, "Enabled") == (expectedNames.Count > 0));
                Check((string)Call(pad, "GetSelectedPriceCategory") == priceCategory, "PriceCategory must come from cbCategory");
                int count = Convert.ToInt32(Get(Get(categories, "Items"), "Count"));
                for (int index = 0; index < count; ++index)
                {
                    Set(categories, "SelectedIndex", index);
                    string categoryName = Convert.ToString(Get(categories, "Text"));
                    await VerifyItems(pad, equipmentId, priceCategory, categoryName);
                    Check(Object.ReferenceEquals(sourceDocument, Field(pad, "m_sourceDocument")), "Cascade must retain analyzed source document");
                    ++categoryCount;
                }
            }
        }
        // Two quick changes must leave the final radio's data displayed.
        Set(Field(pad, "rbEquipment1"), "Checked", true);
        Set(Field(pad, "rbEquipment2"), "Checked", true);
        Set(Field(pad, "rbEquipment1"), "Checked", true);
        HashSet<string> finalCategories;
        using (var rows = Query("GetPriceCategories", firstEquipmentId)) finalCategories = Values(rows, "PriceCategory");
        await Wait(() => ComboValues(priceCategories).SetEquals(finalCategories));
        string finalPriceCategory = Convert.ToString(Get(priceCategories, "Text"));
        HashSet<string> finalNames;
        using (var rows = Query("GetCategoryNames", finalPriceCategory, firstEquipmentId)) finalNames = Values(rows, "CategoryName");
        await Wait(() => ComboValues(categories).SetEquals(finalNames));
        if (finalNames.Count > 0) await VerifyItems(pad, firstEquipmentId, finalPriceCategory, Convert.ToString(Get(categories, "Text")));
        Check((string)Call(pad, "GetSelectedEquipmentName") == "수성", "Rapid switching must retain final equipment");
        using (var bitmap = new Bitmap(((Control)pad).Width, ((Control)pad).Height))
        {
            ((Control)pad).DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(output);
        }
        Console.WriteLine("PASS: " + checks + " cascade checks across 2 equipment types and " + categoryCount + " material categories");
    }
    [STAThread] private static int Main(string[] args)
    {
        int exit = 0;
        try
        {
            Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            repository = app.GetType("PineTree.Classes.PriceItemRepository", true);
            var type = app.GetType("PineTree.UserControls.UcOrderDesignPad", true);
            using (var pad = (Control)FormatterServices.GetUninitializedObject(type))
            using (var form = new Form { ClientSize = new Size(1600, 920), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Left = -4000, Top = -4000 })
            {
                typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(pad, new object[0]);
                foreach (string method in new[] { "InitializeComponent", "ApplyDesignPadVisualStyle", "InitOrderItemTable", "InitOrderEditor", "InitPreviewEvent" }) Call(pad, method);
                pad.Dock = DockStyle.Fill; form.Controls.Add(pad);
                form.Shown += async delegate
                {
                    try { await Run(pad, Path.GetFullPath(args[1])); }
                    catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; }
                    finally { form.Close(); }
                };
                Application.Run(form);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        return exit;
    }
}
