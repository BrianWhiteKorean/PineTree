using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class PostProcessSourceScaleRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Field(object target, string name) { return target.GetType().GetField(name, Private).GetValue(target); }
    private static object Get(object target, string name) { return target.GetType().GetProperty(name).GetValue(target, null); }
    private static void Set(object target, string name, object value) { target.GetType().GetProperty(name).SetValue(target, value, null); }
    private static object Call(object target, string method, params object[] args) { return target.GetType().GetMethod(method, Private).Invoke(target, args); }

    private static void Expect(object expected, object actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(description + ": expected " + expected + ", got " + actual);
    }

    private static void CheckNumber(Form dialog, string name, decimal expected)
    {
        Expect(expected, Convert.ToDecimal(Get(Field(dialog, name), "Value")), dialog.GetType().Name + "." + name);
    }

    private static void CheckHoleDialogs(Form parent, decimal divisor)
    {
        decimal offset = 20m / divisor;
        for (int type = 0; type < 3; type++)
        {
            Set(Field(parent, "cbDeployType"), "SelectedIndex", type);
            Exception error = null;
            bool opened = false;
            using (var timer = new Timer { Interval = 30 })
            {
                timer.Tick += delegate
                {
                    Form child = parent.OwnedForms.Length == 0 ? Form.ActiveForm : parent.OwnedForms[0];
                    if (child == null || child == parent || !child.GetType().Name.StartsWith("Dlg타공_")) return;
                    timer.Stop();
                    opened = true;
                    try
                    {
                        if (type == 0)
                        {
                            foreach (var side in new[] { "Top", "Bottom", "Left", "Right" })
                                CheckNumber(child, "tb" + side, offset);
                        }
                        else
                        {
                            string[] sides = type == 1 ? new[] { "Top", "Bottom" } : new[] { "Left", "Right" };
                            foreach (var side in sides)
                            {
                                for (int position = 1; position <= 6; position++)
                                    CheckNumber(child, "tb" + side + "Pos" + position, offset);
                                CheckNumber(child, "tb" + side + "Gap", 0m);
                            }
                            Set(Field(child, "chk" + sides[0] + "Hole"), "Checked", true);
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                        child.Close();
                        return;
                    }
                    ((IButtonControl)Field(child, "btnOK")).PerformClick();
                };
                timer.Start();
                Call(parent, "AddPlacement");
            }
            if (error != null) throw error;
            Expect(true, opened, "Hole dialog opened");
        }

        var placements = (IList)Field(parent, "_placements");
        Expect(3, placements.Count, "Saved hole placements");
        Expect(20d, Get(Get(placements[0], "Edges"), "TopMm"), "Finished corner offset");
        Expect(20d, Get(Get(placements[1], "TopRun"), "EndRightMm"), "Finished top run end");
        Expect(20d, Get(Get(placements[2], "LeftRun"), "EndBottomMm"), "Finished left run end");
    }

    private static void CheckSewDialog(Form dialog, decimal divisor, string field, decimal first, decimal second, int index)
    {
        CheckNumber(dialog, field, first / divisor);
        Set(Field(dialog, "cbType"), "SelectedIndex", index);
        CheckNumber(dialog, field, second / divisor);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var settingType = app.GetType("PineTree.Classes.OrderPostProcessSetting", true);
            foreach (string editorName in new[] { "UcOrder수성_그룹A", "UcOrder수성_그룹B", "UcOrder라텍스_그룹A" })
            using (var editor = (UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder." + editorName, true)))
            {
                editor.GetType().GetField("_sourceWidthMm", Private).SetValue(editor, 500m);
                editor.GetType().GetField("_sourceHeightMm", Private).SetValue(editor, 300m);
                Call(editor, "ApplyZoomDimensions");
                foreach (decimal divisor in new[] { 1m, 10m })
                {
                    var option = Field(editor, divisor == 1m ? "inputOption6" : "inputOption7");
                    Set(Field(editor, "cbZoomType"), "SelectedOption", option);
                    Expect(divisor == 1m ? "1:1" : "1/10", Get(option, "Text"), editorName);
                    foreach (string code in new[] { "HOLE", "WOOD", "WOOD_TB", "POLE_SEW", "HEM_SEW", "BAND_SEW", "ROPE_SEW", "CUT", "AL_SCROLL" })
                    {
                        if (code == "AL_SCROLL" && editorName != "UcOrder수성_그룹B") continue;
                        var setting = code == "HOLE"
                            ? app.GetType("PineTree.UserControlsOrder.Dlg후가공공통_타공마크", true)
                                .GetMethod("CreateEmptySetting", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null)
                            : settingType.GetMethod("Create").Invoke(null, new object[] { code });
                        using (var dialog = (Form)Call(editor, "CreatePostProcessDialog", setting))
                        {
                            switch (code)
                            {
                                case "HOLE": CheckHoleDialogs(dialog, divisor); break;
                                case "WOOD":
                                case "WOOD_TB": CheckNumber(dialog, "tbExtraLine", 80m / divisor); break;
                                case "POLE_SEW":
                                    CheckSewDialog(dialog, divisor, "tbLength", 100m, 80m, 4);
                                    Expect(true, Get(Field(dialog, "chkCutLine"), "Checked"), "Round pole slit");
                                    Set(Field(dialog, "cbType"), "SelectedIndex", 2);
                                    CheckNumber(dialog, "tbLength", 0m);
                                    break;
                                case "HEM_SEW": CheckSewDialog(dialog, divisor, "tbLength", 15m, 30m, 1); break;
                                case "BAND_SEW": CheckSewDialog(dialog, divisor, "tbLineWidth", 30m, 40m, 1); break;
                                case "ROPE_SEW": CheckSewDialog(dialog, divisor, "tbTop", 30m, 40m, 1); break;
                                case "CUT": CheckNumber(dialog, "tbLineWidth", .18m / divisor); break;
                                case "AL_SCROLL": CheckNumber(dialog, "tbTop", 5m / divisor); break;
                            }
                        }
                    }
                }
            }
            Console.WriteLine("PASS: source scale reaches all finishing dialogs in all three order editors.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
