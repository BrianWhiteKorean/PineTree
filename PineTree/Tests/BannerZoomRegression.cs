using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
internal static class BannerZoomRegression
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static object Field(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    static object Get(object o, string n) { return o.GetType().GetProperty(n).GetValue(o, null); }
    static void Set(object o, string n, object v) { o.GetType().GetProperty(n).SetValue(o, v, null); }
    static object Call(object o, string n, params object[] args) { return o.GetType().GetMethod(n, F).Invoke(o, args); }
    static decimal Value(object o, string n) { return Convert.ToDecimal(Get(Field(o, n), "Value")); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Mode(object o, string option) { Set(Field(o, "cbZoomType"), "SelectedOption", Field(o, option)); }
    [STAThread] static int Main(string[] args) {
        try {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            foreach (var editorName in new[] { "UcOrder수성_그룹A", "UcOrder수성_그룹B", "UcOrder라텍스_그룹A" })
            {
                var type = app.GetType("PineTree.UserControlsOrder." + editorName, true);
                using (var editor = (UserControl)Activator.CreateInstance(type)) {
                    Check(type.GetField("cbDeploy", F) == null && type.GetField("tbZoomRate", F) == null, "removed controls are absent");
                    Check((int)Get(Get(Field(editor, "cbZoomType"), "Items"), "Count") == 2, "exactly two modes");
                    var edges = new[] { "tbEdgeTop", "tbEdgeBottom", "tbEdgeLeft", "tbEdgeRight" };
                    foreach (var n in edges) Check(Value(editor, n) == 0, "default image expansion is zero");
                    Check(Value(editor, "tbEdgeAll") == 0 && !(bool)Get(Field(editor, "chkAll"), "Checked"), "bulk input starts at zero and unchecked");
                    var pageType = app.GetType("PineTree.Classes.OrderSourcePage", true);
                    var page = Activator.CreateInstance(pageType, true);
                    pageType.GetField("SourceWidthMm").SetValue(page, 1000d);
                    pageType.GetField("SourceHeightMm").SetValue(page, 800d);
                    Call(editor, "ApplySourceMeasurements", page);
                    Check(Value(editor, "tbTargetWidth") == 1000 && Value(editor, "tbTargetHeight") == 800, "original size");
                    type.GetMethod("SetOrderQuantity").Invoke(editor, new object[] { 3m });
                    Mode(editor, "inputOption7");
                    Check(Value(editor, "tbTargetWidth") == 1000 && Value(editor, "tbTargetHeight") == 800, "changing source scale leaves order dimensions unchanged");
                    Check((decimal)Get(editor, "Quantity") == 3m && (decimal)Get(editor, "AreaQuantity") == 2.4m, "changing source scale leaves quantity and area unchanged");
                    Check((double)Get(Call(editor, "ReadPdfGenerationSettings"), "OutputRipScale") == 10, "10x mode delivers a 1:10 PDF");
                    // Round the original source before applying enlargement, including exact halves.
                    pageType.GetField("SourceWidthMm").SetValue(page, 179.7543888889d);
                    pageType.GetField("SourceHeightMm").SetValue(page, 119.7539405639d);
                    Mode(editor, "inputOption6"); Call(editor, "ApplySourceMeasurements", page);
                    Check(Value(editor, "tbTargetWidth") == 180 && Value(editor, "tbTargetHeight") == 120, "original dimensions round before enlargement");
                    Mode(editor, "inputOption7");
                    Check(Value(editor, "tbTargetWidth") == 180 && Value(editor, "tbTargetHeight") == 120, "changing source scale preserves the rounded dimensions");
                    var roundedSettings = Call(editor, "ReadPdfGenerationSettings");
                    Check((double)Get(roundedSettings, "TargetWidthMm") == 1800 && (string)Get(roundedSettings, "SourceSizingMode") == "SCALE", "rounded scaling is captured and sent to output");
                    pageType.GetField("SourceWidthMm").SetValue(page, 10.5d); pageType.GetField("SourceHeightMm").SetValue(page, 8.49d);
                    Call(editor, "ApplySourceMeasurements", page);
                    Check(Value(editor, "tbTargetWidth") == 11 && Value(editor, "tbTargetHeight") == 8, "source dimensions round independently of selected scale");
                    pageType.GetField("SourceWidthMm").SetValue(page, 1000d); pageType.GetField("SourceHeightMm").SetValue(page, 800d);
                    Call(editor, "ApplySourceMeasurements", page);
                    Mode(editor, "inputOption6");
                    Check(Value(editor, "tbTargetWidth") == 1000 && Value(editor, "tbTargetHeight") == 800 && (decimal)Get(editor, "Quantity") == 3m, "switching back preserves dimensions and quantity");
                    Set(Field(editor, "tbEdgeAll"), "Value", 4.125m);
                    foreach (var n in edges) Check(Value(editor, n) == 0, "bulk values are ignored while unchecked");
                    Set(Field(editor, "chkAll"), "Checked", true);
                    foreach (var n in edges) Check(Value(editor, n) == 4.125m, "checking syncs all four edges");
                    Set(Field(editor, "tbEdgeTop"), "Value", 9m);
                    Check(Value(editor, "tbEdgeTop") == 9m && Value(editor, "tbEdgeAll") == 4.125m, "individual edit survives while checked");
                    for (int i = 1; i < edges.Length; i++)
                        Check(Value(editor, edges[i]) == 4.125m, "individual edit leaves the other edges alone");
                    Set(Field(editor, "tbEdgeAll"), "Value", 5m);
                    foreach (var n in edges) Check(Value(editor, n) == 5m, "changing the bulk value syncs again");
                    Set(Field(editor, "chkAll"), "Checked", false);
                    Set(Field(editor, "tbEdgeAll"), "Value", 6m);
                    foreach (var n in edges) Check(Value(editor, n) == 5m, "unchecking preserves the applied edges");
                    var settings = Call(editor, "ReadPdfGenerationSettings");
                    Check((double)Get(settings, "OutputRipScale") == 1, "original mode delivers a 1:1 PDF");
                    Check((bool)Get(settings, "ExpandImage"), "output adds white canvas margins");
                    Check((double)Get(settings, "TargetWidthMm") + (double)Get(settings, "EdgeLeftMm") + (double)Get(settings, "EdgeRightMm") == 1010, "output width 1010");
                    Check((double)Get(settings, "TargetHeightMm") + (double)Get(settings, "EdgeTopMm") + (double)Get(settings, "EdgeBottomMm") == 810, "output height 810");
                    Mode(editor, "inputOption7");
                    var state = type.GetMethod("CaptureEditorState").Invoke(editor, null);
                    Check((decimal)Get(state, "ZoomRate") == 10, "save fixed ten times rate");
                    using (var restored = (UserControl)Activator.CreateInstance(type)) {
                        var json = (string)state.GetType().GetMethod("ToJson").Invoke(state, null);
                        var decoded = state.GetType().GetMethod("FromJson").Invoke(null, new object[] { json });
                        type.GetMethod("RestoreEditorState").Invoke(restored, new[] { decoded });
                        Check(Value(restored, "tbTargetWidth") == 1000 && Value(restored, "tbTargetHeight") == 800 && Value(restored, "tbEdgeTop") == 5 && (int)Get(Field(restored, "cbZoomType"), "SelectedIndex") == 1 && (decimal)Get(restored, "Quantity") == 3m, "state restores scale and unchanged order dimensions");
                        Set(decoded, "ZoomRate", 1.5m); Set(decoded, "WidthMm", 1500m); Set(decoded, "HeightMm", 1200m);
                        type.GetMethod("RestoreEditorState").Invoke(restored, new[] { decoded });
                        Check(Value(restored, "tbTargetWidth") == 1500 && Value(restored, "tbTargetHeight") == 1200 &&
                            (int)Get(Field(restored, "cbZoomType"), "SelectedIndex") < 0, "legacy custom scale retains dimensions without selecting a new mode");
                        Mode(restored, "inputOption6");
                        Check(Value(restored, "tbTargetWidth") == 1500 && Value(restored, "tbTargetHeight") == 1200, "selecting a supported mode retains the legacy dimensions");
                    }
                }
            }
            Console.WriteLine("PASS: banner size / image expansion / saved state: " + checks); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
